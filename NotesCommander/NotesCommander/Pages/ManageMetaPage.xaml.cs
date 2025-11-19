using NotesCommander.Services;

namespace NotesCommander.Pages;

public partial class ManageMetaPage : ContentPage
{
	public ManageMetaPage() : this(ServiceHelper.GetService<ManageMetaPageModel>())
	{
	}

	public ManageMetaPage(ManageMetaPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}