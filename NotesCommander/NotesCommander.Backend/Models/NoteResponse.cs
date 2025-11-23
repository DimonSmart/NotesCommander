namespace NotesCommander.Backend.Models;

public sealed class NoteResponse
{
    public Guid Id { get; init; }
        = Guid.Empty;

    public string Title { get; init; } = string.Empty;

    public string CategoryLabel { get; init; } = string.Empty;

    public string? RecognizedText { get; init; }
        = string.Empty;

    public NoteRecognitionStatus RecognitionStatus { get; init; }
        = NoteRecognitionStatus.Uploaded;

    public string? ErrorMessage { get; init; }
        = string.Empty;

}
