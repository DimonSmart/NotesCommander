namespace NotesCommander.Backend.Models;

public enum NoteRecognitionStatus
{
    Uploaded,
    Queued,
    Recognizing,
    Completed,
    Failed
}
