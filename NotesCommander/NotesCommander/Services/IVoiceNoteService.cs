using System.Collections.Generic;
using NotesCommander.Domain;

namespace NotesCommander.Services;

public interface IVoiceNoteService
{
        Task<IReadOnlyList<VoiceNote>> GetNotesAsync(CancellationToken cancellationToken = default);

        Task<VoiceNote> SaveAsync(VoiceNote note, CancellationToken cancellationToken = default);

        Task<VoiceNote?> GetAsync(int id, CancellationToken cancellationToken = default);
}
