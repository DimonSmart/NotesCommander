using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using NotesCommander.Models;

namespace NotesCommander.Data;

public class SeedDataService
{
        private readonly VoiceNoteRepository _voiceNoteRepository;
        private readonly TagRepository _tagRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly string _seedDataFilePath = "SeedData.json";
        private readonly ILogger<SeedDataService> _logger;

        public SeedDataService(VoiceNoteRepository voiceNoteRepository, TagRepository tagRepository, CategoryRepository categoryRepository, ILogger<SeedDataService> logger)
        {
                _voiceNoteRepository = voiceNoteRepository;
                _tagRepository = tagRepository;
                _categoryRepository = categoryRepository;
                _logger = logger;
        }

        public async Task LoadSeedDataAsync()
        {
                await ClearTablesAsync();

                await using Stream templateStream = await FileSystem.OpenAppPackageFileAsync(_seedDataFilePath);

                VoiceNotesSeedPayload? payload = null;
                try
                {
                        payload = JsonSerializer.Deserialize(templateStream, JsonContext.Default.VoiceNotesSeedPayload);
                }
                catch (Exception e)
                {
                        _logger.LogError(e, "Error deserializing seed data");
                }

                if (payload is null)
                {
                        return;
                }

                try
                {
                        foreach (var category in payload.Categories)
                        {
                                await _categoryRepository.SaveItemAsync(category);
                        }

                        foreach (var tag in payload.Tags)
                        {
                                await _tagRepository.SaveItemAsync(tag);
                        }

                        foreach (var seed in payload.Notes)
                        {
                                var note = new VoiceNote
                                {
                                        Title = string.IsNullOrWhiteSpace(seed.Title)
                                                ? $"Заметка {DateTime.Now:HH:mm}"
                                                : seed.Title,
                                        AudioFilePath = EnsureSeedFile(seed.AudioFile),
                                        Duration = TimeSpan.FromSeconds(seed.DurationSeconds),
                                        OriginalText = seed.OriginalText,
                                        RecognizedText = seed.RecognizedText,
                                        CategoryLabel = seed.CategoryLabel,
                                        RecognitionStatus = seed.RecognitionStatus,
                                        SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                                        CreatedAt = DateTime.UtcNow,
                                        UpdatedAt = DateTime.UtcNow,
                                        Photos = seed.Photos
                                                .Select(photo => new VoiceNotePhoto
                                                {
                                                        FilePath = EnsureSeedFile(photo),
                                                        CreatedAt = DateTime.UtcNow
                                                })
                                                .ToList(),
                                        Tags = seed.Tags
                                                .Select(tag => new VoiceNoteTag { Value = tag })
                                                .ToList()
                                };

                                await _voiceNoteRepository.SaveAsync(note);
                        }
                }
                catch (Exception e)
                {
                        _logger.LogError(e, "Error saving seed data");
                        throw;
                }
        }

        private async Task ClearTablesAsync()
        {
                try
                {
                        await Task.WhenAll(
                                _voiceNoteRepository.DropTablesAsync(),
                                _tagRepository.DropTableAsync(),
                                _categoryRepository.DropTableAsync());
                }
                catch (Exception e)
                {
                        _logger.LogError(e, "Error clearing tables");
                }
        }

        private static string EnsureSeedFile(string fileName)
        {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                        return string.Empty;
                }

                var sanitizedName = fileName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                var destination = Path.Combine(FileSystem.AppDataDirectory, sanitizedName);
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory))
                {
                        Directory.CreateDirectory(directory);
                }

                if (!File.Exists(destination))
                {
                        File.WriteAllBytes(destination, Array.Empty<byte>());
                }

                return destination;
        }
}
