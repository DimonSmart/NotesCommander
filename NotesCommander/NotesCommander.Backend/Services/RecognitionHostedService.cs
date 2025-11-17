using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.Services;

public sealed class RecognitionHostedService : BackgroundService
{
    private readonly ILogger<RecognitionHostedService> _logger;
    private readonly NoteStore _store;
    private readonly WhisperClient _whisperClient;

    public RecognitionHostedService(
        ILogger<RecognitionHostedService> logger,
        NoteStore store,
        WhisperClient whisperClient)
    {
        _logger = logger;
        _store = store;
        _whisperClient = whisperClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recognition service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                var queued = await _store.ListByStatusAsync(NoteRecognitionStatus.Queued, stoppingToken);

                foreach (var note in queued)
                {
                    await ProcessNoteAsync(note, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recognition processing failed");
            }
        }

        _logger.LogInformation("Recognition service stopped");
    }

    private async Task ProcessNoteAsync(NoteRecord note, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing note {NoteId}: {Title}", note.Id, note.Title);

            // Update status to Recognizing
            await _store.UpdateStatusAsync(
                note.Id,
                NoteRecognitionStatus.Recognizing,
                note.RecognizedText,
                note.CategoryLabel,
                null,
                cancellationToken);

            // Check if audio file exists
            if (string.IsNullOrWhiteSpace(note.AudioPath) || !File.Exists(note.AudioPath))
            {
                _logger.LogWarning("Audio file not found for note {NoteId}: {AudioPath}", note.Id, note.AudioPath);
                await _store.UpdateStatusAsync(
                    note.Id,
                    NoteRecognitionStatus.Failed,
                    note.RecognizedText,
                    note.CategoryLabel,
                    "Аудиофайл не найден",
                    cancellationToken);
                return;
            }

            // Transcribe audio using Whisper
            var transcription = await _whisperClient.TranscribeAsync(note.AudioPath, null, cancellationToken);

            _logger.LogInformation("Successfully transcribed note {NoteId}", note.Id);

            // Update note with transcription result
            await _store.UpdateStatusAsync(
                note.Id,
                NoteRecognitionStatus.Completed,
                transcription.Text,
                note.CategoryLabel,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process note {NoteId}", note.Id);
            await _store.UpdateStatusAsync(
                note.Id,
                NoteRecognitionStatus.Failed,
                note.RecognizedText,
                note.CategoryLabel,
                $"Ошибка распознавания: {ex.Message}",
                cancellationToken);
        }
    }
}
