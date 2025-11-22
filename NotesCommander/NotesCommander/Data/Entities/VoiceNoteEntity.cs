using NotesCommander.Domain;

namespace NotesCommander.Data.Entities;

public class VoiceNoteEntity
{
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AudioFilePath { get; set; } = string.Empty;
        public long DurationTicks { get; set; }
        public string? OriginalText { get; set; }
        public string? RecognizedText { get; set; }
        public string CategoryLabel { get; set; } = "Входящие";
        public VoiceNoteSyncStatus SyncStatus { get; set; }
        public string? ServerId { get; set; }
        public VoiceNoteRecognitionStatus RecognitionStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
}

public class VoiceNotePhotoEntity
{
        public int Id { get; set; }
        public int VoiceNoteId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
}

public class VoiceNoteTagEntity
{
        public int Id { get; set; }
        public int VoiceNoteId { get; set; }
        public string Value { get; set; } = string.Empty;
}
