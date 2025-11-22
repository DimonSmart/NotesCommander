using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using NotesCommander.Domain;

namespace NotesCommander.Services;

public sealed class NoteSyncService : IAsyncDisposable
{
        public const string HttpClientName = "NotesBackend";

        private readonly IVoiceNoteService _voiceNoteService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NoteSyncService> _logger;
        private readonly Channel<int> _pendingNotes = Channel.CreateUnbounded<int>();
        private readonly ConcurrentDictionary<string, int> _remoteToLocal = new();
        private readonly SemaphoreSlim _seedSemaphore = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _uploadWorker;
        private readonly Task _pollingWorker;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
        private readonly string _fallbackBackendUrl;
        private readonly bool _disabled;

        public NoteSyncService(IVoiceNoteService voiceNoteService, IHttpClientFactory httpClientFactory, ILogger<NoteSyncService> logger, IConfiguration configuration)
        {
                _voiceNoteService = voiceNoteService;
                _httpClientFactory = httpClientFactory;
                _logger = logger;
                _fallbackBackendUrl = configuration["Backend:BaseUrl"]
                        ?? configuration["NOTESCOMMANDER_BACKEND_URL"]
                        ?? "https+http://notes-backend";

                // Temporary: allow disabling sync to avoid crashes while backend is unreachable.
                // Default is disabled unless explicitly set to "false".
                var disabledConfig = configuration["NoteSync:Disabled"] ?? configuration["DisableNoteSync"];
                _disabled = string.IsNullOrWhiteSpace(disabledConfig) || bool.TryParse(disabledConfig, out var disabled) && disabled;

                if (_disabled)
                {
                        _uploadWorker = Task.CompletedTask;
                        _pollingWorker = Task.CompletedTask;
                        _logger.LogInformation("NoteSyncService is disabled (set NoteSync:Disabled=false to enable).");
                        return;
                }

                Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

                _uploadWorker = Task.Run(ProcessQueueAsync);
                _pollingWorker = Task.Run(PollRecognitionAsync);

                _ = SeedQueueAsync();
        }

        public void TrackForUpload(VoiceNote note)
        {
                if (_disabled)
                {
                        return;
                }

                if (note is null)
                {
                        return;
                }

                if (!string.IsNullOrWhiteSpace(note.ServerId))
                {
                        TrackRecognition(note);
                        return;
                }

                _pendingNotes.Writer.TryWrite(note.LocalId);
        }

        public async ValueTask DisposeAsync()
        {
                if (_disabled)
                {
                        return;
                }

                Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
                _cts.Cancel();
                try
                {
                        await Task.WhenAll(_uploadWorker, _pollingWorker);
                }
                catch (OperationCanceledException)
                {
                        // ignored
                }

                _cts.Dispose();
                _seedSemaphore.Dispose();
        }

        private async Task SeedQueueAsync()
        {
                if (_disabled)
                {
                        return;
                }

                try
                {
                        await _seedSemaphore.WaitAsync(_cts.Token);
                        try
                        {
                                var notes = await _voiceNoteService.GetNotesAsync(_cts.Token);
                                foreach (var note in notes)
                                {
                                        if (string.IsNullOrWhiteSpace(note.ServerId)
                                            && (note.SyncStatus is VoiceNoteSyncStatus.LocalOnly or VoiceNoteSyncStatus.Failed))
                                        {
                                                _pendingNotes.Writer.TryWrite(note.LocalId);
                                        }
                                        else if (note.RecognitionStatus is VoiceNoteRecognitionStatus.InQueue or VoiceNoteRecognitionStatus.Recognizing)
                                        {
                                                TrackRecognition(note);
                                        }
                                }
                        }
                        finally
                        {
                                _seedSemaphore.Release();
                        }
                }
                catch (OperationCanceledException)
                {
                        // ignored
                }
                catch (Exception ex)
                {
                        _logger.LogError(ex, "Failed to load pending notes");
                }
        }

