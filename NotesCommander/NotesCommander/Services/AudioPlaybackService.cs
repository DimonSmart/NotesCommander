using Plugin.Maui.Audio;

namespace NotesCommander.Services;

public interface IAudioPlaybackService
{
        event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
        string? CurrentFilePath { get; }
        Task PlayAsync(string filePath);
        void Stop();
}

public class AudioPlaybackService : IAudioPlaybackService
{
        private readonly IAudioManager _audioManager;
        private IAudioPlayer? _currentPlayer;
        private Stream? _currentStream;

        public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;

        public string? CurrentFilePath { get; private set; }

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
}

public class PlaybackEndedEventArgs : EventArgs
{
        public PlaybackEndedEventArgs(string? filePath)
        {
                FilePath = filePath;
        }

        public string? FilePath { get; }
}
