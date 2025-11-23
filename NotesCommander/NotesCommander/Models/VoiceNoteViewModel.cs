using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotesCommander.Domain;
using System.Linq;

namespace NotesCommander.Models;

public partial class VoiceNoteViewModel : ObservableObject
{
        private bool isPlaying;

        public int LocalId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string AudioFilePath { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        public string? OriginalText { get; set; }
                = string.Empty;

        public string? RecognizedText { get; set; }
                = string.Empty;

        public string CategoryLabel { get; set; } = "Входящие";

        public VoiceNoteRecognitionStatus RecognitionStatus { get; set; }
                = VoiceNoteRecognitionStatus.InQueue;

        public VoiceNoteSyncStatus SyncStatus { get; set; }
                = VoiceNoteSyncStatus.LocalOnly;

        public string? ServerId { get; set; }
                = string.Empty;

        public List<VoiceNotePhoto> Photos { get; set; } = [];

        public List<VoiceNoteTag> Tags { get; set; } = [];

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string DurationDisplay => Duration == TimeSpan.Zero
                ? "00:00"
                : Duration.ToString("mm\\:ss");

        public string RecognitionStatusDisplay => RecognitionStatus switch
        {
                VoiceNoteRecognitionStatus.Ready => "Готово",
                VoiceNoteRecognitionStatus.Recognizing => "Распознаётся",
                VoiceNoteRecognitionStatus.Error => "Ошибка",
                _ => "В очереди"
        };

        public string? PhotoPreviewPath => Photos.FirstOrDefault()?.FilePath;

        public bool HasPhotoPreview => !string.IsNullOrEmpty(PhotoPreviewPath);

        public string PhotoPreviewFallbackGlyph
        {
                get
                {
                        var normalizedCategory = (CategoryLabel ?? string.Empty).Trim().ToLowerInvariant();
                        return normalizedCategory switch
                        {
                                "работа" => "💼",
                                "личное" => "🏡",
                                "интервью" => "🎤",
                                _ => "📝"
                        };
                }
        }

        public bool IsPlaying
        {
                get => isPlaying;
                set => SetProperty(ref isPlaying, value);
        }

        [RelayCommand]
        private async Task PlayAudio()
        {
            var mainModel = MauiProgram.Resvices.GetService<MainPageModel>()!;
            VoiceNoteViewModel note = this;

            try
            {
                if (note is null)
                {
                    mainModel.LastPlayAudioStatus = "Parameter note is null";
                    await AppShell.DisplaySnackbarAsync("Cannot play this note");
                    return;
                }

                mainModel.PlayAudioInvokedCount++;
                var title = string.IsNullOrWhiteSpace(note.Title) ? "(Untitled)" : note.Title;
                var switchingFromOtherNote = !mainModel.IsNoteCurrentlyPlaying(note);

                if (mainModel.IsNoteCurrentlyPlaying(note))
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayAudio] Stopping playback for note: {title}");
                    mainModel.StopCurrentPlayback();
                    mainModel.LastPlayAudioStatus = $"Stopped: {title}";
                    return;
                }

                if (switchingFromOtherNote)
                {
                    System.Diagnostics.Debug.WriteLine("[PlayAudio] Switching to another note, stopping the current one first");
                    mainModel.StopCurrentPlayback();
                }

                mainModel.LastPlayAudioStatus = $"Clicked: {title} @ {DateTime.Now:T}";

                System.Diagnostics.Debug.WriteLine($"[PlayAudio] Starting playback for note: {title}");
                System.Diagnostics.Debug.WriteLine($"[PlayAudio] Audio file path: {note.AudioFilePath}");

                if (string.IsNullOrEmpty(note.AudioFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("[PlayAudio] ERROR: AudioFilePath is null or empty");
                    mainModel.LastPlayAudioStatus = "File path missing";
                    await AppShell.DisplaySnackbarAsync("Missing audio file path");
                    return;
                }

                if (!File.Exists(note.AudioFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayAudio] ERROR: File does not exist: {note.AudioFilePath}");
                    mainModel.LastPlayAudioStatus = "File not found";
                    await AppShell.DisplaySnackbarAsync("Audio file not found");
                    return;
                }

                var fileInfo = new FileInfo(note.AudioFilePath);
                System.Diagnostics.Debug.WriteLine($"[PlayAudio] File size: {fileInfo.Length} bytes");

                mainModel.StopCurrentPlayback();

                System.Diagnostics.Debug.WriteLine("[PlayAudio] Calling audio playback service...");
                await mainModel.AudioPlaybackService.PlayAsync(note.AudioFilePath);
                mainModel.SetCurrentPlayingNote(note);

                System.Diagnostics.Debug.WriteLine("[PlayAudio] Playback started successfully");
                mainModel.LastPlayAudioStatus = $"Started: {note.Title}";
                await AppShell.DisplayToastAsync($"Playing: {note.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayAudio] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PlayAudio] Stack trace: {ex.StackTrace}");
                mainModel.ErrorHandler.HandleError(ex);
                mainModel.LastPlayAudioStatus = $"Error: {ex.Message}";
                await AppShell.DisplaySnackbarAsync($"Playback error: {ex.Message}");
                mainModel.StopCurrentPlayback();
            }
        }
}
