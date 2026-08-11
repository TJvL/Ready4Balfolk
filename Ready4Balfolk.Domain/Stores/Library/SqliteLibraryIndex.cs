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
            if (_connection is not null)
            {
                return;
            }

            dataDirectory.DirectoryInfoRoot.Create();
            var path = Path.Combine(dataDirectory.DirectoryInfoRoot.FullName, DatabaseFileName);

            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());

            await connection.OpenAsync(token);

            // WAL so a crash mid-scan leaves a readable database rather than a truncated one.
            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", token);
            await ExecuteAsync(connection, "PRAGMA synchronous=NORMAL;", token);
            await ExecuteAsync(connection, Schema, token);

            _connection = connection;
            _ = loggerService.InfoAsync($"Library index opened at {path}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, LibraryEntry>> SnapshotByPathAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var connection = Require();
            var entries = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT p.content_hash, p.path, p.file_size, p.last_write_utc,
                       t.duration_ticks, t.format, t.dance_slug, t.original_dance, t.artist, t.title,
                       t.dance_kind, t.dance_detail, t.dance_reason,
                       t.artist_kind, t.artist_detail, t.artist_reason,
                       t.title_kind, t.title_detail, t.title_reason
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
                    TitleFrom = ReadSource(reader, 16)
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
            var connection = Require();
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            // Upsert on the content hash: a file that was renamed or retagged is the same audio and
            // keeps its row. Every column here is derived and is meant to be overwritten by the
            // newest reading; what a person decided is in the approvals table, which this does not
            // touch.
            command.CommandText = """
                INSERT INTO tracks
                    (content_hash, path, file_size, last_write_utc, duration_ticks, format,
                     dance_slug, original_dance, artist, title,
                     dance_kind, dance_detail, dance_reason,
                     artist_kind, artist_detail, artist_reason,
                     title_kind, title_detail, title_reason)
                VALUES ($hash, $path, $size, $written, $duration, $format,
                        $slug, $originalDance, $artist, $title,
                        $danceKind, $danceDetail, $danceReason,
                        $artistKind, $artistDetail, $artistReason,
                        $titleKind, $titleDetail, $titleReason)
                ON CONFLICT(content_hash) DO UPDATE SET
                    path = excluded.path,
                    file_size = excluded.file_size,
                    last_write_utc = excluded.last_write_utc,
                    duration_ticks = excluded.duration_ticks,
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
            pathCommand.CommandText = """
                INSERT INTO track_paths (path, content_hash, file_size, last_write_utc)
                VALUES ($path, $hash, $size, $written)
                ON CONFLICT(path) DO UPDATE SET
                    content_hash = excluded.content_hash,
                    file_size = excluded.file_size,
                    last_write_utc = excluded.last_write_utc;
                """;
            var pathPath = pathCommand.Parameters.Add("$path", SqliteType.Text);
            var pathHash = pathCommand.Parameters.Add("$hash", SqliteType.Blob);
            var pathSize = pathCommand.Parameters.Add("$size", SqliteType.Integer);
            var pathWritten = pathCommand.Parameters.Add("$written", SqliteType.Integer);

            var hash = command.Parameters.Add("$hash", SqliteType.Blob);
            var path = command.Parameters.Add("$path", SqliteType.Text);
            var size = command.Parameters.Add("$size", SqliteType.Integer);
            var written = command.Parameters.Add("$written", SqliteType.Integer);
            var duration = command.Parameters.Add("$duration", SqliteType.Integer);
            var format = command.Parameters.Add("$format", SqliteType.Integer);
            var slug = command.Parameters.Add("$slug", SqliteType.Text);
            var originalDance = command.Parameters.Add("$originalDance", SqliteType.Text);
            var artist = command.Parameters.Add("$artist", SqliteType.Text);
            var title = command.Parameters.Add("$title", SqliteType.Text);
            var sources = SourceColumns
                .Select(field => (
                    Kind: command.Parameters.Add($"${field}Kind", SqliteType.Integer),
                    Detail: command.Parameters.Add($"${field}Detail", SqliteType.Text),
                    Reason: command.Parameters.Add($"${field}Reason", SqliteType.Integer)))
                .ToList();

            foreach (var entry in entries)
            {
                hash.Value = entry.ContentHash;
                path.Value = entry.Path;
                size.Value = entry.FileSize;
                written.Value = entry.LastWriteUtc.Ticks;
                duration.Value = entry.Duration.Ticks;
                format.Value = (int)entry.Format;
                slug.Value = (object?)entry.DanceSlug ?? DBNull.Value;
                originalDance.Value = (object?)entry.OriginalDance ?? DBNull.Value;
                artist.Value = (object?)entry.Artist ?? DBNull.Value;
                title.Value = (object?)entry.Title ?? DBNull.Value;

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
            await using var command = Require().CreateCommand();
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
            var connection = Require();
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            // One answer per field per track: agreeing to something new replaces what was agreed to
            // before rather than leaving two answers to choose between.
            command.CommandText = """
                INSERT INTO approvals (content_hash, field, value, kind, rule, file_write_utc)
                VALUES ($hash, $field, $value, $kind, $rule, $written)
                ON CONFLICT(content_hash, field) DO UPDATE SET
                    value = excluded.value,
                    kind = excluded.kind,
                    rule = excluded.rule,
                    file_write_utc = excluded.file_write_utc;
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
            await ExecuteAsync(Require(), $"DELETE FROM approvals WHERE kind = {(int)ApprovalKind.ByRule};", token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApproveIndividuallyAsync(
        IReadOnlyCollection<string> paths, TrackField field, string value, CancellationToken token = default)
    {
        if (paths.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(token);
        try
        {
            var connection = Require();
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            // By path, but it lands on the audio, so both copies of a duplicated track get the
            // answer rather than only the one that happened to be clicked. The file's write time
            // comes from the row, so a later retag is what marks it changed since it was approved.
            command.CommandText = """
                INSERT INTO approvals (content_hash, field, value, kind, rule, file_write_utc)
                SELECT p.content_hash, $field, $value, $kind, NULL, p.last_write_utc
                FROM track_paths p WHERE p.path = $path
                ON CONFLICT(content_hash, field) DO UPDATE SET
                    value = excluded.value,
                    kind = excluded.kind,
                    rule = NULL,
                    file_write_utc = excluded.file_write_utc;
                """;

            command.Parameters.AddWithValue("$field", (int)field);
            command.Parameters.AddWithValue("$value", value);
            command.Parameters.AddWithValue("$kind", (int)ApprovalKind.Individual);
            var path = command.Parameters.Add("$path", SqliteType.Text);

            foreach (var target in paths)
            {
                path.Value = target;
                await command.ExecuteNonQueryAsync(token);
            }

            await transaction.CommitAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteMissingAsync(IReadOnlyCollection<string> existingPaths, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var connection = Require();
            await using var transaction = await connection.BeginTransactionAsync(token);

            // A temporary table rather than a giant IN clause: a music directory can hold more paths
            // than SQLite will accept as parameters.
            await ExecuteAsync(connection, "CREATE TEMP TABLE IF NOT EXISTS present (path TEXT PRIMARY KEY);", token,
                (SqliteTransaction)transaction);
            await ExecuteAsync(connection, "DELETE FROM present;", token, (SqliteTransaction)transaction);

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText = "INSERT OR IGNORE INTO present (path) VALUES ($path);";
                var path = insert.Parameters.Add("$path", SqliteType.Text);
                foreach (var existing in existingPaths)
                {
                    path.Value = existing;
                    await insert.ExecuteNonQueryAsync(token);
                }
            }

            await ExecuteAsync(connection,
                "DELETE FROM track_paths WHERE path NOT IN (SELECT path FROM present);", token,
                (SqliteTransaction)transaction);

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

    public async Task<IReadOnlySet<string>> GetIgnoredValuesAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = Require().CreateCommand();
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
            await using var command = Require().CreateCommand();
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
            await using var command = Require().CreateCommand();
            command.CommandText = "DELETE FROM ignored_values WHERE folded_value = $folded;";
            command.Parameters.AddWithValue("$folded", StringNormalizer.Normalize(value));
            await command.ExecuteNonQueryAsync(token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CountInReviewAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            await using var command = Require().CreateCommand();
            // Approved on all three fields is the cheapest form of the gate that SQL can answer. The
            // rest of it, whether the dance is one the published list knows and whether the file has
            // changed since, needs the list and is decided by ReviewGate.
            command.CommandText = """
                SELECT COUNT(*) FROM track_paths p
                JOIN tracks t ON t.content_hash = p.content_hash
                WHERE (SELECT COUNT(*) FROM approvals a WHERE a.content_hash = p.content_hash) < 3;
                """;
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

    private SqliteConnection Require() =>
        _connection ?? throw new InvalidOperationException("The library index has not been opened.");

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
            path           TEXT    NOT NULL,
            file_size      INTEGER NOT NULL,
            last_write_utc INTEGER NOT NULL,
            duration_ticks INTEGER NOT NULL,
            format         INTEGER NOT NULL,
            dance_slug     TEXT    NULL,
            original_dance TEXT    NULL,
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
        CREATE INDEX IF NOT EXISTS ix_tracks_path ON tracks (path);

        -- One audio, many places it lives. The same recording on two compilations is not an error
        -- and not a collision: it is one track with two copies, and a decision about it applies to
        -- both. Without this table only one copy was ever remembered, so the other was re-read from
        -- disk on every single startup.
        CREATE TABLE IF NOT EXISTS track_paths (
            path           TEXT    PRIMARY KEY,
            content_hash   BLOB    NOT NULL,
            file_size      INTEGER NOT NULL,
            last_write_utc INTEGER NOT NULL
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
