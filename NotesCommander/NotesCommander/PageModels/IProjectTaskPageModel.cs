using CommunityToolkit.Mvvm.Input;
using NotesCommander.Models;

namespace NotesCommander.PageModels;

public interface IProjectTaskPageModel
{
	IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
	bool IsBusy { get; }
}