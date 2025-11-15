using System.Collections.Generic;
using NotesCommander.Models;

namespace NotesCommander.Services;

public interface IVoiceNoteService
{
        Task<IReadOnlyList<VoiceNote>> GetNotesAsync(CancellationToken cancellationToken = default);

        Task<VoiceNote> SaveAsync(VoiceNote note, CancellationToken cancellationToken = default);
}
