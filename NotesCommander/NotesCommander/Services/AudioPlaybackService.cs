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
                Stop();

                _currentStream = File.OpenRead(filePath);
                _currentPlayer = _audioManager.CreatePlayer(_currentStream);
                await Task.Run(() => _currentPlayer.Play());
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
