using NotesCommander.PageModels;
using NotesCommander.Services;

namespace NotesCommander.Pages;

public partial class MainPage : ContentPage
{
	private readonly MainPageModel _model;

	public MainPage()
	{
		InitializeComponent();
		_model = ServiceHelper.GetService<MainPageModel>();
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