        private void TrackRecognition(VoiceNote note)
        {
                if (string.IsNullOrWhiteSpace(note.ServerId))
                {
                        return;
                }

                if (note.RecognitionStatus is VoiceNoteRecognitionStatus.Ready or VoiceNoteRecognitionStatus.Error)
                {
                        _remoteToLocal.TryRemove(note.ServerId, out _);
                        return;
                }

                _remoteToLocal[note.ServerId] = note.LocalId;
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
                if (e.NetworkAccess == NetworkAccess.Internet)
                {
                        _ = SeedQueueAsync();
                }
        }

        private async Task ProcessQueueAsync()
        {
                try
                {
                        while (await _pendingNotes.Reader.WaitToReadAsync(_cts.Token))
                        {
                                while (_pendingNotes.Reader.TryRead(out var noteId))
                                {
                                        if (!IsOnline())
                                        {
                                                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                                                _pendingNotes.Writer.TryWrite(noteId);
                                                continue;
                                        }

                                        try
                                        {
                                                await UploadNoteAsync(noteId, _cts.Token);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                                return;
                                        }
                                        catch (Exception ex)
                                        {
                                                _logger.LogError(ex, "Failed to upload note {NoteId}", noteId);
                                                await MarkFailedAsync(noteId, ex.Message ?? "Sync failed", _cts.Token);
                                        }
                                }
                        }
                }
                catch (OperationCanceledException)
                {
                        // ignored
                }
        }

        private async Task PollRecognitionAsync()
        {
                try
                {
                        while (!_cts.Token.IsCancellationRequested)
                        {
                                await Task.Delay(_pollingInterval, _cts.Token);
                                await SeedQueueAsync();
                                if (!IsOnline())
                                {
                                        continue;
                                }

                                foreach (var pair in _remoteToLocal.ToArray())
                                {
                                        try
                                        {
                                                await RefreshNoteAsync(pair.Key, pair.Value, _cts.Token);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                                return;
                                        }
                                        catch (Exception ex)
                                        {
                                                _logger.LogWarning(ex, "Failed to refresh note {RemoteId}", pair.Key);
                                        }
                                }
                        }
                }
                catch (OperationCanceledException)
                {
                        // ignored
                }
        }

        private async Task UploadNoteAsync(int localId, CancellationToken cancellationToken)
        {
                var note = await _voiceNoteService.GetAsync(localId, cancellationToken);
                if (note is null)
                {
                        return;
                }

                if (!string.IsNullOrWhiteSpace(note.ServerId))
                {
                        TrackRecognition(note);
                        return;
                }

                note.SyncStatus = VoiceNoteSyncStatus.Uploading;
                note.RecognitionStatus = VoiceNoteRecognitionStatus.InQueue;
                await _voiceNoteService.SaveAsync(note, cancellationToken);

                var client = CreateClient();

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(note.Title), "title");
                content.Add(new StringContent(note.CategoryLabel ?? "Входящие"), "categoryLabel");
                if (!string.IsNullOrWhiteSpace(note.OriginalText))
                {
                        content.Add(new StringContent(note.OriginalText), "originalText");
                }

                if (!string.IsNullOrWhiteSpace(note.AudioFilePath) && File.Exists(note.AudioFilePath))
                {
                        var audioContent = new StreamContent(File.OpenRead(note.AudioFilePath));
                        audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        content.Add(audioContent, "audio", Path.GetFileName(note.AudioFilePath));
                }

                foreach (var photo in note.Photos)
                {
                        if (string.IsNullOrWhiteSpace(photo.FilePath) || !File.Exists(photo.FilePath))
                        {
                                continue;
                        }

                        var photoContent = new StreamContent(File.OpenRead(photo.FilePath));
                        photoContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        content.Add(photoContent, "photos", Path.GetFileName(photo.FilePath));
                }

                var response = await client.PostAsync("notes", content, cancellationToken);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<RemoteNoteResponse>(cancellationToken: cancellationToken);
                if (payload is null)
                {
                        throw new InvalidOperationException("Backend returned empty payload");
                }

                note.ServerId = payload.Id.ToString();
                note.SyncStatus = VoiceNoteSyncStatus.Synced;
                note.RecognitionStatus = MapStatus(payload.RecognitionStatus);
                note.RecognizedText = ResolveRecognizedText(payload);
                if (!string.IsNullOrWhiteSpace(payload.CategoryLabel))
                {
                        note.CategoryLabel = payload.CategoryLabel!;
                }

