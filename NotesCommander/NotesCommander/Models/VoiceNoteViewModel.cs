using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NotesCommander.Domain;

namespace NotesCommander.Models;

public partial class VoiceNoteViewModel : ObservableObject
{
        private bool isPlaying;

        public int LocalId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string AudioFilePath { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        public string? OriginalText { get; set; }
                = string.Empty;

        public string? RecognizedText { get; set; }
                = string.Empty;

        public string CategoryLabel { get; set; } = "Входящие";

        public VoiceNoteRecognitionStatus RecognitionStatus { get; set; }
                = VoiceNoteRecognitionStatus.InQueue;

        public VoiceNoteSyncStatus SyncStatus { get; set; }
                = VoiceNoteSyncStatus.LocalOnly;

        public string? ServerId { get; set; }
                = string.Empty;

        public List<VoiceNotePhoto> Photos { get; set; } = [];

        public List<VoiceNoteTag> Tags { get; set; } = [];

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string DurationDisplay => Duration == TimeSpan.Zero
                ? "00:00"
                : Duration.ToString("mm\\:ss");

        public string RecognitionStatusDisplay => RecognitionStatus switch
        {
                VoiceNoteRecognitionStatus.Ready => "Готово",
                VoiceNoteRecognitionStatus.Recognizing => "Распознаётся",
                VoiceNoteRecognitionStatus.Error => "Ошибка",
                _ => "В очереди"
        };

        public string? PhotoPreviewPath => Photos.FirstOrDefault()?.FilePath;

        public bool HasPhotoPreview => !string.IsNullOrEmpty(PhotoPreviewPath);

        public string PhotoPreviewFallbackGlyph
        {
                get
                {
                        var normalizedCategory = (CategoryLabel ?? string.Empty).Trim().ToLowerInvariant();
                        return normalizedCategory switch
                        {
                                "работа" => "💼",
                                "личное" => "🏡",
                                "интервью" => "🎤",
                                _ => "📝"
                        };
                }
        }

        public bool IsPlaying
        {
                get => isPlaying;
                set => SetProperty(ref isPlaying, value);
        }

        public static VoiceNoteViewModel FromDomain(VoiceNote note)
        {
                return new VoiceNoteViewModel
                {
                        LocalId = note.LocalId,
                        Title = note.Title,
                        AudioFilePath = note.AudioFilePath,
                        Duration = note.Duration,
                        OriginalText = note.OriginalText,
                        RecognizedText = note.RecognizedText,
                        CategoryLabel = note.CategoryLabel,
                        RecognitionStatus = note.RecognitionStatus,
                        SyncStatus = note.SyncStatus,
                        ServerId = note.ServerId,
                        Photos = note.Photos.ToList(),
                        Tags = note.Tags.ToList(),
                        CreatedAt = note.CreatedAt,
                        UpdatedAt = note.UpdatedAt
                };
        }

        public VoiceNote ToDomain()
        {
                return new VoiceNote
                {
                        LocalId = LocalId,
                        Title = Title,
                        AudioFilePath = AudioFilePath,
                        Duration = Duration,
                        OriginalText = OriginalText,
                        RecognizedText = RecognizedText,
                        CategoryLabel = CategoryLabel,
                        RecognitionStatus = RecognitionStatus,
                        SyncStatus = SyncStatus,
                        ServerId = ServerId,
                        Photos = Photos.ToList(),
                        Tags = Tags.ToList(),
                        CreatedAt = CreatedAt,
                        UpdatedAt = UpdatedAt
                };
        }
}
