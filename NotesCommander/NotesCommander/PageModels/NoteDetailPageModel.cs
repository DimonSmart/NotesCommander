using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotesCommander.Domain;
using NotesCommander.Models;
using NotesCommander.Services;

namespace NotesCommander.PageModels;

public partial class NoteDetailPageModel : ObservableObject
{
	private readonly IAudioPlaybackService _audioPlaybackService;
	private readonly IErrorHandler _errorHandler;
	private string? _currentRecordingPath;

	[ObservableProperty]
	private VoiceNoteViewModel? currentNote;

	[ObservableProperty]
	private ObservableCollection<VoiceNotePhoto> photos = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ToggleButtonText))]
	private bool isPlaying;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ToggleButtonText))]
	private bool isRecording;

	public string AudioDurationDisplay => CurrentNote?.Duration != TimeSpan.Zero
		? $"{(int)CurrentNote!.Duration.TotalMinutes}:{CurrentNote.Duration.Seconds:D2}"
		: "0:00";

	public string ToggleButtonText => IsRecording ? "Остановить запись" : (IsPlaying ? "Остановить" : "Воспроизвести");

	public NoteDetailPageModel(IAudioPlaybackService audioPlaybackService, IErrorHandler errorHandler)
	{
		_audioPlaybackService = audioPlaybackService;
		_errorHandler = errorHandler;
		_audioPlaybackService.PlaybackEnded += HandlePlaybackEnded;
		_audioPlaybackService.RecordingStateChanged += HandleRecordingStateChanged;
	}

	public void LoadNote(VoiceNoteViewModel note)
	{
		CurrentNote = note;
		Photos = new ObservableCollection<VoiceNotePhoto>(note.Photos);
		OnPropertyChanged(nameof(AudioDurationDisplay));
	}

	[RelayCommand]
	private async Task ToggleAudioPlayback()
	{
		if (CurrentNote is null)
		{
			await AppShell.DisplaySnackbarAsync("Нечего воспроизводить");
			return;
		}

		try
		{
			// Если идёт запись, остановить её
			if (IsRecording)
			{
				await StopRecordingAsync();
				return;
			}

			// Если идёт воспроизведение, остановить его
			if (IsPlaying)
			{
				_audioPlaybackService.Stop();
				IsPlaying = false;
				return;
			}

			// Если нет файла для воспроизведения, начать запись
			if (string.IsNullOrEmpty(CurrentNote.AudioFilePath))
			{
				await StartRecordingAsync();
				return;
			}

			// Воспроизвести существующий файл
			await PlayAudioAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] ERROR in ToggleAudioPlayback: {ex.Message}");
			_errorHandler.HandleError(ex);
			await AppShell.DisplaySnackbarAsync($"Ошибка: {ex.Message}");
		}
	}

	[RelayCommand]
	private async Task Close()
	{
		// Остановить воспроизведение и запись при закрытии
		if (IsRecording)
		{
			await _audioPlaybackService.CancelRecording();
			IsRecording = false;
		}
		else if (IsPlaying)
		{
			_audioPlaybackService.Stop();
			IsPlaying = false;
		}

		await Shell.Current.Navigation.PopModalAsync();
	}

	private async Task PlayAudioAsync()
	{
		if (CurrentNote?.AudioFilePath is null)
		{
			return;
		}

		try
		{
			System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] Starting playback: {CurrentNote.AudioFilePath}");
			await _audioPlaybackService.PlayAsync(CurrentNote.AudioFilePath);
			IsPlaying = true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] ERROR: {ex.Message}");
			IsPlaying = false;
			throw;
		}
	}

	private async Task StartRecordingAsync()
	{
		if (CurrentNote is null)
		{
			return;
		}

		try
		{
			System.Diagnostics.Debug.WriteLine("[NoteDetailPageModel] Starting recording...");
			
			// Генерируем путь для нового аудиофайла
			_currentRecordingPath = GenerateAudioFilePath();
			
			await _audioPlaybackService.StartRecordingAsync(_currentRecordingPath);
			IsRecording = true;
			
			await AppShell.DisplayToastAsync("Запись началась");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] ERROR: {ex.Message}");
			IsRecording = false;
			_currentRecordingPath = null;
			throw;
		}
	}

	private async Task StopRecordingAsync()
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("[NoteDetailPageModel] Stopping recording...");
			
			await _audioPlaybackService.StopRecordingAsync();
			IsRecording = false;

			// Сохраняем путь записанного файла в модель
			if (!string.IsNullOrEmpty(_currentRecordingPath) && CurrentNote is not null)
			{
				CurrentNote.AudioFilePath = _currentRecordingPath;
				System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] Saved recording path: {_currentRecordingPath}");
				await AppShell.DisplayToastAsync("Запись сохранена");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] ERROR: {ex.Message}");
			IsRecording = false;
			throw;
		}
	}

	private void HandlePlaybackEnded(object? sender, PlaybackEndedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			IsPlaying = false;
			System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] Playback ended for: {e.FilePath}");
		});
	}

	private void HandleRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			IsRecording = e.IsRecording;
			if (!e.IsRecording && !string.IsNullOrEmpty(e.FilePath))
			{
				_currentRecordingPath = e.FilePath;
			}
		});
	}

	private static string GenerateAudioFilePath()
	{
		var fileName = $"voice-note-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.m4a";
		return Path.Combine(FileSystem.AppDataDirectory, fileName);
	}
}
