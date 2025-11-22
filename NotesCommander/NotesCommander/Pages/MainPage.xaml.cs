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

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_model.AppearingCommand.CanExecute(null))
		{
			await _model.AppearingCommand.ExecuteAsync(null);
		}
	}
}
