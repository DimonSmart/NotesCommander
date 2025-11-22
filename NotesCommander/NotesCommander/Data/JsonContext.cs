using System.Text.Json.Serialization;
using NotesCommander.Domain;

[JsonSerializable(typeof(VoiceNote))]
[JsonSerializable(typeof(VoiceNotesSeedPayload))]
[JsonSerializable(typeof(VoiceNoteSeed))]
[JsonSerializable(typeof(Category))]
[JsonSerializable(typeof(Tag))]
public partial class JsonContext : JsonSerializerContext
{
}
