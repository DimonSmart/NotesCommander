using NotesCommander.Models;
using NotesCommander.PageModels;

namespace NotesCommander.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}