using System.Collections.Generic;
using System.Linq;
using NotesCommander.Models;

namespace NotesCommander.Services;

public class VoiceNoteService : IVoiceNoteService
{
        private readonly List<VoiceNote> _notes = [];

        public VoiceNoteService()
        {
                _notes.Add(new VoiceNote
                {
                        Title = "Интервью с клиентом",
                        Duration = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(23),
                        CategoryLabel = "Работа",
                        RecognitionStatus = VoiceNoteRecognitionStatus.Completed,
                        Photos = new List<string>(),
                        CreatedAt = DateTime.Now.AddHours(-3)
                });

                _notes.Add(new VoiceNote
                {
                        Title = "Идея для подкаста",
                        Duration = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(41),
                        CategoryLabel = "Личное",
                        RecognitionStatus = VoiceNoteRecognitionStatus.Processing,
                        Photos = new List<string>(),
                        CreatedAt = DateTime.Now.AddHours(-8)
                });
        }

        public Task<IReadOnlyList<VoiceNote>> GetNotesAsync(CancellationToken cancellationToken = default)
        {
                IReadOnlyList<VoiceNote> snapshot = _notes
                        .OrderByDescending(note => note.CreatedAt)
                        .Select(note => Clone(note))
                        .ToList();

                return Task.FromResult(snapshot);
        }

        public Task SaveAsync(VoiceNote note, CancellationToken cancellationToken = default)
        {
                var existingIndex = _notes.FindIndex(n => n.Id == note.Id);
                if (existingIndex >= 0)
                {
                        _notes[existingIndex] = Clone(note);
                }
                else
                {
                        _notes.Insert(0, Clone(note));
                }

                return Task.CompletedTask;
        }

        private static VoiceNote Clone(VoiceNote source)
                => new()
                {
                        Id = source.Id,
                        Title = source.Title,
                        Duration = source.Duration,
                        CategoryLabel = source.CategoryLabel,
                        RecognitionStatus = source.RecognitionStatus,
                        AudioFilePath = source.AudioFilePath,
                        Photos = source.Photos.ToList(),
                        CreatedAt = source.CreatedAt
                };
}
