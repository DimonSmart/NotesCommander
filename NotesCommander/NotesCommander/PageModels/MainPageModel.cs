using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using NotesCommander.Domain;
using NotesCommander.Models;
using NotesCommander.Mappers;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CancellationTokenSource _statusMonitorCts = new();
        private readonly Timer _recordingTimer = new(1000);
        private VoiceNoteViewModel? _currentlyPlayingNote;
        private bool _isNavigatedTo;
        private bool _dataLoaded;
        private DateTimeOffset? _recordingStartedAt;
        private string? _currentRecordingPath;

        public IAudioPlaybackService AudioPlaybackService => _audioPlaybackService;
        public IErrorHandler ErrorHandler => _errorHandler;

        [ObservableProperty]
        private ObservableCollection<VoiceNoteGroup> groupedVoiceNotes = new();

        [ObservableProperty]
        private ObservableCollection<VoiceNoteViewModel> debugVoiceNotes = new();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecordingStatusText))]
        [NotifyPropertyChangedFor(nameof(RecordingButtonText))]
        private bool isRecording;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecordingButtonText))]
        private bool isPlaying;

        [ObservableProperty]
        public VoiceNoteViewModel? currentNote;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecordingDurationDisplay))]
        private TimeSpan recordingDuration;

        [ObservableProperty]
        private string? draftTitle;

        [ObservableProperty]
        private string draftCategoryLabel = "Входящие";

        [ObservableProperty]
        private int playAudioInvokedCount;

        [ObservableProperty]
        private int playButtonClickedCount;

        [ObservableProperty]
        private string? lastPlayAudioStatus;

        [ObservableProperty]
        private string debugVmName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InternetStatusText))]
        [NotifyPropertyChangedFor(nameof(InternetStatusColor))]
        private bool hasInternetAccess;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BackendStatusText))]
        [NotifyPropertyChangedFor(nameof(BackendStatusColor))]
        private bool hasBackendConnection;

        public ObservableCollection<string> DraftPhotoPaths { get; } = new();

        public string RecordingStatusText => IsRecording ? "Идёт запись" : "Ожидание записи";

        public string RecordingButtonText => IsRecording ? "Остановить запись" : "Начать запись";

        public string RecordingDurationDisplay => RecordingDuration == TimeSpan.Zero
                ? "00:00"
                : RecordingDuration.ToString("mm\\:ss");

        public bool HasDraftPhotos => DraftPhotoPaths.Count > 0;

        public string PlayCommandStatus
        {
                get
                {
                        var cmd = GroupedVoiceNotes.SelectMany(g => g).FirstOrDefault()?.PlayAudioCommand;
                        if (cmd is null)
                                return "PlayAudioCommand=null";

                        return $"PlayAudioCommand ok (CanExecute={cmd.CanExecute(null)})";
                }
        }

        public string InternetStatusText => HasInternetAccess ? "🌐 Интернет" : "🌐 Нет сети";

        public Color InternetStatusColor => HasInternetAccess ? Colors.ForestGreen : Colors.Gray;

        public string BackendStatusText => HasBackendConnection ? "🗄️ Backend" : "🗄️ Нет связи с сервером";

        public Color BackendStatusColor => HasBackendConnection ? Colors.Teal : Colors.Gray;

        public MainPageModel(
                IVoiceNoteService voiceNoteService,
                IErrorHandler errorHandler,
                IServiceProvider serviceProvider,
                NoteSyncService noteSyncService,
                SeedDataService seedDataService,
                IAudioPlaybackService audioPlaybackService,
                IHttpClientFactory httpClientFactory)
        {
                _voiceNoteService = voiceNoteService;
                _errorHandler = errorHandler;
                _serviceProvider = serviceProvider;
                _noteSyncService = noteSyncService;
                _seedDataService = seedDataService;
                _audioPlaybackService = audioPlaybackService;
                _httpClientFactory = httpClientFactory;
                _audioPlaybackService.PlaybackEnded += HandlePlaybackEnded;

                DraftPhotoPaths.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDraftPhotos));
                DebugVmName = GetType().Name;

                _recordingTimer.AutoReset = true;
                _recordingTimer.Elapsed += (_, _) => UpdateRecordingDuration();

                HasInternetAccess = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
                Connectivity.Current.ConnectivityChanged += HandleConnectivityChanged;
                _ = MonitorBackendStatusAsync(_statusMonitorCts.Token);
        }

        [RelayCommand]
        private void RecordPlayButtonClick(VoiceNoteViewModel? noteFromButton)
        {
                PlayButtonClickedCount++;
                var title = noteFromButton is null || string.IsNullOrWhiteSpace(noteFromButton.Title)
                        ? "(null)"
                        : noteFromButton.Title;
                LastPlayAudioStatus = $"Clicked event: {title} @ {DateTime.Now:T}";
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
        private async Task OpenNoteDetail(VoiceNoteViewModel note)
        {
                var detailPage = _serviceProvider.GetRequiredService<NoteDetailPage>();
                var pageModel = _serviceProvider.GetRequiredService<NoteDetailPageModel>();
                pageModel.LoadNote(note);
                detailPage.BindingContext = pageModel;
                await Shell.Current.Navigation.PushModalAsync(detailPage);
        }

        
        public bool IsNoteCurrentlyPlaying(VoiceNoteViewModel note)
        {
                if (_currentlyPlayingNote is null || note is null)
                {
                        return false;
                }

                if (ReferenceEquals(_currentlyPlayingNote, note))
                {
                        return true;
                }

                if (_currentlyPlayingNote.LocalId != 0 && _currentlyPlayingNote.LocalId == note.LocalId)
                {
                        return true;
                }

                return false;
        }

        public void SetCurrentPlayingNote(VoiceNoteViewModel note)
        {
                if (_currentlyPlayingNote is not null && _currentlyPlayingNote != note)
                {
                        _currentlyPlayingNote.IsPlaying = false;
                }

                _currentlyPlayingNote = note;
                _currentlyPlayingNote.IsPlaying = true;
        }

        public void StopCurrentPlayback()
        {
                _audioPlaybackService.Stop();

                if (_currentlyPlayingNote is not null)
                {
                        _currentlyPlayingNote.IsPlaying = false;
                        _currentlyPlayingNote = null;
                }
        }

        private void HandlePlaybackEnded(object? sender, PlaybackEndedEventArgs e)
        {
                var finishedPath = e.FilePath;

                if (_currentlyPlayingNote is null)
                {
                        return;
                }

                if (!string.IsNullOrEmpty(finishedPath)
                    && !string.Equals(_currentlyPlayingNote.AudioFilePath, finishedPath, StringComparison.OrdinalIgnoreCase))
                {
                        return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                        if (_currentlyPlayingNote is not null)
                        {
                                _currentlyPlayingNote.IsPlaying = false;
                                _currentlyPlayingNote = null;
                        }
                });
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
            //if (IsRecording)
            //{
            //        StopRecording();
            //        return;
            //}

            //StartRecording();

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
                    _currentRecordingPath = GenerateAudioFilePath();
                    CurrentNote.AudioFilePath = _currentRecordingPath;
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
                //_currentRecordingPath = GenerateAudioFilePath();

                await _audioPlaybackService.StartRecordingAsync(CurrentNote.AudioFilePath);
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

        private async Task StopRecordingAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[NoteDetailPageModel] Stopping recording...");

                await _audioPlaybackService.StopRecordingAsync();
                IsRecording = false;

                if (!File.Exists(CurrentNote.AudioFilePath))
                    System.Diagnostics.Debug.WriteLine($"[NoteDetailPageModel] file not exists: {_currentRecordingPath}");

                // Сохраняем путь записанного файла в модель
                if (!string.IsNullOrEmpty(CurrentNote.AudioFilePath) && CurrentNote is not null)
                {
                    //CurrentNote.AudioFilePath = _currentRecordingPath;
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

        private static string GenerateAudioFilePath()
        {
            var fileName = $"voice-note-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.m4a";
            return Path.Combine(FileSystem.AppDataDirectory, fileName);
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

        [RelayCommand]
        private async Task CancelMetadata()
        {
            await Shell.Current.Navigation.PopModalAsync();
        }

        public void Dispose()
        {
                _recordingTimer.Stop();
                _recordingTimer.Dispose();
                _audioPlaybackService.PlaybackEnded -= HandlePlaybackEnded;
                Connectivity.Current.ConnectivityChanged -= HandleConnectivityChanged;
                _statusMonitorCts.Cancel();
                _statusMonitorCts.Dispose();
                StopCurrentPlayback();
        }

        private async Task InitializeSeedDataIfNeeded()
        {
                // Ensure demo assets exist even if the database was already seeded (repairs empty seed files).
                await _seedDataService.EnsureSeedAssetsAsync();

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

                        if (notes.Count == 0)
                        {
                                GroupedVoiceNotes = new ObservableCollection<VoiceNoteGroup>();
                                System.Diagnostics.Debug.WriteLine("[LoadNotesAsync] No notes found, cleared collection");
                                return;
                        }

                        var viewModels = notes
                                .Select(VoiceNoteMapper.ToViewModel)
                                .OrderByDescending(n => n.CreatedAt)
                                .ToList();

                        // Группировка по датам
                        var grouped = viewModels
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

        private async Task EnsureDebugNotesAsync()
        {
                if (DebugVoiceNotes.Count > 0)
                {
                        return;
                }

                var samplePath = await EnsureSampleAudioAssetAsync("SeedFiles/audio/demo-note.wav", "debug/demo-note.wav");
                var now = DateTime.UtcNow;

                var samples = new[]
                {
                        CreateDebugNote("Совещание по проекту", TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(15), VoiceNoteRecognitionStatus.Recognizing, samplePath, now.AddMinutes(-5)),
                        CreateDebugNote("Интервью с экспертом", TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(40), VoiceNoteRecognitionStatus.Ready, samplePath, now.AddMinutes(-15)),
                        CreateDebugNote("Идея для заметки", TimeSpan.FromSeconds(48), VoiceNoteRecognitionStatus.InQueue, samplePath, now.AddMinutes(-30)),
                        CreateDebugNote("Отчёт для руководителя", TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(5), VoiceNoteRecognitionStatus.Ready, samplePath, now.AddHours(-2)),
                        CreateDebugNote("Утренний stand-up", TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(20), VoiceNoteRecognitionStatus.Recognizing, samplePath, now.AddHours(-5))
                };

                DebugVoiceNotes = new ObservableCollection<VoiceNoteViewModel>(samples.Select(VoiceNoteMapper.ToViewModel));
        }

        private static VoiceNote CreateDebugNote(string title, TimeSpan duration, VoiceNoteRecognitionStatus status, string audioPath, DateTime timestamp)
                => new()
                {
                        Title = title,
                        Duration = duration,
                        RecognitionStatus = status,
                        AudioFilePath = audioPath,
                        CategoryLabel = "Отладка",
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                };

        private static async Task<string> EnsureSampleAudioAssetAsync(string resourcePath, string destinationFileName)
        {
                var destination = Path.Combine(FileSystem.AppDataDirectory, destinationFileName);
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory))
                {
                        Directory.CreateDirectory(directory);
                }

                var destinationInfo = new FileInfo(destination);
                if (!destinationInfo.Exists || destinationInfo.Length == 0)
                {
                        await using var sourceStream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
                        await using var destinationStream = File.Create(destination);
                        await sourceStream.CopyToAsync(destinationStream);
                        destinationInfo.Refresh();
                        System.Diagnostics.Debug.WriteLine($"[EnsureSampleAudioAssetAsync] Copied '{resourcePath}' to '{destination}' (size={destinationInfo.Length})");
                }

                return destination;
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

        private async Task MonitorBackendStatusAsync(CancellationToken cancellationToken)
        {
                while (!cancellationToken.IsCancellationRequested)
                {
                        try
                        {
                                await RefreshBackendStatusAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                                break;
                        }
                        catch (Exception ex)
                        {
                                System.Diagnostics.Debug.WriteLine($"[BackendStatus] ERROR: {ex.Message}");
                                HasBackendConnection = false;
                        }

                        try
                        {
                                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                                break;
                        }
                }
        }

        private async Task RefreshBackendStatusAsync(CancellationToken cancellationToken)
        {
                if (!HasInternetAccess)
                {
                        HasBackendConnection = false;
                        return;
                }

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(3));

                try
                {
                        var client = _httpClientFactory.CreateClient(NoteSyncService.HttpClientName);
                        var response = await client.GetAsync("alive", linkedCts.Token).ConfigureAwait(false);
                        HasBackendConnection = response.IsSuccessStatusCode;
                }
                catch (OperationCanceledException)
                {
                        HasBackendConnection = false;
                }
                catch (Exception ex)
                {
                        System.Diagnostics.Debug.WriteLine($"[BackendStatus] ERROR: {ex.Message}");
                        HasBackendConnection = false;
                }
                finally
                {
                        linkedCts.Dispose();
                }
        }

        private void HandleConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
                HasInternetAccess = e.NetworkAccess == NetworkAccess.Internet;
                if (HasInternetAccess)
                {
                        _ = RefreshBackendStatusAsync(_statusMonitorCts.Token);
                }
                else
                {
                        HasBackendConnection = false;
                }
        }

}
