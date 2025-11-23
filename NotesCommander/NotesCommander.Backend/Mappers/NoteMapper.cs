using NotesCommander.Backend.Models;
using Riok.Mapperly.Abstractions;

namespace NotesCommander.Backend.Mappers;

[Mapper]
public static partial class NoteMapper
{
    public static partial NoteResponse ToResponse(this NoteRecord record);
}
