using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using NotesCommander.Backend.Models;

namespace NotesCommander.Backend.Storage;

public sealed class NoteStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _initialized;

    public NoteStore(IOptions<NoteStorageOptions> options)
    {
        var databasePath = options.Value.DatabasePath;
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public async Task<NoteRecord> CreateAsync(NoteRecord note, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        note.Id = Guid.NewGuid();
        note.CreatedAt = DateTimeOffset.UtcNow;
        note.UpdatedAt = note.CreatedAt;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO Notes(Id, Title, CategoryLabel, OriginalText, RecognizedText, RecognitionStatus, ErrorMessage, AudioPath, PhotoPaths, CreatedAt, UpdatedAt) VALUES (@id, @title, @category, @original, @recognized, @status, @error, @audio, @photos, @createdAt, @updatedAt);";
        command.Parameters.AddWithValue("@id", note.Id.ToString());
        command.Parameters.AddWithValue("@title", note.Title);
        command.Parameters.AddWithValue("@category", note.CategoryLabel);
        command.Parameters.AddWithValue("@original", note.OriginalText ?? string.Empty);
        command.Parameters.AddWithValue("@recognized", note.RecognizedText ?? string.Empty);
        command.Parameters.AddWithValue("@status", note.RecognitionStatus.ToString());
        command.Parameters.AddWithValue("@error", note.ErrorMessage ?? string.Empty);
        command.Parameters.AddWithValue("@audio", note.AudioPath ?? string.Empty);
        command.Parameters.AddWithValue("@photos", JsonSerializer.Serialize(note.PhotoPaths));
        command.Parameters.AddWithValue("@createdAt", note.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@updatedAt", note.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);

        return note;
    }

    public async Task<NoteRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, CategoryLabel, OriginalText, RecognizedText, RecognitionStatus, ErrorMessage, AudioPath, PhotoPaths, CreatedAt, UpdatedAt FROM Notes WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<NoteRecord>> ListByStatusAsync(NoteRecognitionStatus status, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, CategoryLabel, OriginalText, RecognizedText, RecognitionStatus, ErrorMessage, AudioPath, PhotoPaths, CreatedAt, UpdatedAt FROM Notes WHERE RecognitionStatus = @status";
        command.Parameters.AddWithValue("@status", status.ToString());

        var result = new List<NoteRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task UpdateStatusAsync(Guid id, NoteRecognitionStatus status, string? recognizedText, string? categoryLabel, string? errorMessage, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE Notes SET RecognitionStatus = @status, RecognizedText = @recognized, CategoryLabel = CASE WHEN @category IS NULL OR @category = '' THEN CategoryLabel ELSE @category END, ErrorMessage = @error, UpdatedAt = @updatedAt WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@recognized", recognizedText ?? string.Empty);
        command.Parameters.AddWithValue("@category", categoryLabel ?? string.Empty);
        command.Parameters.AddWithValue("@error", errorMessage ?? string.Empty);
        command.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS Notes (Id TEXT PRIMARY KEY, Title TEXT NOT NULL, CategoryLabel TEXT NOT NULL, OriginalText TEXT, RecognizedText TEXT, RecognitionStatus TEXT NOT NULL, ErrorMessage TEXT, AudioPath TEXT, PhotoPaths TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);";
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private static NoteRecord Map(SqliteDataReader reader)
    {
        return new NoteRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            Title = reader.GetString(1),
            CategoryLabel = reader.GetString(2),
            OriginalText = reader.GetString(3),
            RecognizedText = reader.GetString(4),
            RecognitionStatus = Enum.Parse<NoteRecognitionStatus>(reader.GetString(5), true),
            ErrorMessage = reader.GetString(6),
            AudioPath = reader.GetString(7),
            PhotoPaths = JsonSerializer.Deserialize<List<string>>(reader.GetString(8)) ?? [],
            CreatedAt = DateTimeOffset.Parse(reader.GetString(9)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(10))
        };
    }
}