                await _voiceNoteService.SaveAsync(note, cancellationToken);

                if (!string.IsNullOrWhiteSpace(note.ServerId))
                {
                        await TryRequestRecognitionAsync(client, note.ServerId, cancellationToken);
                        TrackRecognition(note);
                }
        }

        private async Task RefreshNoteAsync(string remoteId, int localId, CancellationToken cancellationToken)
        {
                var note = await _voiceNoteService.GetAsync(localId, cancellationToken);
                if (note is null)
                {
                        _remoteToLocal.TryRemove(remoteId, out _);
                        return;
                }

                var client = CreateClient();
                var response = await client.GetAsync($"notes/{remoteId}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                        return;
                }

                var payload = await response.Content.ReadFromJsonAsync<RemoteNoteResponse>(cancellationToken: cancellationToken);
                if (payload is null)
                {
                        return;
                }

                if (payload.RecognitionStatus == RemoteRecognitionStatus.Uploaded)
                {
                        await TryRequestRecognitionAsync(client, remoteId, cancellationToken);
                }

                note.RecognizedText = ResolveRecognizedText(payload);
                if (!string.IsNullOrWhiteSpace(payload.CategoryLabel))
                {
                        note.CategoryLabel = payload.CategoryLabel!;
                }

                note.RecognitionStatus = MapStatus(payload.RecognitionStatus);
                note.SyncStatus = VoiceNoteSyncStatus.Synced;
                await _voiceNoteService.SaveAsync(note, cancellationToken);

                if (note.RecognitionStatus is VoiceNoteRecognitionStatus.Ready or VoiceNoteRecognitionStatus.Error)
                {
                        _remoteToLocal.TryRemove(remoteId, out _);
                }
        }

        private async Task MarkFailedAsync(int localId, string message, CancellationToken cancellationToken)
        {
                var note = await _voiceNoteService.GetAsync(localId, cancellationToken);
                if (note is null)
                {
                        return;
                }

                note.SyncStatus = VoiceNoteSyncStatus.Failed;
                note.RecognitionStatus = VoiceNoteRecognitionStatus.Error;
                note.RecognizedText = message;
                await _voiceNoteService.SaveAsync(note, cancellationToken);
        }

        private HttpClient CreateClient()
        {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                if (client.BaseAddress is null)
                {
                        client.BaseAddress = new Uri(_fallbackBackendUrl);
                }

                return client;
        }

        private static VoiceNoteRecognitionStatus MapStatus(RemoteRecognitionStatus status)
                => status switch
                {
                        RemoteRecognitionStatus.Completed => VoiceNoteRecognitionStatus.Ready,
                        RemoteRecognitionStatus.Recognizing => VoiceNoteRecognitionStatus.Recognizing,
                        RemoteRecognitionStatus.Queued => VoiceNoteRecognitionStatus.InQueue,
                        RemoteRecognitionStatus.Failed => VoiceNoteRecognitionStatus.Error,
                        _ => VoiceNoteRecognitionStatus.InQueue
                };

        private static bool IsOnline()
                => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        private static string? ResolveRecognizedText(RemoteNoteResponse payload)
                => string.IsNullOrWhiteSpace(payload.ErrorMessage) ? payload.RecognizedText : payload.ErrorMessage;

        private async Task TryRequestRecognitionAsync(HttpClient client, string remoteId, CancellationToken cancellationToken)
        {
                try
                {
                        var response = await client.PostAsync($"notes/{remoteId}/recognize", new StringContent(string.Empty), cancellationToken);
                        response.EnsureSuccessStatusCode();
                }
                catch (OperationCanceledException)
                {
                        throw;
                }
                catch (Exception ex)
                {
                        _logger.LogWarning(ex, "Failed to start recognition for note {RemoteId}", remoteId);
                }
        }

        private sealed record RemoteNoteResponse(Guid Id, string Title, string? CategoryLabel, string? RecognizedText, RemoteRecognitionStatus RecognitionStatus, string? ErrorMessage);

        private enum RemoteRecognitionStatus
        {
                Uploaded,
                Queued,
                Recognizing,
                Completed,
                Failed
        }
}
