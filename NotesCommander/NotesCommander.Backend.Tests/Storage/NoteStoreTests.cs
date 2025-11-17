using Microsoft.Extensions.Options;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.Tests.Storage;

public sealed class NoteStoreTests
{
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
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
