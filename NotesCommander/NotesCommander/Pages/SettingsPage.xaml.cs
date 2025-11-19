using NotesCommander.PageModels;

namespace NotesCommander.Pages;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsPageModel pageModel)
	{
		InitializeComponent();
		BindingContext = pageModel;
	}
}
