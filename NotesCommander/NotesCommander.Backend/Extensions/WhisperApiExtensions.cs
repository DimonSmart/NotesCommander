using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NotesCommander.Backend.Services;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.Extensions;

public static class WhisperApiExtensions
{
    public static IEndpointRouteBuilder MapWhisperApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whisper");

        group.MapPost("/transcribe", TranscribeAudioAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<WhisperTranscriptionResponse>()
            .WithName("TranscribeAudio")
            .DisableAntiforgery(); // Disable antiforgery for testing

        return app;
    }

    private static async Task<Results<Ok<WhisperTranscriptionResponse>, BadRequest<string>>> TranscribeAudioAsync(
        [FromForm] IFormFile audio,
        [FromForm] string? language,
        WhisperClient whisperClient,
        MediaStorage storage,
        CancellationToken cancellationToken)
    {
        if (audio == null || audio.Length == 0)
        {
            return TypedResults.BadRequest("Audio file is required");
        }

        try
        {
            // Save the audio file temporarily
            var audioPath = await storage.SaveAsync(audio, cancellationToken);

            try
            {
                // Transcribe using WhisperClient
                var result = await whisperClient.TranscribeAsync(
                    audioPath,
                    language,
                    cancellationToken);

                return TypedResults.Ok(result);
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(audioPath))
                {
                    File.Delete(audioPath);
                }
            }
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Transcription failed: {ex.Message}");
        }
    }
}
