using System.Collections.Generic;
using System.Linq;

namespace NotesCommander.Models;

public enum VoiceNoteRecognitionStatus
{
        InQueue = 0,
        Recognizing = 1,
        Ready = 2,
        Error = 3
}

public enum VoiceNoteSyncStatus
{
        LocalOnly,
        Uploading,
        Synced,
        Failed
}

public class VoiceNote
{
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
}

public class VoiceNotePhoto
{
        public int Id { get; set; }

        public int VoiceNoteId { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class VoiceNoteTag
{
        public int Id { get; set; }

        public int VoiceNoteId { get; set; }

        public string Value { get; set; } = string.Empty;
}

public class VoiceNoteSeed
{
        public string Title { get; set; } = string.Empty;

        public string AudioFile { get; set; } = string.Empty;

        public double DurationSeconds { get; set; }
                = 0;

        public string? OriginalText { get; set; }
                = string.Empty;

        public string? RecognizedText { get; set; }
                = string.Empty;

        public string CategoryLabel { get; set; } = "Входящие";

        public VoiceNoteRecognitionStatus RecognitionStatus { get; set; }
                = VoiceNoteRecognitionStatus.InQueue;

        public List<string> Photos { get; set; } = [];

        public List<string> Tags { get; set; } = [];
}

public class VoiceNotesSeedPayload
{
        public List<Category> Categories { get; set; } = [];

        public List<Tag> Tags { get; set; } = [];

        public List<VoiceNoteSeed> Notes { get; set; } = [];
}
