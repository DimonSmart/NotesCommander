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
        private readonly IErrorHandler _errorHandler;
        private readonly NoteSyncService _noteSyncService;
        private readonly IAudioPlaybackService _audioPlaybackService;
        private readonly SeedDataService _seedDataService;
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

        public MainPageModel(IVoiceNoteService voiceNoteService, IErrorHandler errorHandler, IServiceProvider serviceProvider, NoteSyncService noteSyncService, SeedDataService seedDataService, IAudioPlaybackService audioPlaybackService)
        {
                _voiceNoteService = voiceNoteService;
                _errorHandler = errorHandler;
                _serviceProvider = serviceProvider;
                _noteSyncService = noteSyncService;
                _seedDataService = seedDataService;
                _audioPlaybackService = audioPlaybackService;

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
                try
                {
                        System.Diagnostics.Debug.WriteLine($"[Appearing] Starting... _dataLoaded={_dataLoaded}, _isNavigatedTo={_isNavigatedTo}");
                        
                        // Тестируем базу данных
                        await TestDbHelper.TestDatabase();
                        
                        if (!_dataLoaded)
                        {
                                System.Diagnostics.Debug.WriteLine("[Appearing] Initializing seed data...");
                                await InitializeSeedDataIfNeeded();
                                
                                System.Diagnostics.Debug.WriteLine("[Appearing] Refreshing data...");
                                await Refresh();
                                _dataLoaded = true;
                        }
                        else if (!_isNavigatedTo)
                        {
                                System.Diagnostics.Debug.WriteLine("[Appearing] Refreshing data (not navigated to)...");
                                await Refresh();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[Appearing] Completed. GroupedVoiceNotes.Count={GroupedVoiceNotes.Count}");
                }
                catch (Exception ex)
                {
                        System.Diagnostics.Debug.WriteLine($"[Appearing] ERROR: {ex.Message}\n{ex.StackTrace}");
                        throw;
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
                finally
                {
                        IsRefreshing = false;
                }
        }

        [RelayCommand]
        private async Task OpenNoteDetail(VoiceNote note)
        {
                var detailPage = _serviceProvider.GetRequiredService<NoteDetailPage>();
                var pageModel = _serviceProvider.GetRequiredService<NoteDetailPageModel>();
                pageModel.LoadNote(note);
                await Shell.Current.Navigation.PushModalAsync(detailPage);
        }

        [RelayCommand]
        private async Task PlayAudio(VoiceNote note)
        {
                try
                {
                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] Starting playback for note: {note.Title}");
                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] Audio file path: {note.AudioFilePath}");
                        
                        if (string.IsNullOrEmpty(note.AudioFilePath))
                        {
                                System.Diagnostics.Debug.WriteLine($"[PlayAudio] ERROR: AudioFilePath is null or empty");
                                await AppShell.DisplaySnackbarAsync("Аудиофайл не указан");
                                return;
                        }
                        
                        if (!File.Exists(note.AudioFilePath))
                        {
                                System.Diagnostics.Debug.WriteLine($"[PlayAudio] ERROR: File does not exist: {note.AudioFilePath}");
                                await AppShell.DisplaySnackbarAsync("Аудиофайл не найден");
                                return;
                        }
                        
                        var fileInfo = new FileInfo(note.AudioFilePath);
                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] File size: {fileInfo.Length} bytes");

                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] Calling audio playback service...");
                        await _audioPlaybackService.PlayAsync(note.AudioFilePath);
                        
                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] Playback started successfully");
                        await AppShell.DisplayToastAsync($"▶ {note.Title}");
                }
                catch (Exception ex)
                {
                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] ERROR: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[PlayAudio] Stack trace: {ex.StackTrace}");
                        _errorHandler.HandleError(ex);
                        await AppShell.DisplaySnackbarAsync($"Ошибка воспроизведения: {ex.Message}");
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
                _audioPlaybackService.Stop();
        }

        private async Task InitializeSeedDataIfNeeded()
        {
                var isSeeded = Preferences.Default.Get("is_seeded", false);
                var existingNotes = await _voiceNoteService.GetNotesAsync();
                if (!isSeeded || existingNotes.Count == 0)
                {
                        await _seedDataService.LoadSeedDataAsync();
                        Preferences.Default.Set("is_seeded", true);
                }
        }

        private async Task LoadNotesAsync()
        {
                try
                {
                        IsBusy = true;
                        var notes = await _voiceNoteService.GetNotesAsync();
                        System.Diagnostics.Debug.WriteLine($"[LoadNotesAsync] Loaded {notes.Count} notes from service");
                        
                        // Фильтрация по последнему месяцу
                        var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                        var filteredNotes = notes.Where(n => n.CreatedAt >= oneMonthAgo).ToList();
                        System.Diagnostics.Debug.WriteLine($"[LoadNotesAsync] Filtered to {filteredNotes.Count} notes (last month)");
                        
                        // Группировка по датам
                        var grouped = filteredNotes
                                .GroupBy(n => n.CreatedAt.Date)
                                .OrderByDescending(g => g.Key)
                                .Select(g => new VoiceNoteGroup(
                                        g.Key.ToString("yyyy-MM-dd"),
                                        FormatDateGroupHeader(g.Key),
                                        g.OrderByDescending(n => n.CreatedAt)))
                                .ToList();
                        
                        System.Diagnostics.Debug.WriteLine($"[LoadNotesAsync] Created {grouped.Count} groups");
                        foreach (var group in grouped)
                        {
                                System.Diagnostics.Debug.WriteLine($"[LoadNotesAsync]   Group '{group.DateGroupDisplay}' has {group.Count} notes");
                        }
                        
                        GroupedVoiceNotes = new ObservableCollection<VoiceNoteGroup>(grouped);
                        System.Diagnostics.Debug.WriteLine($"[LoadNotesAsync] GroupedVoiceNotes assigned. Count={GroupedVoiceNotes.Count}");
                }
                catch (Exception ex)
                {
                        System.Diagnostics.Debug.WriteLine($"[LoadNotesAsync] ERROR: {ex.Message}\n{ex.StackTrace}");
                        throw;
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
