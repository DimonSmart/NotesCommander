using Plugin.Maui.Audio;

namespace NotesCommander.Services;

public interface IAudioPlaybackService
{
    event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
    event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    string? CurrentFilePath { get; }
    string? CurrentRecordingPath { get; }
    bool IsRecording { get; }

    // Playback methods
    Task PlayAsync(string filePath);
    void Stop();

    // Recording methods
    Task StartRecordingAsync(string outputFilePath);
    Task StopRecordingAsync();
    Task CancelRecording();
}

public class AudioPlaybackService : IAudioPlaybackService
{
        private readonly IAudioManager _audioManager;
        private IAudioPlayer? _currentPlayer;
        private IAudioRecorder? _currentRecorder;
        private Stream? _currentStream;
        private Stream? _recordingStream;

        public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
        public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;
        public string? CurrentFilePath { get; private set; }
        public string? CurrentRecordingPath { get; private set; }
        public bool IsRecording => _currentRecorder?.IsRecording ?? false;

        public AudioPlaybackService(IAudioManager audioManager)
        {
                _audioManager = audioManager;
        }

        public async Task PlayAsync(string filePath)
        {
                try
                {
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Starting playback: {filePath}");
                        
                        if (!File.Exists(filePath))
                        {
                                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] ERROR: File not found: {filePath}");
                                throw new FileNotFoundException($"Audio file not found: {filePath}");
                        }
                        
                        Stop();

                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Opening file stream...");
                        _currentStream = File.OpenRead(filePath);
                        
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Creating player...");
                        _currentPlayer = _audioManager.CreatePlayer(_currentStream);
                        _currentPlayer.PlaybackEnded += HandlePlaybackEnded;
                        CurrentFilePath = filePath;
                        
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Starting playback...");
                        _currentPlayer.Play();
                        
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Playback started successfully. Duration: {_currentPlayer.Duration}");
                }
                catch (Exception ex)
                {
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] ERROR: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Stack trace: {ex.StackTrace}");
                        throw;
                }
        }

        public void Stop()
        {
                if (_currentPlayer is null)
                {
                        CurrentFilePath = null;
                        return;
                }

                try
                {
                        _currentPlayer.Stop();
                }
                finally
                {
                        CleanupPlayer();
                }
        }

        // ===== RECORDING METHODS =====

        public async Task StartRecordingAsync(string outputFilePath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Starting recording to: {outputFilePath}");

                // Stop playback if active
                if (_currentPlayer is not null)
                {
                    Stop();
                }

                // Create output directory if needed
                var directory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create recorder
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Creating recorder...");
                _currentRecorder = _audioManager.CreateRecorder();

                if (_currentRecorder is null)
                {
                    throw new InvalidOperationException("Failed to create audio recorder");
                }

                CurrentRecordingPath = outputFilePath;

                // Start recording
                await _currentRecorder.StartAsync();

                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Recording started successfully");
                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, outputFilePath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Stack trace: {ex.StackTrace}");
                CurrentRecordingPath = null;
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            if (_currentRecorder is null || !_currentRecorder.IsRecording)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] No active recording to stop");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Stopping recording...");
                await _currentRecorder.StopAsync();

                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Recording stopped. Saving to: {CurrentRecordingPath}");

                var recordedPath = CurrentRecordingPath;
                CleanupRecorder();

                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Recording saved successfully");
                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(false, recordedPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] ERROR: {ex.Message}");
                CleanupRecorder();
                throw;
            }
        }

        public async Task CancelRecording()
        {
            System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Cancelling recording...");

            if (_currentRecorder is not null)
            {
                try
                {
                    await _currentRecorder.StopAsync();

                    // Try to delete the partial recording file
                    if (!string.IsNullOrEmpty(CurrentRecordingPath) && File.Exists(CurrentRecordingPath))
                    {
                        File.Delete(CurrentRecordingPath);
                        System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] Deleted partial recording file");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AudioPlaybackService] ERROR during cancel: {ex.Message}");
                }
                finally
                {
                    CleanupRecorder();
                }
            }
        }

        private void HandlePlaybackEnded(object? sender, EventArgs e)
        {
            var finishedPath = CurrentFilePath;
            CleanupPlayer();
            PlaybackEnded?.Invoke(this, new PlaybackEndedEventArgs(finishedPath));
        }

        private void CleanupPlayer()
        {
            if (_currentPlayer is not null)
            {
                _currentPlayer.PlaybackEnded -= HandlePlaybackEnded;
                _currentPlayer.Dispose();
                _currentPlayer = null;
            }

            _currentStream?.Dispose();
            _currentStream = null;
            CurrentFilePath = null;
        }

        private void CleanupRecorder()
        {
            if (_currentRecorder is not null)
            {
                //_currentRecorder.Dispose();
                _currentRecorder = null;
            }

            _recordingStream?.Dispose();
            _recordingStream = null;
            CurrentRecordingPath = null;
        }
}

public class PlaybackEndedEventArgs : EventArgs
{
        public PlaybackEndedEventArgs(string? filePath)
        {
                FilePath = filePath;
        }

        public string? FilePath { get; }
}

public class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingStateChangedEventArgs(bool isRecording, string? filePath)
    {
        IsRecording = isRecording;
        FilePath = filePath;
    }

    public bool IsRecording { get; }
    public string? FilePath { get; }
}
