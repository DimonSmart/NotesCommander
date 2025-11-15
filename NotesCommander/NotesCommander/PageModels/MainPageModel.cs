using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using NotesCommander.Models;
using NotesCommander.Pages;

namespace NotesCommander.PageModels;

public partial class MainPageModel : ObservableObject, IDisposable
{
        private readonly IVoiceNoteService _voiceNoteService;
        private readonly ModalErrorHandler _errorHandler;
        private readonly IServiceProvider _serviceProvider;
        private readonly Timer _recordingTimer = new(1000);
        private bool _isNavigatedTo;
        private bool _dataLoaded;
        private DateTimeOffset? _recordingStartedAt;

        [ObservableProperty]
        private ObservableCollection<VoiceNote> voiceNotes = new();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PermissionsStatusText))]
        private bool hasMicrophonePermission;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PermissionsStatusText))]
        private bool hasMediaPermission;

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

        public string PermissionsStatusText => HasMicrophonePermission && HasMediaPermission
                ? "Разрешения получены"
                : "Нужно разрешить доступ к микрофону и медиа";

        public string RecordingStatusText => IsRecording ? "Идёт запись" : "Ожидание записи";

        public string RecordingButtonText => IsRecording ? "Остановить запись" : "Начать запись";

        public string RecordingDurationDisplay => RecordingDuration == TimeSpan.Zero
                ? "00:00"
                : RecordingDuration.ToString("mm\\:ss");

        public bool HasDraftPhotos => DraftPhotoPaths.Count > 0;

        public MainPageModel(IVoiceNoteService voiceNoteService, ModalErrorHandler errorHandler, IServiceProvider serviceProvider)
        {
                _voiceNoteService = voiceNoteService;
                _errorHandler = errorHandler;
                _serviceProvider = serviceProvider;

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
        private async Task AddVoiceNote()
        {
                PrepareDraft();
                var capturePage = _serviceProvider.GetRequiredService<NoteCapturePage>();
                await Shell.Current.Navigation.PushModalAsync(capturePage);
        }

        [RelayCommand]
        private async Task RequestPermissions()
        {
                try
                {
                        var microphone = await RequestPermissionAsync<Permissions.Microphone>();
                        HasMicrophonePermission = microphone == PermissionStatus.Granted;

                        PermissionStatus mediaStatus;
                        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
                        {
                                mediaStatus = await RequestPermissionAsync<Permissions.StorageWrite>();
                        }
                        else
                        {
                                mediaStatus = await RequestPermissionAsync<Permissions.Photos>();
                        }

                        HasMediaPermission = mediaStatus == PermissionStatus.Granted;
                }
                catch (Exception ex)
                {
                        _errorHandler.HandleError(ex);
                }
        }

        [RelayCommand]
        private async Task ToggleRecording()
        {
                if (!HasMicrophonePermission)
                {
                        await AppShell.DisplaySnackbarAsync("Сначала предоставьте доступ к микрофону");
                        return;
                }

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
                if (!HasMediaPermission)
                {
                        await AppShell.DisplaySnackbarAsync("Нужно разрешение на фото или память");
                        return;
                }

                try
                {
                        var photo = await MediaPicker.Default.PickPhotoAsync();
                        if (photo is not null)
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
                if (!HasMediaPermission)
                {
                        await AppShell.DisplaySnackbarAsync("Нужно разрешение на фото или память");
                        return;
                }

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
                                RecognitionStatus = VoiceNoteRecognitionStatus.Pending,
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
                        VoiceNotes.Insert(0, saved);

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
                        VoiceNotes = new ObservableCollection<VoiceNote>(notes);
                }
                finally
                {
                        IsBusy = false;
                }
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

        private static async Task<PermissionStatus> RequestPermissionAsync<TPermission>() where TPermission : Permissions.BasePermission, new()
        {
                        var status = await Permissions.CheckStatusAsync<TPermission>();
                        if (status != PermissionStatus.Granted)
                        {
                                status = await Permissions.RequestAsync<TPermission>();
                        }

                        return status;
        }
}
