using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotesCommander.Domain;
using NotesCommander.Models;

namespace NotesCommander.PageModels;

public partial class NoteDetailPageModel : ObservableObject
{
	[ObservableProperty]
        private VoiceNoteViewModel? currentNote;

	[ObservableProperty]
	private ObservableCollection<VoiceNotePhoto> photos = new();

	[ObservableProperty]
	private bool isPlaying;

	public string AudioDurationDisplay => CurrentNote?.Duration != TimeSpan.Zero
		? $"{(int)CurrentNote!.Duration.TotalMinutes}:{CurrentNote.Duration.Seconds:D2}"
		: "0:00";

        public void LoadNote(VoiceNoteViewModel note)
        {
                CurrentNote = note;
                Photos = new ObservableCollection<VoiceNotePhoto>(note.Photos);
		OnPropertyChanged(nameof(AudioDurationDisplay));
	}

	[RelayCommand]
	private async Task ToggleAudioPlayback()
	{
		// TODO: Implement audio playback logic
		IsPlaying = !IsPlaying;
		await Task.CompletedTask;
	}

	[RelayCommand]
	private async Task Close()
	{
		await Shell.Current.Navigation.PopModalAsync();
	}
}
