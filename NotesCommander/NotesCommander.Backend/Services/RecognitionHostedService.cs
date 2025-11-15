using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.Services;

public sealed class RecognitionHostedService : BackgroundService
{
    private static readonly string[] Categories =
    [
        "Работа",
        "Личное",
        "Интервью",
        "Заметки"
    ];

    private readonly ILogger<RecognitionHostedService> _logger;
    private readonly NoteStore _store;

    public RecognitionHostedService(ILogger<RecognitionHostedService> logger, NoteStore store)
    {
        _logger = logger;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var random = new Random();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                var queued = await _store.ListByStatusAsync(NoteRecognitionStatus.Queued, stoppingToken);
                foreach (var note in queued)
                {
                    await _store.UpdateStatusAsync(note.Id, NoteRecognitionStatus.Recognizing, note.RecognizedText, note.CategoryLabel, null, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

                    var success = random.NextDouble() > 0.1;
                    if (success)
                    {
                        var category = Categories[random.Next(Categories.Length)];
                        var recognizedText = $"Распознанная заметка: {note.Title} ({DateTimeOffset.UtcNow:O})";
                        await _store.UpdateStatusAsync(note.Id, NoteRecognitionStatus.Completed, recognizedText, category, null, stoppingToken);
                    }
                    else
                    {
                        await _store.UpdateStatusAsync(note.Id, NoteRecognitionStatus.Failed, note.RecognizedText, note.CategoryLabel, "Имитация ошибки", stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recognition simulation failed");
            }
        }
    }
}
