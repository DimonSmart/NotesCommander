using NotesCommander.Models;
using NotesCommander.PageModels;

namespace NotesCommander.Pages;

public partial class MainPage : ContentPage
{
	private readonly MainPageModel _model;

	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		_model = model;
		BindingContext = _model;
	}

	private async void OnPlayButtonClicked(object? sender, EventArgs e)
	{
		if (BindingContext is not MainPageModel vm)
		{
			return;
		}

		var note = (sender as BindableObject)?.BindingContext as VoiceNote;
		if (vm.RecordPlayButtonClickCommand.CanExecute(note))
		{
			vm.RecordPlayButtonClickCommand.Execute(note);
		}

		if (vm.PlayAudioCommand.CanExecute(note))
		{
			await vm.PlayAudioCommand.ExecuteAsync(note);
		}
		else
		{
			vm.LastPlayAudioStatus = "PlayAudioCommand.CanExecute returned false";
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_model.AppearingCommand.CanExecute(null))
		{
			await _model.AppearingCommand.ExecuteAsync(null);
		}
	}
}
