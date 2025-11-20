using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using NotesCommander.Models;

namespace NotesCommander.Data;

public class SeedDataService
{
        private readonly VoiceNoteRepository _voiceNoteRepository;
        private readonly TagRepository _tagRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly ILogger<SeedDataService> _logger;

        public SeedDataService(VoiceNoteRepository voiceNoteRepository, TagRepository tagRepository, CategoryRepository categoryRepository, ILogger<SeedDataService> logger)
        {
                _voiceNoteRepository = voiceNoteRepository;
                _tagRepository = tagRepository;
                _categoryRepository = categoryRepository;
                _logger = logger;
        }

        public async Task<(string AudioPath, string PhotoPath)> EnsureSeedAssetsAsync()
        {
                var audioPath = await EnsureSeedFileAsync("SeedFiles/audio/demo-note.wav", "demo-note.wav");
                var photoPath = await EnsureSeedFileAsync("SeedFiles/photos/demo-photo.png", "demo-photo.png");
                return (audioPath, photoPath);
        }

        public async Task LoadSeedDataAsync()
        {
                _logger.LogInformation("Starting seed data loading...");
                await ClearTablesAsync();

                // Создаём категории
                var categories = new[]
                {
                        new Category { Title = "Работа", Color = "#3068df" },
                        new Category { Title = "Личное", Color = "#f97316" },
                        new Category { Title = "Идеи", Color = "#10b981" }
                };

                foreach (var category in categories)
                {
                        await _categoryRepository.SaveItemAsync(category);
                }
                _logger.LogInformation("Categories created: {Count}", categories.Length);

                // Создаём теги
                var tags = new[]
                {
                        new Tag { Title = "демо", Color = "#9333ea" },
                        new Tag { Title = "история", Color = "#14b8a6" }
                };

                foreach (var tag in tags)
                {
                        await _tagRepository.SaveItemAsync(tag);
                }
                _logger.LogInformation("Tags created: {Count}", tags.Length);

                // Создаём демонстрационную заметку
                var (audioPath, photoPath) = await EnsureSeedAssetsAsync();

                _logger.LogInformation("Audio file path: {AudioPath}", audioPath);
                _logger.LogInformation("Photo file path: {PhotoPath}", photoPath);

                var demoNote = new VoiceNote
                {
                        Title = "Добро пожаловать в NotesCommander",
                        AudioFilePath = audioPath,
                        Duration = TimeSpan.FromSeconds(11),
                        OriginalText = "Это демонстрационная заметка для знакомства с приложением.",
                        RecognizedText = "And so my fellow Americans ask not what your country can do for you ask what you can do for your country.",
                        CategoryLabel = "Идеи",
                        RecognitionStatus = VoiceNoteRecognitionStatus.Ready,
                        SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Photos = new List<VoiceNotePhoto>
                        {
                                new VoiceNotePhoto
                                {
                                        FilePath = photoPath,
                                        CreatedAt = DateTime.UtcNow
                                }
                        },
                        Tags = new List<VoiceNoteTag>
                        {
                                new VoiceNoteTag { Value = "демо" },
                                new VoiceNoteTag { Value = "история" }
                        }
                };

                await _voiceNoteRepository.SaveAsync(demoNote);
                _logger.LogInformation("Demo note created successfully with ID: {Id}", demoNote.LocalId);
        }

        private async Task ClearTablesAsync()
        {
                await Task.WhenAll(
                        _voiceNoteRepository.DropTablesAsync(),
                        _tagRepository.DropTableAsync(),
                        _categoryRepository.DropTableAsync());
                _logger.LogInformation("Tables cleared successfully");
        }

        private async Task<string> EnsureSeedFileAsync(string fileName, string? destinationFileName = null)
        {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                        return string.Empty;
                }

                var sanitizedName = (destinationFileName ?? fileName)
                        .Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar);

                var destination = Path.Combine(FileSystem.AppDataDirectory, sanitizedName);
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory))
                {
                        Directory.CreateDirectory(directory);
                }

                try
                {
                        var destinationInfo = new FileInfo(destination);
                        if (!destinationInfo.Exists || destinationInfo.Length == 0)
                        {
                                await using var sourceStream = await FileSystem.OpenAppPackageFileAsync(fileName);
                                await using var destinationStream = File.Create(destination);
                                await sourceStream.CopyToAsync(destinationStream);
                                destinationInfo.Refresh();
                                _logger.LogInformation("Copied seed file from {Source} to {Destination}", fileName, destination);
                        }
                        else
                        {
                                _logger.LogDebug("Seed file already exists at {Destination} (size={Size})", destination, destinationInfo.Length);
                        }
                }
                catch (Exception ex)
                {
                        _logger.LogWarning(ex, "Could not copy seed file {FileName}, file may not exist in Resources", fileName);
                        // If the asset is missing in the package we skip seeding but keep the app running.
                        return string.Empty;
                }

                return destination;
        }
}
