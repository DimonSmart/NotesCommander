namespace NotesCommander.Backend.Models;

public sealed class NoteRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string CategoryLabel { get; set; } = "Входящие";

    public string? OriginalText { get; set; }
        = string.Empty;

    public string? RecognizedText { get; set; }
        = string.Empty;

    public string? AudioPath { get; set; }
        = string.Empty;

    public List<string> PhotoPaths { get; set; }
        = [];

    public NoteRecognitionStatus RecognitionStatus { get; set; }
        = NoteRecognitionStatus.Uploaded;

    public string? ErrorMessage { get; set; }
        = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}
