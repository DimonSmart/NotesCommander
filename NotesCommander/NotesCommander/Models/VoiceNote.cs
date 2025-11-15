using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NotesCommander.Models;

public enum VoiceNoteRecognitionStatus
{
        Pending,
        Processing,
        Completed,
        Failed
}

public class VoiceNote
{
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; }
                = TimeSpan.Zero;

        public VoiceNoteRecognitionStatus RecognitionStatus { get; set; }
                = VoiceNoteRecognitionStatus.Pending;

        public string CategoryLabel { get; set; } = "Входящие";

        public string? AudioFilePath { get; set; }
                = string.Empty;

        public List<string> Photos { get; set; } = [];

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string DurationDisplay => Duration == TimeSpan.Zero
                ? "00:00"
                : Duration.ToString("mm\\:ss");

        public string RecognitionStatusDisplay => RecognitionStatus switch
        {
                VoiceNoteRecognitionStatus.Completed => "Расшифровка готова",
                VoiceNoteRecognitionStatus.Processing => "Обработка",
                VoiceNoteRecognitionStatus.Failed => "Ошибка распознавания",
                _ => "Ожидает очереди"
        };

        public string? PhotoPreviewPath => Photos.FirstOrDefault();

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
}
