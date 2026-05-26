using Microsoft.Data.Sqlite;
using MomentTrace.Core;

namespace MomentTrace.Storage;

public sealed class SqliteActivityStore(string databasePath) : IActivityStore
{
    private readonly string _databasePath = databasePath;

    public static string DefaultDatabasePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var databasePath = Path.Combine(appData, "MomentTrace", "timeline.db");
            var legacyDatabasePath = Path.Combine(appData, "NwaogaTimeline", "timeline.db");

            if (!File.Exists(databasePath) && File.Exists(legacyDatabasePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
                File.Copy(legacyDatabasePath, databasePath);
            }

            return databasePath;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS activities (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                app_name TEXT NOT NULL,
                window_title TEXT NOT NULL,
                process_name TEXT NOT NULL,
                executable_path TEXT NULL,
                resource TEXT NULL,
                screenshot_path TEXT NULL,
                kind TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                note TEXT NULL,
                is_bookmarked INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS ix_activities_started_at ON activities(started_at DESC);
            CREATE INDEX IF NOT EXISTS ix_activities_kind ON activities(kind);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "executable_path", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "screenshot_path", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "note", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "is_bookmarked", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
    }

    public async Task<long> AddAsync(TimelineActivity activity, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO activities (
                app_name, window_title, process_name, executable_path, resource, screenshot_path, kind, started_at, ended_at, is_pinned, note, is_bookmarked
            )
            VALUES (
                $app_name, $window_title, $process_name, $executable_path, $resource, $screenshot_path, $kind, $started_at, $ended_at, $is_pinned, $note, $is_bookmarked
            );
            SELECT last_insert_rowid();
            """;

        AddActivityParameters(command, activity);
        var id = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return id;
    }

    public async Task UpdateEndAsync(long id, DateTimeOffset endedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE activities
            SET ended_at = $ended_at
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$ended_at", endedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetPinnedAsync(long id, bool isPinned, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE activities
            SET is_pinned = $is_pinned
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$is_pinned", isPinned ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetNoteAsync(long id, string? note, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE activities
            SET note = $note
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetBookmarkedAsync(long id, bool isBookmarked, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE activities
            SET is_bookmarked = $is_bookmarked
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$is_bookmarked", isBookmarked ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimelineActivity>> QueryAsync(TimelineQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var filters = new List<string>();
        var command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filters.Add("(app_name LIKE $search OR window_title LIKE $search OR process_name LIKE $search)");
            command.Parameters.AddWithValue("$search", $"%{query.SearchText.Trim()}%");
        }

        if (query.Kind is not null)
        {
            filters.Add("kind = $kind");
            command.Parameters.AddWithValue("$kind", query.Kind.Value.ToString());
        }

        if (query.IsPinned is not null)
        {
            filters.Add("is_pinned = $is_pinned");
            command.Parameters.AddWithValue("$is_pinned", query.IsPinned.Value ? 1 : 0);
        }

        if (query.IsBookmarked is not null)
        {
            filters.Add("is_bookmarked = $is_bookmarked");
            command.Parameters.AddWithValue("$is_bookmarked", query.IsBookmarked.Value ? 1 : 0);
        }

        command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 1000));
        command.CommandText = $"""
            SELECT id, app_name, window_title, process_name, executable_path, resource, screenshot_path, kind, started_at, ended_at, is_pinned, note, is_bookmarked
            FROM activities
            {(filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty)}
            ORDER BY is_pinned DESC, started_at DESC
            LIMIT $limit;
            """;

        var activities = new List<TimelineActivity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(ReadActivity(reader));
        }

        return activities;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(builder.ToString());
    }

    private static void AddActivityParameters(SqliteCommand command, TimelineActivity activity)
    {
        command.Parameters.AddWithValue("$app_name", activity.AppName);
        command.Parameters.AddWithValue("$window_title", activity.WindowTitle);
        command.Parameters.AddWithValue("$process_name", activity.ProcessName);
        command.Parameters.AddWithValue("$executable_path", (object?)activity.ExecutablePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$resource", (object?)activity.Resource ?? DBNull.Value);
        command.Parameters.AddWithValue("$screenshot_path", (object?)activity.ScreenshotPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$kind", activity.Kind.ToString());
        command.Parameters.AddWithValue("$started_at", activity.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$ended_at", activity.EndedAt.ToString("O"));
        command.Parameters.AddWithValue("$is_pinned", activity.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue("$note", (object?)activity.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$is_bookmarked", activity.IsBookmarked ? 1 : 0);
    }

    private static TimelineActivity ReadActivity(SqliteDataReader reader)
    {
        return new TimelineActivity
        {
            Id = reader.GetInt64(0),
            AppName = reader.GetString(1),
            WindowTitle = reader.GetString(2),
            ProcessName = reader.GetString(3),
            ExecutablePath = reader.IsDBNull(4) ? null : reader.GetString(4),
            Resource = reader.IsDBNull(5) ? null : reader.GetString(5),
            ScreenshotPath = reader.IsDBNull(6) ? null : reader.GetString(6),
            Kind = Enum.TryParse<ActivityKind>(reader.GetString(7), out var kind) ? kind : ActivityKind.App,
            StartedAt = DateTimeOffset.Parse(reader.GetString(8)),
            EndedAt = DateTimeOffset.Parse(reader.GetString(9)),
            IsPinned = reader.GetInt32(10) == 1,
            Note = reader.IsDBNull(11) ? null : reader.GetString(11),
            IsBookmarked = reader.GetInt32(12) == 1
        };
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "PRAGMA table_info(activities);";

        var exists = false;
        await using (var reader = await schemaCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE activities ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
