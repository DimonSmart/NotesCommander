using NotesCommander.Data;
using NotesCommander.Models;

namespace NotesCommander;

public static class TestDbHelper
{
    public static async Task TestDatabase()
    {
        try
        {
            var repo = new VoiceNoteRepository(null!); // Для теста без логгера
            
            System.Diagnostics.Debug.WriteLine($"[TestDb] Database path: {Constants.DatabasePath}");
            System.Diagnostics.Debug.WriteLine($"[TestDb] AppDataDirectory: {FileSystem.AppDataDirectory}");
            
            var notes = await repo.ListAsync();
            System.Diagnostics.Debug.WriteLine($"[TestDb] Found {notes.Count} notes in database");
            
            foreach (var note in notes)
            {
                System.Diagnostics.Debug.WriteLine($"[TestDb]   - {note.Title} (Created: {note.CreatedAt})");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TestDb] ERROR: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
