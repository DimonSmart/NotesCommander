using NotesCommander.PageModels;
using NotesCommander.Services;

namespace NotesCommander.Pages;

public partial class SettingsPage : ContentPage
{
	public SettingsPage() : this(ServiceHelper.GetService<SettingsPageModel>())
	{
	}

	public SettingsPage(SettingsPageModel pageModel)
	{
		InitializeComponent();
		BindingContext = pageModel;
	}
}
