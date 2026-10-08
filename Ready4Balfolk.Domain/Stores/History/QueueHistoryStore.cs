using System.Globalization;
using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Data.Sqlite;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.History;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Domain.Stores.History;

/// <summary>The nights, in SQLite.</summary>
/// <remarks>
/// <para>
/// A night is a row and every entry is a row appended to it. The file this replaced was rewritten in
/// full after every single entry, truncated first and then serialised, so a machine that stopped
/// inside that window left a partial file and the evening read as though it had never happened.
/// Appending has no such window, and appending is what is actually happening.
/// </para>
/// <para>
/// Entries keep their polymorphic JSON as a payload rather than being flattened into columns: the
/// models do not change, and ordering is the database's problem. The kind and the start are lifted
/// out of the payload beside it for whoever opens the file to look at an evening, which nothing in
/// this class does: it reads the payload, and counts a night by its rows.
/// </para>
/// <para>
/// The file carries the shape it was laid out in, in <c>PRAGMA user_version</c>, and a file stamped
/// with any other shape is refused and left exactly as it is, unlike the library index, which
/// rebuilds itself. That one is derived and a scan puts it back; this one is the only copy there is
/// of the evenings in it.
/// </para>
/// </remarks>
public sealed class QueueHistoryStore(
    IApplicationSettingsDirectory dataDirectory,
    IFileSystem fileSystem,
    ILoggerService loggerService,
    INotificationService notifications,
    TimeProvider time)
    : IQueueHistoryStore
{
    private const string DatabaseFileName = "history.sqlite";

    /// <summary>The shape this build was written against, stamped in the file's user_version.</summary>
    /// <remarks>
    /// Raise it in the same commit as any change to <see cref="Schema"/>, together with whatever
    /// carries an older file's nights across to the new shape: a file stamped with any number but
    /// this one is not opened at all. Zero is the one exception, because it is what every file
    /// written before the stamp existed reads back, and those were all laid out in exactly this
    /// shape, so they are stamped as they stand.
    /// </remarks>
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            // Lenient for the reason the settings are: a status added by a later build would
            // otherwise throw on the one entry carrying it, and that throw takes the whole night
            // with it on any build that does not know the name. One entry read with the wrong
            // status is the price instead. Written exactly as the stock converter writes it.
            new LenientEnumConverter()
        }
    };

    /// <summary>The same night, without where the files are.</summary>
    /// <remarks>
    /// An export is a record of an evening for whoever asked for one, and a path says nothing
    /// about the evening and everything about the DJ's disk. The payload in the database keeps it:
    /// that is what the duplicate rule and the random picker read a played track back by.
    /// </remarks>
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonOptions)
    {
        // A name is written as itself rather than as escapes. The default encoder escapes every
        // non-ASCII character so that JSON can be dropped straight into an HTML page, and an export
        // is a file somebody opens and reads: nothing embeds it, so the defence buys nothing here
        // and costs a DJ a night of Bourrees written as Bourr\u00e9es. "Unsafe" means unsafe to
        // embed; quotes, backslashes and control characters are still escaped, so this is the same
        // JSON either way and parses to the same strings.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                DropTrackFilePath
            }
        }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<QueueHistory> _history = new(QueueHistory.Empty);
    private readonly BehaviorSubject<bool> _isLoading = new(false);

    private SqliteConnection? _connection;

    public IObservable<bool> IsLoading => _isLoading.AsObservable();

    public QueueHistory Current => _history.Value;

    public IObservable<QueueHistory> Observe() => _history.AsObservable();

    public async Task LoadAsync(CancellationToken token)
    {
        _isLoading.OnNext(true);
        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            var night = await ReadCurrentNightAsync(connection, token);
            if (night is not null)
            {
                _history.OnNext(night);
                _ = loggerService.InfoAsync($"Loaded the current night ({night.Entries.Count} entries)");
            }
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            // Deliberately not deleted and rebuilt the way the library index is. The index is
            // derived and a scan puts it back; a history is the only copy there is of an evening,
            // and an unreadable one is a thing to look at rather than a thing to tidy away.
            loggerService.Report(
                "Failed to read the history database", notifications, DomainStrings.History_ReadFailed, exception);
        }
        finally
        {
            _gate.Release();
            _isLoading.OnNext(false);
        }
    }

    public async Task AddAsync(QueueHistoryEntry entry)
    {
        await _gate.WaitAsync();
        try
        {
            var current = Current;
            var startedAt = current.StartedAt ?? time.GetLocalNow().DateTime;
            var entries = new List<QueueHistoryEntry>(current.Entries)
            {
                entry
            };

            // The screen is updated whether or not the write lands. What is on it is what a person
            // is reading between tracks, and a database that has gone away is a thing to log rather
            // than a reason to blank the evening in front of them.
            var nightId = await AppendEntryAsync(current.Id, startedAt, entry, entries.Count - 1);
            _history.OnNext(current with { StartedAt = startedAt, Entries = entries, Id = nightId });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EndNightAsync(DateTime? endedAt = null)
    {
        await _gate.WaitAsync();
        try
        {
            var current = Current;
            if (current.Id != 0)
            {
                await ExecuteOnNightAsync(
                    "UPDATE nights SET ended_at = $endedAt WHERE id = $id;",
                    current.Id,
                    "Failed to end the night",
                    DomainStrings.History_EndNightFailed,
                    command => command.Parameters.AddWithValue(
                        "$endedAt", Format(endedAt ?? time.GetLocalNow().DateTime)));
            }

            _history.OnNext(QueueHistory.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteNightAsync(long nightId)
    {
        await _gate.WaitAsync();
        try
        {
            if (nightId == 0)
            {
                return;
            }

            await ExecuteOnNightAsync(
                """
                DELETE FROM entries WHERE night_id = $id;
                DELETE FROM nights WHERE id = $id;
                """,
                nightId,
                "Failed to delete the night",
                DomainStrings.History_DeleteNightFailed);

            // Only the running night is on screen as it happens; throwing away a filed one leaves
            // tonight alone.
            if (nightId == Current.Id)
            {
                _history.OnNext(QueueHistory.Empty);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<NightSummary>> ListNightsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var connection = await EnsureOpenLockedAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT n.id, n.started_at, n.ended_at, COUNT(e.ordinal)
                FROM nights n LEFT JOIN entries e ON e.night_id = n.id
                GROUP BY n.id
                ORDER BY n.id DESC;
                """;

            var nights = new List<NightSummary>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                nights.Add(new NightSummary(
                    reader.GetInt64(0),
                    Parse(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : Parse(reader.GetString(2)),
                    reader.GetInt32(3)));
            }

            return nights;
        }
        catch (Exception exception) when (CannotOpen(exception) || exception is FormatException)
        {
            loggerService.Report(
                "Failed to list the nights", notifications, DomainStrings.History_ListNightsFailed, exception);
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QueueHistory?> ReadNightAsync(long nightId)
    {
        // The running night is already in memory, and it is the only one that is still changing.
        if (nightId != 0 && nightId == Current.Id)
        {
            return Current;
        }

        await _gate.WaitAsync();
        try
        {
            var connection = await EnsureOpenLockedAsync(CancellationToken.None);
            return await ReadNightLockedAsync(connection, nightId, CancellationToken.None);
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            loggerService.Report(
                "Failed to read a night", notifications, DomainStrings.History_ReadNightFailed, exception);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ExportAsync(long nightId, string destinationPath) =>
        WriteExportAsync(nightId, destinationPath,
            (stream, night) => JsonSerializer.SerializeAsync(stream, night, ExportJsonOptions));

    public Task ExportReportAsync(long nightId, string destinationPath) =>
        WriteExportAsync(nightId, destinationPath, async (stream, night) =>
        {
            // No byte order mark: the document says it is UTF-8 in its own head, and a browser that
            // reads that mark as content puts it on screen above the heading. The stream is left
            // open because the caller owns it, the same as it does for the JSON export.
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            await writer.WriteAsync(NightReport.Render(night));
        });

    public Task ExportSpreadsheetAsync(long nightId, string destinationPath) =>
        WriteExportAsync(nightId, destinationPath, async (stream, night) =>
        {
            // With a byte order mark, which is the opposite call to the report's and for the same
            // reason: CSV carries no way to say what it is encoded in, and Excel opened without one
            // reads the file in the machine's own code page. That turns every accented name in a
            // balfolk evening into mojibake, which is most of them.
            await using var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true);
            await writer.WriteAsync(NightSpreadsheet.Render(night));
        });

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        _gate.Dispose();
        _history.Dispose();
        _isLoading.Dispose();
    }

    /// <summary>Reads the night and hands it to whoever writes the file, in whichever format.</summary>
    private async Task WriteExportAsync(
        long nightId, string destinationPath, Func<Stream, QueueHistory, Task> write)
    {
        var night = await ReadNightAsync(nightId);
        if (night is null)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            var destination = fileSystem.FileInfo.New(destinationPath);
            destination.Directory?.Create();
            await using var stream = fileSystem.File.Create(destination.FullName);
            await write(stream, night);
            _ = loggerService.InfoAsync($"Exported the night to {LogPaths.Name(destination.FullName)}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The open connection, opening it first when nobody has yet. The gate must be held.</summary>
    private async Task<SqliteConnection> EnsureOpenLockedAsync(CancellationToken token)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        dataDirectory.DirectoryInfoRoot.Create();
        var path = Path.Combine(dataDirectory.DirectoryInfoRoot.FullName, DatabaseFileName);

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Held for as long as the application runs, so a pool buys nothing, and a pooled handle
            // outlives Dispose and keeps the file locked after it.
            Pooling = false
        }.ToString());

        try
        {
            await connection.OpenAsync(token);

            // Asked before anything is written, the journal mode included, so a file this build
            // will not open is left byte for byte as it was found.
            var stamped = await ReadSchemaVersionAsync(connection, token);
            if (stamped is not 0 and not SchemaVersion)
            {
                throw new InvalidDataException(
                    $"{DatabaseFileName} was laid out for schema {stamped} and this build expects "
                    + $"{SchemaVersion}; leaving it as it is");
            }

            // WAL so a machine that stops mid-evening leaves a readable database rather than a
            // truncated one. That is the whole reason this is not a file being rewritten.
            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", token);
            await ExecuteAsync(connection, "PRAGMA synchronous=NORMAL;", token);
            await ExecuteAsync(connection, Schema, token);
            // Interpolated rather than bound: a pragma takes no parameters, and the value is a
            // constant of this file rather than anything a caller supplies.
            await ExecuteAsync(connection, $"PRAGMA user_version = {SchemaVersion};", token);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _connection = connection;
        _ = loggerService.InfoAsync($"History opened ({DatabaseFileName})");
        return connection;
    }

    /// <summary>The newest night, or nothing when the newest one has already been filed.</summary>
    private static async Task<QueueHistory?> ReadCurrentNightAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, started_at, ended_at FROM nights ORDER BY id DESC LIMIT 1;";

        long id;
        DateTime startedAt;
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            // A night that ended is not the current one, and the next has no row until something
            // happens in it, so there is nothing to come back to.
            if (!reader.IsDBNull(2))
            {
                return null;
            }

            id = reader.GetInt64(0);
            startedAt = Parse(reader.GetString(1));
        }

        return new QueueHistory(startedAt, await ReadEntriesAsync(connection, id, token))
        {
            Id = id
        };
    }

    /// <summary>One night with its entries, or nothing when no night has that id.</summary>
    private static async Task<QueueHistory?> ReadNightLockedAsync(
        SqliteConnection connection, long nightId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT started_at, ended_at FROM nights WHERE id = $id;";
        command.Parameters.AddWithValue("$id", nightId);

        DateTime startedAt;
        DateTime? endedAt;
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            startedAt = Parse(reader.GetString(0));
            endedAt = reader.IsDBNull(1) ? null : Parse(reader.GetString(1));
        }

        return new QueueHistory(startedAt, await ReadEntriesAsync(connection, nightId, token))
        {
            Id = nightId,
            EndedAt = endedAt
        };
    }

    private static async Task<List<QueueHistoryEntry>> ReadEntriesAsync(
        SqliteConnection connection, long nightId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM entries WHERE night_id = $id ORDER BY ordinal;";
        command.Parameters.AddWithValue("$id", nightId);

        var entries = new List<QueueHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            if (JsonSerializer.Deserialize<QueueHistoryEntry>(reader.GetString(0), JsonOptions) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>Writes the entry, opening the night first when this is the first thing in it.</summary>
    /// <remarks>Returns the night it was written to, or the one that was passed in when the write failed.</remarks>
    private async Task<long> AppendEntryAsync(long nightId, DateTime startedAt, QueueHistoryEntry entry, int ordinal)
    {
        try
        {
            var connection = await EnsureOpenLockedAsync(CancellationToken.None);
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None);
            var id = nightId;

            // A night exists once something has happened in it, so an evening nobody played
            // anything on leaves nothing behind at all.
            if (id == 0)
            {
                await using var openNight = connection.CreateCommand();
                openNight.Transaction = (SqliteTransaction)transaction;
                openNight.CommandText =
                    "INSERT INTO nights (started_at) VALUES ($startedAt); SELECT last_insert_rowid();";
                openNight.Parameters.AddWithValue("$startedAt", Format(startedAt));
                id = Convert.ToInt64(await openNight.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            }

            var payload = JsonSerializer.Serialize(entry, JsonOptions);

            await using var append = connection.CreateCommand();
            append.Transaction = (SqliteTransaction)transaction;
            append.CommandText = """
                INSERT INTO entries (night_id, ordinal, kind, started_at, payload)
                VALUES ($nightId, $ordinal, $kind, $startedAt, $payload);
                """;
            append.Parameters.AddWithValue("$nightId", id);
            append.Parameters.AddWithValue("$ordinal", ordinal);
            append.Parameters.AddWithValue("$kind", KindOf(payload));
            append.Parameters.AddWithValue("$startedAt",
                entry.StartedAt is { } at ? Format(at) : DBNull.Value);
            append.Parameters.AddWithValue("$payload", payload);
            await append.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return id;
        }
        catch (Exception exception) when (CannotOpen(exception))
        {
            loggerService.Report(
                "Failed to write a history entry", notifications, DomainStrings.History_WriteFailed, exception);
            return nightId;
        }
    }

    private async Task ExecuteOnNightAsync(
        string sql, long nightId, string logLine, string screenText, Action<SqliteCommand>? bind = null)
    {
        try
        {
            var connection = await EnsureOpenLockedAsync(CancellationToken.None);

            // One transaction whatever the statement count. Deleting a night is two statements, and
            // outside one a failure between them left a night with its entries gone and its row
            // still listed: an evening that reads as one where nothing happened.
            await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", nightId);
            bind?.Invoke(command);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch (Exception exception) when (CannotOpen(exception))
        {
            loggerService.Report(logLine, notifications, screenText, exception);
        }
    }

    /// <summary>What opening the file or writing to it can meet.</summary>
    /// <remarks>
    /// An InvalidDataException is a file stamped for a shape this build does not know, which is
    /// refused rather than opened, and is reported the way any other database failure is.
    /// </remarks>
    private static bool CannotOpen(Exception exception) =>
        exception is SqliteException or InvalidDataException;

    /// <summary>What reading a night back can meet, on top of what opening the file can.</summary>
    /// <remarks>
    /// A JsonException is an entry whose payload will not parse, and a FormatException a stored
    /// time that will not.
    /// </remarks>
    private static bool IsUnreadable(Exception exception) =>
        CannotOpen(exception) || exception is JsonException or FormatException;

    /// <summary>Which shape the file on disk was laid out in. Zero until something stamps it.</summary>
    private static async Task<long> ReadSchemaVersionAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), provider: null);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private static void DropTrackFilePath(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(TrackHistoryEntry))
        {
            return;
        }

        // Found by the property it serialises rather than by the name it is stored under, so the
        // path stays out of an export even if the two ever come apart.
        var filePath = typeInfo.Properties.FirstOrDefault(property =>
            property.AttributeProvider is PropertyInfo { Name: nameof(TrackHistoryEntry.FilePath) });
        if (filePath is not null)
        {
            typeInfo.Properties.Remove(filePath);
        }
    }

    /// <summary>The entry's kind, read back off the payload so the column cannot drift from it.</summary>
    private static string KindOf(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("type", out var type)
            ? type.GetString() ?? ""
            : "";
    }

    // Round-trip text rather than ticks: a night is a thing somebody may well open the database to
    // look at, and a number nobody can read is a poor way to store a date that has to be legible.
    private static string Format(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime Parse(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <remarks>
    /// <c>id INTEGER PRIMARY KEY</c> is an alias for the rowid, so a night carries no second index.
    /// The entries are keyed on their position in the night, which is the order they are read back
    /// in and the only order they have ever had.
    /// </remarks>
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS nights (
            id         INTEGER PRIMARY KEY,
            started_at TEXT NOT NULL,
            -- Null while this is the night that is running. Set once, and never by anything but a
            -- person or the end of the night playing out.
            ended_at   TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS entries (
            night_id   INTEGER NOT NULL,
            ordinal    INTEGER NOT NULL,
            -- This and started_at are lifted out of the payload for a person opening the file, so
            -- what an evening was made of can be read without parsing JSON. Nothing in the
            -- application queries either; it reads the payload.
            kind       TEXT    NOT NULL,
            started_at TEXT    NULL,
            payload    TEXT    NOT NULL,
            PRIMARY KEY (night_id, ordinal)
        );
        """;
}
