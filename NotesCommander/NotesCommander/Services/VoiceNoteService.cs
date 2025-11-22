using NotesCommander.Data;
using NotesCommander.Domain;

namespace NotesCommander.Services;

public class VoiceNoteService : IVoiceNoteService
{
        private readonly VoiceNoteRepository _repository;

        public VoiceNoteService(VoiceNoteRepository repository)
        {
                _repository = repository;
        }

        public async Task<IReadOnlyList<VoiceNote>> GetNotesAsync(CancellationToken cancellationToken = default)
                => await _repository.ListAsync(cancellationToken);

        public async Task<VoiceNote> SaveAsync(VoiceNote note, CancellationToken cancellationToken = default)
        {
                await _repository.SaveAsync(note, cancellationToken);
                return note;
        }

        public Task<VoiceNote?> GetAsync(int id, CancellationToken cancellationToken = default)
                => _repository.GetAsync(id, cancellationToken);
}
