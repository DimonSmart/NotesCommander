using NotesCommander.PageModels;

namespace NotesCommander.Pages;

public partial class NoteDetailPage : ContentPage
{
	public NoteDetailPage(NoteDetailPageModel pageModel)
	{
		InitializeComponent();
		BindingContext = pageModel;
	}
}
