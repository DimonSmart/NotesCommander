using Plugin.Maui.Audio;

namespace NotesCommander.Services;

public interface IAudioPlaybackService
{
        Task PlayAsync(string filePath);
        void Stop();
}

public class AudioPlaybackService : IAudioPlaybackService
{
        private readonly IAudioManager _audioManager;
        private IAudioPlayer? _currentPlayer;
        private Stream? _currentStream;

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
                        return;
                }

                try
                {
                        _currentPlayer.Stop();
                }
                finally
                {
                        _currentPlayer.Dispose();
                        _currentPlayer = null;
                        _currentStream?.Dispose();
                        _currentStream = null;
                }
        }
}
