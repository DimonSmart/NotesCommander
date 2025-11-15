using System.IO;

namespace NotesCommander.Backend.Storage;

public sealed class NoteStorageOptions
{
    public string DatabasePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "notes.db");

    public string MediaDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "media");
}
