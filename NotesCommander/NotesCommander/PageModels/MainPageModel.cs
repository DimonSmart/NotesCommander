using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Timers;
using Timer = System.Timers.Timer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using NotesCommander.Models;
using NotesCommander.Pages;
using NotesCommander.Services;

namespace NotesCommander.PageModels;

public partial class MainPageModel : ObservableObject, IDisposable
{
        private readonly IVoiceNoteService _voiceNoteService;
        private readonly ModalErrorHandler _errorHandler;
        private readonly NoteSyncService _noteSyncService;
        private readonly IServiceProvider _serviceProvider;
        private readonly Timer _recordingTimer = new(1000);
        private bool _isNavigatedTo;
        private bool _dataLoaded;
        private DateTimeOffset? _recordingStartedAt;

        [ObservableProperty]
        private ObservableCollection<VoiceNoteGroup> groupedVoiceNotes = new();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecordingStatusText))]
        [NotifyPropertyChangedFor(nameof(RecordingButtonText))]
        private bool isRecording;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecordingDurationDisplay))]
        private TimeSpan recordingDuration;

        [ObservableProperty]
        private string? draftTitle;

        [ObservableProperty]
        private string draftCategoryLabel = "Входящие";

        public ObservableCollection<string> DraftPhotoPaths { get; } = new();

        public string RecordingStatusText => IsRecording ? "Идёт запись" : "Ожидание записи";

        public string RecordingButtonText => IsRecording ? "Остановить запись" : "Начать запись";

        public string RecordingDurationDisplay => RecordingDuration == TimeSpan.Zero
                ? "00:00"
                : RecordingDuration.ToString("mm\\:ss");

        public bool HasDraftPhotos => DraftPhotoPaths.Count > 0;

        public MainPageModel(IVoiceNoteService voiceNoteService, ModalErrorHandler errorHandler, IServiceProvider serviceProvider, NoteSyncService noteSyncService)
        {
                _voiceNoteService = voiceNoteService;
                _errorHandler = errorHandler;
                _serviceProvider = serviceProvider;
                _noteSyncService = noteSyncService;

                DraftPhotoPaths.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDraftPhotos));

                _recordingTimer.AutoReset = true;
                _recordingTimer.Elapsed += (_, _) => UpdateRecordingDuration();
        }

        [RelayCommand]
        private void NavigatedTo() => _isNavigatedTo = true;

        [RelayCommand]
        private void NavigatedFrom() => _isNavigatedTo = false;

        [RelayCommand]
        private async Task Appearing()
        {
                if (!_dataLoaded)
                {
                        await Refresh();
                        _dataLoaded = true;
                }
                else if (!_isNavigatedTo)
                {
                        await Refresh();
                }
        }

        [RelayCommand]
        private async Task Refresh()
        {
                try
                {
                        IsRefreshing = true;
                        await LoadNotesAsync();
                }
                catch (Exception ex)
                {
                        _errorHandler.HandleError(ex);
                }
                finally
                {
                        IsRefreshing = false;
                }
        }

        [RelayCommand]
        private async Task OpenNoteDetail(VoiceNote note)
        {
                try
                {
                        var detailPage = _serviceProvider.GetRequiredService<NoteDetailPage>();
                        var pageModel = _serviceProvider.GetRequiredService<NoteDetailPageModel>();
                        pageModel.LoadNote(note);
                        await Shell.Current.Navigation.PushModalAsync(detailPage);
                }
                catch (Exception ex)
                {
                        _errorHandler.HandleError(ex);
                }
        }

        [RelayCommand]
        private async Task AddVoiceNote()
        {
                PrepareDraft();
                var capturePage = _serviceProvider.GetRequiredService<NoteCapturePage>();
                await Shell.Current.Navigation.PushModalAsync(capturePage);
        }

        [RelayCommand]
        private async Task ToggleRecording()
        {
                if (IsRecording)
                {
                        StopRecording();
                        return;
                }

                StartRecording();
        }

        [RelayCommand]
        private async Task PickPhoto()
        {
                try
                {
                        var photos = await MediaPicker.Default.PickPhotosAsync();
                        if (photos is null)
                        {
                                return;
                        }

                        foreach (var photo in photos)
                        {
                                DraftPhotoPaths.Add(photo.FullPath);
                        }
                }
                catch (FeatureNotSupportedException)
                {
                        await AppShell.DisplaySnackbarAsync("Выбор фото не поддерживается на этом устройстве");
                }
                catch (Exception ex)
                {
                        _errorHandler.HandleError(ex);
                }
        }

        [RelayCommand]
        private async Task CapturePhoto()
        {
                try
                {
                        if (!MediaPicker.Default.IsCaptureSupported)
                        {
                                await AppShell.DisplaySnackbarAsync("Камера недоступна");
                                return;
                        }

                        var photo = await MediaPicker.Default.CapturePhotoAsync();
                        if (photo is not null)
                        {
                                DraftPhotoPaths.Add(photo.FullPath);
                        }
                }
                catch (FeatureNotSupportedException)
                {
                        await AppShell.DisplaySnackbarAsync("Съёмка фото не поддерживается на этом устройстве");
                }
                catch (Exception ex)
                {
                        _errorHandler.HandleError(ex);
                }
        }

        [RelayCommand]
        private async Task SaveMetadata()
        {
                try
                {
                        if (IsRecording)
                        {
                                StopRecording();
                        }

                        var title = string.IsNullOrWhiteSpace(DraftTitle)
                                ? $"Заметка {DateTime.Now:HH:mm}"
                                : DraftTitle.Trim();

                        var note = new VoiceNote
                        {
                                Title = title,
                                Duration = RecordingDuration,
                                CategoryLabel = string.IsNullOrWhiteSpace(DraftCategoryLabel)
                                        ? "Входящие"
                                        : DraftCategoryLabel.Trim(),
                                RecognitionStatus = VoiceNoteRecognitionStatus.InQueue,
                                Photos = DraftPhotoPaths
                                        .Select(path => new VoiceNotePhoto
                                        {
                                                FilePath = path,
                                                CreatedAt = DateTime.UtcNow
                                        })
                                        .ToList(),
                                AudioFilePath = CreateDraftAudioPath(),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                        };

                        var saved = await _voiceNoteService.SaveAsync(note);
                        await LoadNotesAsync();
                        _noteSyncService.TrackForUpload(saved);

                        await AppShell.DisplayToastAsync("Голосовая заметка сохранена");
                        PrepareDraft();
                        await Shell.Current.Navigation.PopModalAsync();
                }
                catch (Exception ex)
                {
                        _errorHandler.HandleError(ex);
                }
        }

        public void Dispose()
        {
                _recordingTimer.Stop();
                _recordingTimer.Dispose();
        }

        private async Task LoadNotesAsync()
        {
                try
                {
                        IsBusy = true;
                        var notes = await _voiceNoteService.GetNotesAsync();
                        
                        // Фильтрация по последнему месяцу
                        var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                        var filteredNotes = notes.Where(n => n.CreatedAt >= oneMonthAgo).ToList();
                        
                        // Группировка по датам
                        var grouped = filteredNotes
                                .GroupBy(n => n.CreatedAt.Date)
                                .OrderByDescending(g => g.Key)
                                .Select(g => new VoiceNoteGroup(
                                        g.Key.ToString("yyyy-MM-dd"),
                                        FormatDateGroupHeader(g.Key),
                                        g.OrderByDescending(n => n.CreatedAt)))
                                .ToList();
                        
                        GroupedVoiceNotes = new ObservableCollection<VoiceNoteGroup>(grouped);
                }
                finally
                {
                        IsBusy = false;
                }
        }
        
        private static string FormatDateGroupHeader(DateTime date)
        {
                var today = DateTime.UtcNow.Date;
                var yesterday = today.AddDays(-1);
                
                if (date == today)
                        return "Сегодня";
                if (date == yesterday)
                        return "Вчера";
                
                return date.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
        }

        private void PrepareDraft()
        {
                DraftTitle = string.Empty;
                DraftCategoryLabel = "Входящие";
                RecordingDuration = TimeSpan.Zero;
                DraftPhotoPaths.Clear();
                _recordingStartedAt = null;
                IsRecording = false;
                _recordingTimer.Stop();
        }

        private void StartRecording()
        {
                RecordingDuration = TimeSpan.Zero;
                _recordingStartedAt = DateTimeOffset.UtcNow;
                _recordingTimer.Start();
                IsRecording = true;
        }

        private void StopRecording()
        {
                _recordingTimer.Stop();
                if (_recordingStartedAt is not null)
                {
                        RecordingDuration = DateTimeOffset.UtcNow - _recordingStartedAt.Value;
                }

                _recordingStartedAt = null;
                IsRecording = false;
        }

        private void UpdateRecordingDuration()
        {
                if (_recordingStartedAt is null)
                {
                        return;
                }

                var duration = DateTimeOffset.UtcNow - _recordingStartedAt.Value;
                MainThread.BeginInvokeOnMainThread(() => RecordingDuration = duration);
        }

        private static string CreateDraftAudioPath()
        {
                var fileName = $"voice-note-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.m4a";
                return Path.Combine(FileSystem.AppDataDirectory, fileName);
        }

}
