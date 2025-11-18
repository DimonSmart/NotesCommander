using System.Collections.ObjectModel;

namespace NotesCommander.Models;

public class VoiceNoteGroup : ObservableCollection<VoiceNote>
{
	public string DateGroupKey { get; }
	public string DateGroupDisplay { get; }

	public VoiceNoteGroup(string dateGroupKey, string dateGroupDisplay, IEnumerable<VoiceNote> notes)
		: base(notes)
	{
		DateGroupKey = dateGroupKey;
		DateGroupDisplay = dateGroupDisplay;
	}
}
