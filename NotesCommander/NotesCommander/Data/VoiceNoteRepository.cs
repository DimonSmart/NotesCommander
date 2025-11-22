using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using NotesCommander.Data.Entities;
using NotesCommander.Domain;

namespace NotesCommander.Data;

public class VoiceNoteRepository
{
        private readonly ILogger<VoiceNoteRepository> _logger;
        private bool _initialized;

        public VoiceNoteRepository(ILogger<VoiceNoteRepository> logger)
        {
                _logger = logger;
        }

        public async Task<List<VoiceNote>> ListAsync(CancellationToken cancellationToken = default)
        {
                await EnsureInitializedAsync(cancellationToken);
                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync(cancellationToken);

                var noteEntities = new List<VoiceNoteEntity>();
                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"SELECT Id, Title, AudioFilePath, DurationTicks, OriginalText, RecognizedText, CategoryLabel,
SyncStatus, ServerId, RecognitionStatus, CreatedAt, UpdatedAt FROM VoiceNote ORDER BY datetime(CreatedAt) DESC";

                await using (var reader = await selectCmd.ExecuteReaderAsync(cancellationToken))
                {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                                noteEntities.Add(MapVoiceNoteEntity(reader));
                        }
                }

                if (noteEntities.Count == 0)
                {
                        return [];
                }

                var ids = noteEntities.Select(n => n.Id).ToList();
                var photosLookup = await LoadPhotosAsync(connection, cancellationToken, ids);
                var tagsLookup = await LoadTagsAsync(connection, cancellationToken, ids);

                var notes = noteEntities
                        .Select(entity => MapToDomain(
                                entity,
                                photosLookup.GetValueOrDefault(entity.Id, []),
                                tagsLookup.GetValueOrDefault(entity.Id, [])))
                        .ToList();

