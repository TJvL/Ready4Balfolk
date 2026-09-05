using Microsoft.Data.Sqlite;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Stores.Library;

/// <summary>The library index, in SQLite.</summary>
/// <remarks>
/// <para>
/// SQLite for incremental upserts and crash safety, not for read speed: a scan reads the whole
/// table once and answers from memory after that.
/// </para>
/// <para>
/// <c>Microsoft.Data.Sqlite</c> is used in this file and nowhere else. Extracting a data project
/// later should be a file move, not an untangling.
/// </para>
/// </remarks>
public sealed class SqliteLibraryIndex(IApplicationSettingsDirectory dataDirectory, ILoggerService loggerService)
    : ILibraryIndex
{
    private const string DatabaseFileName = "library.sqlite";

    /// <summary>The three fields whose source is stored, in the order their parameters are bound.</summary>
    private static readonly string[] SourceColumns = ["dance", "artist", "title"];

    // One connection, guarded. Writes come from a scan running many files at once, and SQLite would
    // rather serialise them here than hand back "database is locked".
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    public async Task OpenAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await EnsureOpenLockedAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The open connection, opening it first when nobody has yet. The gate must be held.</summary>
    /// <remarks>
    /// On demand rather than at a blessed moment in startup: the settings replay and the toolbar's
    /// badge count both reached this store before the window's Opened handler got to call
    /// <see cref="OpenAsync"/>, and "not opened yet" is an ordering accident the caller can do
    /// nothing about.
    /// </remarks>
    private async Task<SqliteConnection> EnsureOpenLockedAsync(CancellationToken token)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        dataDirectory.DirectoryInfoRoot.Create();
        var path = Path.Combine(dataDirectory.DirectoryInfoRoot.FullName, DatabaseFileName);

        try
        {
            _connection = await OpenAtAsync(path, token);
        }
        catch (SqliteException exception)
        {
            // Everything here except the approvals is recomputed by the next scan, and a database
            // SQLite cannot open has lost them either way. Rebuilding beats an application that
            // starts with an empty library and an error toast forever.
            await loggerService.ErrorAsync(
                $"Library index at {path} is unreadable and will be rebuilt", exception);

            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
            _connection = await OpenAtAsync(path, token);
        }

        return _connection;
    }

    private async Task<SqliteConnection> OpenAtAsync(string path, CancellationToken token)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // One connection is opened and held for as long as the application runs, so a pool
            // buys nothing, and a pooled handle outlives Dispose, which on Windows means the
            // file stays locked and cannot be deleted or moved by anything, including us.
            Pooling = false
        }.ToString());

        try
        {
            await connection.OpenAsync(token);

            // WAL so a crash mid-scan leaves a readable database rather than a truncated one.
            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", token);
            await ExecuteAsync(connection, "PRAGMA synchronous=NORMAL;", token);
            await ExecuteAsync(connection, Schema, token);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _ = loggerService.InfoAsync($"Library index opened at {path}");
        return connection;
    }

    public async Task<IReadOnlyDictionary<string, LibraryEntry>> SnapshotByPathAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            var entries = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT p.content_hash, p.path, p.file_size, p.last_write_utc,
                       t.duration_ticks, t.format, t.dance_slug, t.original_dance, t.artist, t.title,
                       t.dance_kind, t.dance_detail, t.dance_reason,
                       t.artist_kind, t.artist_detail, t.artist_reason,
                       t.title_kind, t.title_detail, t.title_reason,
                       t.custom_tag_names, p.available
                FROM track_paths p
                JOIN tracks t ON t.content_hash = p.content_hash;
                """;

            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var entry = new LibraryEntry
                {
                    ContentHash = (byte[])reader["content_hash"],
                    Path = reader.GetString(1),
                    FileSize = reader.GetInt64(2),
                    LastWriteUtc = new DateTime(reader.GetInt64(3), DateTimeKind.Utc),
                    Duration = TimeSpan.FromTicks(reader.GetInt64(4)),
                    Format = (AudioFormat)reader.GetInt32(5),
                    DanceSlug = reader.IsDBNull(6) ? null : reader.GetString(6),
                    OriginalDance = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Artist = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Title = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Dance = ReadSource(reader, 10),
                    ArtistFrom = ReadSource(reader, 13),
                    TitleFrom = ReadSource(reader, 16),
                    CustomTagNames = reader.IsDBNull(19)
                        ? []
                        : System.Text.Json.JsonSerializer.Deserialize<string[]>(reader.GetString(19)) ?? [],
                    IsAvailable = reader.GetInt32(20) != 0
                };

                entries[entry.Path] = entry;
            }

            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(IReadOnlyCollection<LibraryEntry> entries, CancellationToken token = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            // Upsert on the content hash: a file that was renamed or retagged is the same audio and
            // keeps its row. Every column here is derived and is meant to be overwritten by the
            // newest reading; what a person decided is in the approvals table, which this does not
            // touch.
            command.CommandText = """
                INSERT INTO tracks
                    (content_hash, duration_ticks, format,
                     dance_slug, original_dance, artist, title, custom_tag_names,
                     dance_kind, dance_detail, dance_reason,
                     artist_kind, artist_detail, artist_reason,
                     title_kind, title_detail, title_reason)
                VALUES ($hash, $duration, $format,
                        $slug, $originalDance, $artist, $title, $customTagNames,
                        $danceKind, $danceDetail, $danceReason,
                        $artistKind, $artistDetail, $artistReason,
                        $titleKind, $titleDetail, $titleReason)
                ON CONFLICT(content_hash) DO UPDATE SET
                    duration_ticks = excluded.duration_ticks,
                    custom_tag_names = excluded.custom_tag_names,
                    format = excluded.format,
                    dance_slug = excluded.dance_slug,
                    original_dance = excluded.original_dance,
                    artist = excluded.artist,
                    title = excluded.title,
                    dance_kind = excluded.dance_kind,
                    dance_detail = excluded.dance_detail,
                    dance_reason = excluded.dance_reason,
                    artist_kind = excluded.artist_kind,
                    artist_detail = excluded.artist_detail,
                    artist_reason = excluded.artist_reason,
                    title_kind = excluded.title_kind,
                    title_detail = excluded.title_detail,
                    title_reason = excluded.title_reason;
                """;

            await using var pathCommand = connection.CreateCommand();
            pathCommand.Transaction = (SqliteTransaction)transaction;
            // available = 1 unconditionally: nothing is written here that was not just read off the
            // disk, so a row that was being kept as unavailable is reachable again. That is what
            // makes the watcher clear the flag for a file that comes back on its own.
            pathCommand.CommandText = """
                INSERT INTO track_paths (path, content_hash, file_size, last_write_utc, available)
                VALUES ($path, $hash, $size, $written, 1)
                ON CONFLICT(path) DO UPDATE SET
                    content_hash = excluded.content_hash,
                    file_size = excluded.file_size,
                    last_write_utc = excluded.last_write_utc,
                    available = 1;
                """;
            var pathPath = pathCommand.Parameters.Add("$path", SqliteType.Text);
            var pathHash = pathCommand.Parameters.Add("$hash", SqliteType.Blob);
            var pathSize = pathCommand.Parameters.Add("$size", SqliteType.Integer);
            var pathWritten = pathCommand.Parameters.Add("$written", SqliteType.Integer);

            var hash = command.Parameters.Add("$hash", SqliteType.Blob);
            var duration = command.Parameters.Add("$duration", SqliteType.Integer);
            var format = command.Parameters.Add("$format", SqliteType.Integer);
            var slug = command.Parameters.Add("$slug", SqliteType.Text);
            var originalDance = command.Parameters.Add("$originalDance", SqliteType.Text);
            var artist = command.Parameters.Add("$artist", SqliteType.Text);
            var title = command.Parameters.Add("$title", SqliteType.Text);
            var customTagNames = command.Parameters.Add("$customTagNames", SqliteType.Text);
            var sources = SourceColumns
                .Select(field => (
                    Kind: command.Parameters.Add($"${field}Kind", SqliteType.Integer),
                    Detail: command.Parameters.Add($"${field}Detail", SqliteType.Text),
                    Reason: command.Parameters.Add($"${field}Reason", SqliteType.Integer)))
                .ToList();

            foreach (var entry in entries)
            {
                hash.Value = entry.ContentHash;
                duration.Value = entry.Duration.Ticks;
                format.Value = (int)entry.Format;
                slug.Value = (object?)entry.DanceSlug ?? DBNull.Value;
                originalDance.Value = (object?)entry.OriginalDance ?? DBNull.Value;
                artist.Value = (object?)entry.Artist ?? DBNull.Value;
                title.Value = (object?)entry.Title ?? DBNull.Value;
                customTagNames.Value = entry.CustomTagNames.Count == 0
                    ? DBNull.Value
                    : System.Text.Json.JsonSerializer.Serialize(entry.CustomTagNames);

                foreach (var (source, from) in sources.Zip(
                        [entry.Dance, entry.ArtistFrom, entry.TitleFrom]))
                {
                    source.Kind.Value = from.Kind is { } kind ? (int)kind : DBNull.Value;
                    source.Detail.Value = (object?)from.Detail ?? DBNull.Value;
                    source.Reason.Value = (int)from.Reason;
                }

                await command.ExecuteNonQueryAsync(token);

                pathPath.Value = entry.Path;
                pathHash.Value = entry.ContentHash;
                pathSize.Value = entry.FileSize;
                pathWritten.Value = entry.LastWriteUtc.Ticks;
                await pathCommand.ExecuteNonQueryAsync(token);
            }

            await transaction.CommitAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<TrackApproval>>> ApprovalsAsync(
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = (await EnsureOpenLockedAsync(token)).CreateCommand();
            command.CommandText = "SELECT content_hash, field, value, kind, rule, file_write_utc FROM approvals;";

            var byTrack = new Dictionary<string, List<TrackApproval>>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var hash = (byte[])reader["content_hash"];
                var approval = new TrackApproval
                {
                    ContentHash = hash,
                    Field = (TrackField)reader.GetInt32(1),
                    Value = reader.GetString(2),
                    Kind = (ApprovalKind)reader.GetInt32(3),
                    Rule = reader.IsDBNull(4) ? null : reader.GetString(4),
                    FileWriteUtc = new DateTime(reader.GetInt64(5), DateTimeKind.Utc)
                };

                if (!byTrack.TryGetValue(LibraryKey.For(hash), out var list))
                {
                    list = [];
                    byTrack[LibraryKey.For(hash)] = list;
                }

                list.Add(approval);
            }

            return byTrack.ToDictionary(
                pair => pair.Key, pair => (IReadOnlyList<TrackApproval>)pair.Value, StringComparer.Ordinal);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApproveAsync(IReadOnlyCollection<TrackApproval> approvals, CancellationToken token = default)
    {
        if (approvals.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            // One answer per field per track: a rule answering again replaces what a rule answered
            // before rather than leaving two answers to choose between. It stops at a row somebody
            // looked at and answered themselves, which is what makes an individual approval sticky:
            // without the guard a retag, a rename or a newly declared pattern quietly replaced the
            // hand correction, and the row being ByRule afterwards meant the next rule change
            // revoked it outright. The write time is left alone with it, or the rule's clock would
            // stand in for when the person agreed and the track would stop coming back after a
            // retag.
            command.CommandText = $$"""
                INSERT INTO approvals (content_hash, field, value, kind, rule, file_write_utc)
                VALUES ($hash, $field, $value, $kind, $rule, $written)
                ON CONFLICT(content_hash, field) DO UPDATE SET
                    value = excluded.value,
                    kind = excluded.kind,
                    rule = excluded.rule,
                    file_write_utc = excluded.file_write_utc
                WHERE approvals.kind = {{(int)ApprovalKind.ByRule}};
                """;

            var hash = command.Parameters.Add("$hash", SqliteType.Blob);
            var field = command.Parameters.Add("$field", SqliteType.Integer);
            var value = command.Parameters.Add("$value", SqliteType.Text);
            var kind = command.Parameters.Add("$kind", SqliteType.Integer);
            var rule = command.Parameters.Add("$rule", SqliteType.Text);
            var written = command.Parameters.Add("$written", SqliteType.Integer);

            foreach (var approval in approvals)
            {
                hash.Value = approval.ContentHash;
                field.Value = (int)approval.Field;
                value.Value = approval.Value;
                kind.Value = (int)approval.Kind;
                rule.Value = (object?)approval.Rule ?? DBNull.Value;
                written.Value = approval.FileWriteUtc.Ticks;
                await command.ExecuteNonQueryAsync(token);
            }

            await transaction.CommitAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RevokeRuleApprovalsAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await ExecuteAsync(await EnsureOpenLockedAsync(token), $"DELETE FROM approvals WHERE kind = {(int)ApprovalKind.ByRule};", token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApproveIndividuallyAsync(
        IReadOnlyCollection<string> paths, IReadOnlyCollection<FieldAnswer> answers, CancellationToken token = default)
    {
        if (paths.Count == 0 || answers.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            // By path, but it lands on the audio, so both copies of a duplicated track get the
            // answer rather than only the one that happened to be clicked. The write time is the
            // NEWEST among the copies, for the same reason: an approval given via the older copy
            // must not leave the newer one reading as "changed since" forever. A later retag of
            // any copy still moves past it and flags the track.
            command.CommandText = """
                INSERT INTO approvals (content_hash, field, value, kind, rule, file_write_utc)
                SELECT p.content_hash, $field, $value, $kind, NULL,
                       (SELECT MAX(q.last_write_utc) FROM track_paths q WHERE q.content_hash = p.content_hash)
                FROM track_paths p WHERE p.path = $path
                ON CONFLICT(content_hash, field) DO UPDATE SET
                    value = excluded.value,
                    kind = excluded.kind,
                    rule = NULL,
                    file_write_utc = excluded.file_write_utc;
                """;

            var field = command.Parameters.Add("$field", SqliteType.Integer);
            var value = command.Parameters.Add("$value", SqliteType.Text);
            command.Parameters.AddWithValue("$kind", (int)ApprovalKind.Individual);
            var path = command.Parameters.Add("$path", SqliteType.Text);

            // One transaction for a whole answer: a row confirms three fields, a folder dozens of
            // rows, and a transaction per field made that 3N commits.
            foreach (var answer in answers)
            {
                field.Value = (int)answer.Field;
                value.Value = answer.Value;
                foreach (var target in paths)
                {
                    path.Value = target;
                    await command.ExecuteNonQueryAsync(token);
                }
            }

            await transaction.CommitAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteMissingAsync(
        IReadOnlyCollection<string> existingPaths,
        IReadOnlyCollection<string> unavailablePaths,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(unavailablePaths);

        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);

            // A temporary table rather than a giant IN clause: a music directory can hold more paths
            // than SQLite will accept as parameters.
            await ExecuteAsync(connection,
                "CREATE TEMP TABLE IF NOT EXISTS present (path TEXT PRIMARY KEY, found INTEGER NOT NULL);",
                token, (SqliteTransaction)transaction);
            await ExecuteAsync(connection, "DELETE FROM present;", token, (SqliteTransaction)transaction);

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText = "INSERT OR IGNORE INTO present (path, found) VALUES ($path, $found);";
                var path = insert.Parameters.Add("$path", SqliteType.Text);
                var found = insert.Parameters.Add("$found", SqliteType.Integer);

                found.Value = 1;
                foreach (var existing in existingPaths)
                {
                    path.Value = existing;
                    await insert.ExecuteNonQueryAsync(token);
                }

                found.Value = 0;
                foreach (var kept in unavailablePaths)
                {
                    path.Value = kept;
                    await insert.ExecuteNonQueryAsync(token);
                }
            }

            await ExecuteAsync(connection,
                "DELETE FROM track_paths WHERE path NOT IN (SELECT path FROM present);", token,
                (SqliteTransaction)transaction);

            // What was found is reachable again, whatever it was last time; what the user said to
            // keep is not. Both directions in one statement, so a folder coming back needs nothing
            // more than the scan that finds it.
            await ExecuteAsync(connection,
                "UPDATE track_paths SET available = (SELECT found FROM present WHERE present.path = track_paths.path);",
                token, (SqliteTransaction)transaction);

            // An audio nothing points at any more is gone, along with whatever was decided about it.
            await ExecuteAsync(connection,
                "DELETE FROM tracks WHERE content_hash NOT IN (SELECT content_hash FROM track_paths);",
                token, (SqliteTransaction)transaction);

            await ExecuteAsync(connection,
                "DELETE FROM approvals WHERE content_hash NOT IN (SELECT content_hash FROM track_paths);",
                token, (SqliteTransaction)transaction);

            await transaction.CommitAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeletePathAsync(string path, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var connection = await EnsureOpenLockedAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);

            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)transaction;
                delete.CommandText = "DELETE FROM track_paths WHERE path = $path;";
                delete.Parameters.AddWithValue("$path", path);
                await delete.ExecuteNonQueryAsync(token);
            }

            // The same conclusion a full scan draws: an audio nothing points at any more is gone,
            // along with whatever was decided about it. A rename must therefore write the new path
            // before deleting the old one, or the approvals fall through this hole.
            await ExecuteAsync(connection,
                "DELETE FROM tracks WHERE content_hash NOT IN (SELECT content_hash FROM track_paths);",
                token, (SqliteTransaction)transaction);
            await ExecuteAsync(connection,
                "DELETE FROM approvals WHERE content_hash NOT IN (SELECT content_hash FROM track_paths);",
                token, (SqliteTransaction)transaction);

            await transaction.CommitAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlySet<string>> GetIgnoredValuesAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = (await EnsureOpenLockedAsync(token)).CreateCommand();
            command.CommandText = "SELECT folded_value FROM ignored_values;";

            var values = new HashSet<string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task IgnoreValueAsync(string value, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = (await EnsureOpenLockedAsync(token)).CreateCommand();
            command.CommandText =
                "INSERT OR IGNORE INTO ignored_values (folded_value, value) VALUES ($folded, $value);";
            command.Parameters.AddWithValue("$folded", StringNormalizer.Normalize(value));
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopIgnoringValueAsync(string value, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = (await EnsureOpenLockedAsync(token)).CreateCommand();
            command.CommandText = "DELETE FROM ignored_values WHERE folded_value = $folded;";
            command.Parameters.AddWithValue("$folded", StringNormalizer.Normalize(value));
            await command.ExecuteNonQueryAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CountIndexedAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = (await EnsureOpenLockedAsync(token)).CreateCommand();
            // Reachable rows only. A progress line that counted a dead NAS's twenty thousand would
            // sit above the number of files there are to read and never move.
            command.CommandText = "SELECT COUNT(*) FROM track_paths WHERE available <> 0;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(token), provider: null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        _gate.Dispose();
    }

    /// <summary>Where one value came from, read from the three columns that hold it.</summary>
    private static DerivedFrom ReadSource(SqliteDataReader reader, int at) => new(
        reader.IsDBNull(at) ? null : (ClaimSourceKind)reader.GetInt32(at),
        reader.IsDBNull(at + 1) ? null : reader.GetString(at + 1),
        (DecisionReason)reader.GetInt32(at + 2));

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken token,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync(token);
    }

    /// <remarks>
    /// <c>id INTEGER PRIMARY KEY</c> is an alias for the rowid, so the table has no second index to
    /// maintain. <c>content_hash</c> is the natural key and is what an upsert conflicts on; the
    /// index on <c>path</c> is what a scan looks a file up by.
    /// </remarks>
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS tracks (
            id             INTEGER PRIMARY KEY,
            content_hash   BLOB    NOT NULL UNIQUE,
            -- The paths, sizes and write times live in track_paths alone: one audio, many copies,
            -- and a second copy of any of them here would only be a copy nothing reads.
            duration_ticks INTEGER NOT NULL,
            format         INTEGER NOT NULL,
            dance_slug     TEXT    NULL,
            original_dance TEXT    NULL,
            -- The file's free-form tag names, as JSON. The rules panel counts these against a
            -- declared custom dance tag, which must not cost opening every file.
            custom_tag_names TEXT  NULL,
            artist         TEXT    NULL,
            title          TEXT    NULL,
            -- Where each value came from and how it was decided. Review shows a value next to its
            -- source, because a wrong artist is only obvious when it says it was read off a folder.
            dance_kind     INTEGER NULL,
            dance_detail   TEXT    NULL,
            dance_reason   INTEGER NOT NULL DEFAULT 0,
            artist_kind    INTEGER NULL,
            artist_detail  TEXT    NULL,
            artist_reason  INTEGER NOT NULL DEFAULT 0,
            title_kind     INTEGER NULL,
            title_detail   TEXT    NULL,
            title_reason   INTEGER NOT NULL DEFAULT 0
        );

        -- One audio, many places it lives. The same recording on two compilations is not an error
        -- and not a collision: it is one track with two copies, and a decision about it applies to
        -- both. Without this table only one copy was ever remembered, so the other was re-read from
        -- disk on every single startup.
        CREATE TABLE IF NOT EXISTS track_paths (
            path           TEXT    PRIMARY KEY,
            content_hash   BLOB    NOT NULL,
            file_size      INTEGER NOT NULL,
            last_write_utc INTEGER NOT NULL,
            -- Whether the file was there the last time a scan looked. Per path, because one audio
            -- can sit on a local disk and on a NAS at once and only one of the two goes away with
            -- the mount. A row is only ever 0 because somebody was asked and said to keep it.
            available      INTEGER NOT NULL DEFAULT 1
        );
        CREATE INDEX IF NOT EXISTS ix_track_paths_hash ON track_paths (content_hash);
        CREATE INDEX IF NOT EXISTS ix_tracks_unresolved ON tracks (dance_slug) WHERE dance_slug IS NULL;

        -- What a person agreed to, kept apart from everything that was derived. A scan rewrites the
        -- tracks table freely because all of it is recomputable; nothing in here is, so nothing in a
        -- scan touches it. Keyed on the audio, so a retag or a rename keeps the answer.
        CREATE TABLE IF NOT EXISTS approvals (
            content_hash   BLOB    NOT NULL,
            field          INTEGER NOT NULL,
            value          TEXT    NOT NULL,
            kind           INTEGER NOT NULL,
            rule           TEXT    NULL,
            file_write_utc INTEGER NOT NULL,
            PRIMARY KEY (content_hash, field)
        );

        CREATE TABLE IF NOT EXISTS ignored_values (
            folded_value TEXT PRIMARY KEY,
            value        TEXT NOT NULL
        );
        """;
}
