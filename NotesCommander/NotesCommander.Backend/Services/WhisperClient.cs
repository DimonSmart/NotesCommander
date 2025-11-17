using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotesCommander.Backend.Services;

public sealed class WhisperClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhisperClient> _logger;
    private readonly WhisperOptions _options;

    public WhisperClient(HttpClient httpClient, IOptions<WhisperOptions> options, ILogger<WhisperClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<WhisperTranscriptionResponse> TranscribeAsync(string audioFilePath, string? language = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            
            // Read the audio file
            var audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            
            form.Add(audioContent, "file", Path.GetFileName(audioFilePath));
            
            // Add model parameter
            form.Add(new StringContent(_options.Model), "model");
            
            // Add language if specified
            if (!string.IsNullOrWhiteSpace(language))
            {
                form.Add(new StringContent(language), "language");
            }
            
            // Add response format
            form.Add(new StringContent("verbose_json"), "response_format");

            var response = await _httpClient.PostAsync("/v1/audio/transcriptions", form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var transcription = JsonSerializer.Deserialize<WhisperTranscriptionResponse>(responseContent);

            if (transcription == null)
            {
                throw new InvalidOperationException("Failed to deserialize Whisper response");
            }

            return transcription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe audio file: {AudioFilePath}", audioFilePath);
            throw;
        }
    }
}

public sealed class WhisperOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string Model { get; set; } = "base";
    public string Language { get; set; } = "ru";
}

public sealed class WhisperTranscriptionResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("segments")]
    public List<WhisperSegment>? Segments { get; set; }
}

public sealed class WhisperSegment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("seek")]
    public int Seek { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("tokens")]
    public List<int>? Tokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("avg_logprob")]
    public double AvgLogprob { get; set; }

    [JsonPropertyName("compression_ratio")]
    public double CompressionRatio { get; set; }

    [JsonPropertyName("no_speech_prob")]
    public double NoSpeechProb { get; set; }
}

