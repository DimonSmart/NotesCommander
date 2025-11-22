using System.Collections.ObjectModel;

namespace NotesCommander.Models;

public class VoiceNoteGroup : ObservableCollection<VoiceNoteViewModel>
{
        public string DateGroupKey { get; }
        public string DateGroupDisplay { get; }

        public VoiceNoteGroup(string dateGroupKey, string dateGroupDisplay, IEnumerable<VoiceNoteViewModel> notes)
                : base(notes)
        {
                DateGroupKey = dateGroupKey;
		DateGroupDisplay = dateGroupDisplay;
	}
}
