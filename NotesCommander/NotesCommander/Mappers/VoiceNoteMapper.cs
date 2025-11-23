using NotesCommander.Data.Entities;
using NotesCommander.Domain;
using NotesCommander.Models;
using Riok.Mapperly.Abstractions;

namespace NotesCommander.Mappers;

[Mapper]
public static partial class VoiceNoteMapper
{
    public static partial VoiceNoteViewModel ToViewModel(this VoiceNote source);

    public static partial VoiceNote ToDomain(this VoiceNoteViewModel source);

    [MapProperty(nameof(VoiceNote.Duration), nameof(VoiceNoteEntity.DurationTicks))]
    public static partial VoiceNoteEntity ToEntity(this VoiceNote source);

    [MapProperty(nameof(VoiceNoteEntity.DurationTicks), nameof(VoiceNote.Duration))]
    public static partial VoiceNote ToDomain(this VoiceNoteEntity entity);

    public static VoiceNote ToDomain(this VoiceNoteEntity entity, List<VoiceNotePhoto> photos, List<VoiceNoteTag> tags)
    {
        var note = entity.ToDomain();
        note.Photos = photos;
        note.Tags = tags;
        return note;
    }

    private static long MapDurationToTicks(TimeSpan duration)
        => duration.Ticks;

    private static TimeSpan MapTicksToDuration(long ticks)
        => TimeSpan.FromTicks(ticks);
}
