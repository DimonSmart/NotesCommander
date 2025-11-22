using Microsoft.Extensions.Options;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.FuncTests.Storage;

public sealed class NoteStoreTests
{
    public NoteStoreTests()
    {
        // Initialize SQLite provider for tests
        SQLitePCL.Batteries.Init();
    }

    [Fact]
    public async Task UpdateStatusAsync_PreservesCategory_WhenCategoryIsMissing()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"notes-store-tests-{Guid.NewGuid():N}.db");
        try
        {
            var store = new NoteStore(Options.Create(new NoteStorageOptions { DatabasePath = databasePath }));
            var created = await store.CreateAsync(new NoteRecord
            {
                Title = "Test",
                CategoryLabel = "Inbox",
                OriginalText = "original",
                RecognitionStatus = NoteRecognitionStatus.Uploaded
            }, CancellationToken.None);

            await store.UpdateStatusAsync(created.Id, NoteRecognitionStatus.Completed, "done", null, null, CancellationToken.None);

            var updated = await store.GetAsync(created.Id, CancellationToken.None);
            Assert.NotNull(updated);
            Assert.Equal(NoteRecognitionStatus.Completed, updated!.RecognitionStatus);
            Assert.Equal("Inbox", updated.CategoryLabel);
            Assert.Equal("done", updated.RecognizedText);
        }
        finally
        {
            // Give SQLite time to release the file handle
            await Task.Delay(100);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(databasePath))
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
