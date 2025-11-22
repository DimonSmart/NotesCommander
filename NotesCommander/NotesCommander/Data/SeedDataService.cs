using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using NotesCommander.Domain;

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

                // Категории и теги
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

                // Несколько примерных записей
                var (audioPath, photoPath) = await EnsureSeedAssetsAsync();

                _logger.LogInformation("Audio file path: {AudioPath}", audioPath);
                _logger.LogInformation("Photo file path: {PhotoPath}", photoPath);

                var now = DateTime.UtcNow;
                var seedNotes = new[]
                {
                        new VoiceNote
                        {
                                Title = "Утренний стендап",
                                AudioFilePath = audioPath,
                                Duration = TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(20),
                                OriginalText = "Быстрый статус: закончил прототип, сегодня ревью и тесты.",
                                RecognizedText = "Быстрый статус: закончил прототип, сегодня ревью и тесты.",
                                CategoryLabel = "Работа",
                                RecognitionStatus = VoiceNoteRecognitionStatus.Ready,
                                SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                                CreatedAt = now.AddHours(-1),
                                UpdatedAt = now.AddHours(-1),
                                Photos = new List<VoiceNotePhoto>
                                {
                                        new VoiceNotePhoto { FilePath = photoPath, CreatedAt = now }
                                },
                                Tags = new List<VoiceNoteTag>
                                {
                                        new VoiceNoteTag { Value = "команда" },
                                        new VoiceNoteTag { Value = "ежедневка" }
                                }
                        },
                        new VoiceNote
                        {
                                Title = "Идея для заметки",
                                AudioFilePath = audioPath,
                                Duration = TimeSpan.FromSeconds(42),
                                OriginalText = "Напомнить про новый сценарий онбординга.",
                                RecognizedText = null,
                                CategoryLabel = "Личное",
                                RecognitionStatus = VoiceNoteRecognitionStatus.InQueue,
                                SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                                CreatedAt = now.AddHours(-6),
                                UpdatedAt = now.AddHours(-6),
                                Tags = new List<VoiceNoteTag> { new VoiceNoteTag { Value = "идея" } }
                        },
                        new VoiceNote
                        {
                                Title = "Интервью",
                                AudioFilePath = audioPath,
                                Duration = TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(5),
                                OriginalText = "Черновик вопросов для кандидата.",
                                RecognizedText = null,
                                CategoryLabel = "Работа",
                                RecognitionStatus = VoiceNoteRecognitionStatus.Recognizing,
                                SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                                CreatedAt = now.AddDays(-1).AddHours(-2),
                                UpdatedAt = now.AddDays(-1).AddHours(-2),
                                Tags = new List<VoiceNoteTag>
                                {
                                        new VoiceNoteTag { Value = "интервью" }
                                }
                        },
                        new VoiceNote
                        {
                                Title = "Вчерашний отчёт",
                                AudioFilePath = audioPath,
                                Duration = TimeSpan.FromMinutes(2),
                                OriginalText = "Итоги дня: закрыли задачу по импорту файлов.",
                                RecognizedText = "Итоги дня: закрыли задачу по импорту файлов.",
                                CategoryLabel = "Работа",
                                RecognitionStatus = VoiceNoteRecognitionStatus.Ready,
                                SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                                CreatedAt = now.AddDays(-1).AddHours(-5),
                                UpdatedAt = now.AddDays(-1).AddHours(-5),
                                Tags = new List<VoiceNoteTag> { new VoiceNoteTag { Value = "отчёт" } }
                        },
                        new VoiceNote
                        {
                                Title = "Напоминание купить подарки",
                                AudioFilePath = audioPath,
                                Duration = TimeSpan.FromSeconds(18),
                                OriginalText = "Список подарков на выходные.",
                                RecognizedText = null,
                                CategoryLabel = "Личное",
                                RecognitionStatus = VoiceNoteRecognitionStatus.Error,
                                SyncStatus = VoiceNoteSyncStatus.LocalOnly,
                                CreatedAt = now.AddDays(-3).AddHours(-1),
                                UpdatedAt = now.AddDays(-3).AddHours(-1),
                                Tags = new List<VoiceNoteTag> { new VoiceNoteTag { Value = "покупки" } }
                        }
                };

                foreach (var note in seedNotes)
                {
                        await _voiceNoteRepository.SaveAsync(note);
                        _logger.LogInformation("Seed note created: {Title} (Status={Status}, CreatedAt={CreatedAt})", note.Title, note.RecognitionStatus, note.CreatedAt);
                }
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
