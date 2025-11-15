using NotesCommander.PageModels;

namespace NotesCommander.Pages;

public partial class NoteCapturePage : ContentPage
{
        private readonly MainPageModel _model;

        public NoteCapturePage(MainPageModel model)
        {
                InitializeComponent();
                _model = model;
                BindingContext = _model;
        }

        private async void CloseClicked(object? sender, EventArgs e)
        {
                await Shell.Current.Navigation.PopModalAsync();
        }
}
