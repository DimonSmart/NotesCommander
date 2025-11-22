using Microsoft.Extensions.Options;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.UnitTests;

public class SmokeTests
{
        [Fact]
        public async Task NoteStore_CanCreateAndRead()
        {
                var dbPath = Path.Combine(Path.GetTempPath(), $"nc-unittests-{Guid.NewGuid():N}.db");
                var options = Options.Create(new NoteStorageOptions { DatabasePath = dbPath });
                var store = new NoteStore(options);

                var note = new NoteRecord
                {
                        Title = "Test note",
                        CategoryLabel = "Test",
                        AudioPath = "test.wav"
                };

                var created = await store.CreateAsync(note, CancellationToken.None);
                var fetched = await store.GetAsync(created.Id, CancellationToken.None);

                Assert.NotNull(fetched);
                Assert.Equal(created.Id, fetched!.Id);
                Assert.Equal("Test note", fetched.Title);
        }
}