                return notes;
        }

        public async Task<VoiceNote?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
                await EnsureInitializedAsync(cancellationToken);
                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync(cancellationToken);

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"SELECT Id, Title, AudioFilePath, DurationTicks, OriginalText, RecognizedText, CategoryLabel,
SyncStatus, ServerId, RecognitionStatus, CreatedAt, UpdatedAt FROM VoiceNote WHERE Id = @id";
                selectCmd.Parameters.AddWithValue("@id", id);

                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                        return null;
                }

                var entity = MapVoiceNoteEntity(reader);

                var singleId = new List<int> { entity.Id };
                var photos = (await LoadPhotosAsync(connection, cancellationToken, singleId))
                        .GetValueOrDefault(entity.Id, []);
                var tags = (await LoadTagsAsync(connection, cancellationToken, singleId))
                        .GetValueOrDefault(entity.Id, []);

                return MapToDomain(entity, photos, tags);
        }

        public async Task<int> SaveAsync(VoiceNote note, CancellationToken cancellationToken = default)
        {
                await EnsureInitializedAsync(cancellationToken);
                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync(cancellationToken);
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

                note.AudioFilePath = EnsureMediaPath(note.AudioFilePath);
                foreach (var photo in note.Photos)
                {
                        photo.FilePath = EnsureMediaPath(photo.FilePath);
                }

                note.UpdatedAt = DateTime.UtcNow;
                var entity = MapToEntity(note);

                var upsert = connection.CreateCommand();
                upsert.Transaction = transaction;
                if (entity.Id == 0)
                {
                        upsert.CommandText = @"INSERT INTO VoiceNote (Title, AudioFilePath, DurationTicks, OriginalText, RecognizedText,
CategoryLabel, SyncStatus, ServerId, RecognitionStatus, CreatedAt, UpdatedAt)
VALUES (@title, @audio, @duration, @original, @recognized, @category, @sync, @server, @recognition, @createdAt, @updatedAt);
SELECT last_insert_rowid();";
                }
                else
                {
                        upsert.CommandText = @"UPDATE VoiceNote SET Title = @title, AudioFilePath = @audio, DurationTicks = @duration,
OriginalText = @original, RecognizedText = @recognized, CategoryLabel = @category, SyncStatus = @sync, ServerId = @server,
RecognitionStatus = @recognition, CreatedAt = @createdAt, UpdatedAt = @updatedAt WHERE Id = @id; SELECT @id;";
                        upsert.Parameters.AddWithValue("@id", entity.Id);
                }

                upsert.Parameters.AddWithValue("@title", entity.Title);
                upsert.Parameters.AddWithValue("@audio", entity.AudioFilePath);
                upsert.Parameters.AddWithValue("@duration", entity.DurationTicks);
                upsert.Parameters.AddWithValue("@original", entity.OriginalText ?? (object)DBNull.Value);
                upsert.Parameters.AddWithValue("@recognized", entity.RecognizedText ?? (object)DBNull.Value);
                upsert.Parameters.AddWithValue("@category", entity.CategoryLabel);
                upsert.Parameters.AddWithValue("@sync", (int)entity.SyncStatus);
                upsert.Parameters.AddWithValue("@server", entity.ServerId ?? (object)DBNull.Value);
                upsert.Parameters.AddWithValue("@recognition", (int)entity.RecognitionStatus);
                upsert.Parameters.AddWithValue("@createdAt", entity.CreatedAt.ToString("O"));
                upsert.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt.ToString("O"));

                var insertedIdObj = await upsert.ExecuteScalarAsync(cancellationToken);
                if (entity.Id == 0)
                {
                        entity.Id = Convert.ToInt32(insertedIdObj);
                        note.LocalId = entity.Id;
                }

                await ReplacePhotosAsync(connection, transaction, note, cancellationToken);
                await ReplaceTagsAsync(connection, transaction, note, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return note.LocalId;
        }

        public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
                await EnsureInitializedAsync(cancellationToken);
                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync(cancellationToken);

                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM VoiceNote WHERE Id = @id";
                deleteCmd.Parameters.AddWithValue("@id", id);
                return await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task UpdateSyncStatusAsync(int id, VoiceNoteSyncStatus status, string? serverId, CancellationToken cancellationToken = default)
        {
                await EnsureInitializedAsync(cancellationToken);
                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync(cancellationToken);

                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = "UPDATE VoiceNote SET SyncStatus = @status, ServerId = @serverId, UpdatedAt = @updated WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@status", (int)status);
                updateCmd.Parameters.AddWithValue("@serverId", serverId ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("O"));
                updateCmd.Parameters.AddWithValue("@id", id);

                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task DropTablesAsync()
        {
                await EnsureInitializedAsync();
                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync();

                var dropCmd = connection.CreateCommand();
                dropCmd.CommandText = "DELETE FROM VoiceNote";
                await dropCmd.ExecuteNonQueryAsync();
                dropCmd.CommandText = "DELETE FROM VoiceNotePhoto";
                await dropCmd.ExecuteNonQueryAsync();
                dropCmd.CommandText = "DELETE FROM VoiceNoteTag";
                await dropCmd.ExecuteNonQueryAsync();
        }

        private static VoiceNoteEntity MapVoiceNoteEntity(SqliteDataReader reader)
        {
                return new VoiceNoteEntity
                {
                        Id = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        AudioFilePath = reader.GetString(2),
                        DurationTicks = reader.GetInt64(3),
                        OriginalText = reader.IsDBNull(4) ? null : reader.GetString(4),
                        RecognizedText = reader.IsDBNull(5) ? null : reader.GetString(5),
                        CategoryLabel = reader.GetString(6),
                        SyncStatus = (VoiceNoteSyncStatus)reader.GetInt32(7),
                        ServerId = reader.IsDBNull(8) ? null : reader.GetString(8),
                        RecognitionStatus = (VoiceNoteRecognitionStatus)reader.GetInt32(9),
                        CreatedAt = DateTime.Parse(reader.GetString(10)),
                        UpdatedAt = DateTime.Parse(reader.GetString(11))
                };
        }

        private static VoiceNote MapToDomain(VoiceNoteEntity entity, List<VoiceNotePhoto> photos, List<VoiceNoteTag> tags)
        {
                return new VoiceNote
                {
                        LocalId = entity.Id,
                        Title = entity.Title,
                        AudioFilePath = entity.AudioFilePath,
                        Duration = TimeSpan.FromTicks(entity.DurationTicks),
                        OriginalText = entity.OriginalText,
                        RecognizedText = entity.RecognizedText,
                        CategoryLabel = entity.CategoryLabel,
                        SyncStatus = entity.SyncStatus,
                        ServerId = entity.ServerId,
                        RecognitionStatus = entity.RecognitionStatus,
                        CreatedAt = entity.CreatedAt,
                        UpdatedAt = entity.UpdatedAt,
                        Photos = photos,
                        Tags = tags
                };
        }

        private static VoiceNoteEntity MapToEntity(VoiceNote note)
        {
                return new VoiceNoteEntity
                {
                        Id = note.LocalId,
                        Title = note.Title,
                        AudioFilePath = note.AudioFilePath,
                        DurationTicks = note.Duration.Ticks,
                        OriginalText = note.OriginalText,
                        RecognizedText = note.RecognizedText,
                        CategoryLabel = note.CategoryLabel,
                        SyncStatus = note.SyncStatus,
                        ServerId = note.ServerId,
                        RecognitionStatus = note.RecognitionStatus,
                        CreatedAt = note.CreatedAt,
                        UpdatedAt = note.UpdatedAt
                };
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
                if (_initialized)
                {
                        return;
                }

                Directory.CreateDirectory(FileSystem.AppDataDirectory);

                await using var connection = new SqliteConnection(Constants.DatabasePath);
                await connection.OpenAsync(cancellationToken);

                var command = connection.CreateCommand();
                command.CommandText = @"CREATE TABLE IF NOT EXISTS VoiceNote (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                AudioFilePath TEXT NOT NULL,
                DurationTicks INTEGER NOT NULL,
                OriginalText TEXT,
                RecognizedText TEXT,
                CategoryLabel TEXT NOT NULL,
                SyncStatus INTEGER NOT NULL,
                ServerId TEXT,
                RecognitionStatus INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
        );";
                await command.ExecuteNonQueryAsync(cancellationToken);

                command.CommandText = @"CREATE TABLE IF NOT EXISTS VoiceNotePhoto (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VoiceNoteId INTEGER NOT NULL,
                FilePath TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
        );";
                await command.ExecuteNonQueryAsync(cancellationToken);

                command.CommandText = @"CREATE TABLE IF NOT EXISTS VoiceNoteTag (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VoiceNoteId INTEGER NOT NULL,
                Value TEXT NOT NULL
        );";
                await command.ExecuteNonQueryAsync(cancellationToken);

                _initialized = true;
        }

        private static async Task<Dictionary<int, List<VoiceNotePhoto>>> LoadPhotosAsync(SqliteConnection connection, CancellationToken cancellationToken, IReadOnlyCollection<int>? filter)
        {
                var result = new Dictionary<int, List<VoiceNotePhoto>>();
                var cmd = connection.CreateCommand();
                if (filter is not null && filter.Count > 0)
                {
                        var filterList = filter.ToList();
                        cmd.CommandText = $"SELECT Id, VoiceNoteId, FilePath, CreatedAt FROM VoiceNotePhoto WHERE VoiceNoteId IN ({string.Join(",", filterList.Select((_, index) => $"@photoId{index}"))})";
                        for (var i = 0; i < filterList.Count; i++)
                        {
                                cmd.Parameters.AddWithValue($"@photoId{i}", filterList[i]);
                        }
                }
                else
                {
                        cmd.CommandText = "SELECT Id, VoiceNoteId, FilePath, CreatedAt FROM VoiceNotePhoto";
                }

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                        var photo = new VoiceNotePhoto
                        {
                                Id = reader.GetInt32(0),
                                VoiceNoteId = reader.GetInt32(1),
                                FilePath = reader.GetString(2),
                                CreatedAt = DateTime.Parse(reader.GetString(3))
                        };

                        if (!result.TryGetValue(photo.VoiceNoteId, out var list))
                        {
                                list = [];
                                result[photo.VoiceNoteId] = list;
                        }

                        list.Add(photo);
                }

                return result;
        }

        private static async Task<Dictionary<int, List<VoiceNoteTag>>> LoadTagsAsync(SqliteConnection connection, CancellationToken cancellationToken, IReadOnlyCollection<int>? filter)
        {
                var result = new Dictionary<int, List<VoiceNoteTag>>();
                var cmd = connection.CreateCommand();
                if (filter is not null && filter.Count > 0)
                {
                        var filterList = filter.ToList();
                        cmd.CommandText = $"SELECT Id, VoiceNoteId, Value FROM VoiceNoteTag WHERE VoiceNoteId IN ({string.Join(",", filterList.Select((_, index) => $"@tagId{index}"))})";
                        for (var i = 0; i < filterList.Count; i++)
                        {
                                cmd.Parameters.AddWithValue($"@tagId{i}", filterList[i]);
                        }
                }
                else
                {
                        cmd.CommandText = "SELECT Id, VoiceNoteId, Value FROM VoiceNoteTag";
                }

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                        var tag = new VoiceNoteTag
                        {
                                Id = reader.GetInt32(0),
                                VoiceNoteId = reader.GetInt32(1),
                                Value = reader.GetString(2)
                        };

                        if (!result.TryGetValue(tag.VoiceNoteId, out var list))
                        {
                                list = [];
                                result[tag.VoiceNoteId] = list;
                        }

                        list.Add(tag);
                }

                return result;
        }

        private static async Task ReplacePhotosAsync(SqliteConnection connection, SqliteTransaction transaction, VoiceNote note, CancellationToken cancellationToken)
        {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM VoiceNotePhoto WHERE VoiceNoteId = @voiceNoteId";
                deleteCmd.Parameters.AddWithValue("@voiceNoteId", note.LocalId);
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

                foreach (var photo in note.Photos)
                {
                        var insertCmd = connection.CreateCommand();
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = "INSERT INTO VoiceNotePhoto (VoiceNoteId, FilePath, CreatedAt) VALUES (@voiceNoteId, @path, @createdAt)";
                        insertCmd.Parameters.AddWithValue("@voiceNoteId", note.LocalId);
                        insertCmd.Parameters.AddWithValue("@path", photo.FilePath);
                        insertCmd.Parameters.AddWithValue("@createdAt", photo.CreatedAt.ToString("O"));
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                }
        }

        private static async Task ReplaceTagsAsync(SqliteConnection connection, SqliteTransaction transaction, VoiceNote note, CancellationToken cancellationToken)
        {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM VoiceNoteTag WHERE VoiceNoteId = @voiceNoteId";
                deleteCmd.Parameters.AddWithValue("@voiceNoteId", note.LocalId);
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

                foreach (var tag in note.Tags)
                {
                                var insertCmd = connection.CreateCommand();
                                insertCmd.Transaction = transaction;
                                insertCmd.CommandText = "INSERT INTO VoiceNoteTag (VoiceNoteId, Value) VALUES (@voiceNoteId, @value)";
                                insertCmd.Parameters.AddWithValue("@voiceNoteId", note.LocalId);
                                insertCmd.Parameters.AddWithValue("@value", tag.Value);
                                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                }
        }

        private static string EnsureMediaPath(string? sourcePath)
        {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                        return string.Empty;
                }

                var appDataDirectory = FileSystem.AppDataDirectory;
                var fileName = Path.GetFileName(sourcePath);
                var destinationPath = Path.Combine(appDataDirectory, fileName);
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                        Directory.CreateDirectory(directory);
                }

                try
                {
                        if (File.Exists(sourcePath) && !string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                        {
                                File.Copy(sourcePath, destinationPath, true);
                        }
                }
                catch (Exception ex)
                {
                        System.Diagnostics.Debug.WriteLine($"Failed to copy media file: {ex.Message}");
                }

                return destinationPath;
        }
}
