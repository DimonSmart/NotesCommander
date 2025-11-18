using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using NotesCommander.Backend.Services;
using Xunit;
using Xunit.Abstractions;

namespace NotesCommander.Backend.Tests.Services;

/// <summary>
/// Integration tests for Whisper speech recognition through Backend API.
/// These tests use Aspire to start the entire application stack including the Whisper container.
/// Run with: dotnet test --filter Category=Functional
/// </summary>
public sealed class BackendWhisperIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private DistributedApplication? _app;
    private HttpClient? _backendClient;
    private const string TestAudioFileName = "samples_jfk.wav";
    private const string ExpectedTranscriptionFragment = "ask not what your country can do for you";

    private static string GetTestAudioFilePath()
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", TestAudioFileName);
        if (!File.Exists(testDataPath))
        {
            throw new FileNotFoundException($"Test audio file not found: {testDataPath}");
        }
        return testDataPath;
    }

    public BackendWhisperIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("Starting Aspire application...");

        // Set environment variable to use tiny model for faster testing
        Environment.SetEnvironmentVariable("WhisperModel", "tiny");

        // Create and start the Aspire application
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NotesCommander_AppHost>();

        _app = await appHost.BuildAsync();
        
        _output.WriteLine("Starting Aspire resources...");
        var startTask = _app.StartAsync();
        
        // Wait for start with timeout
        var startCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await startTask.WaitAsync(startCancellation.Token);

        _output.WriteLine("Aspire application started successfully");

        // Wait for resources to be ready using ResourceNotificationService
        var notificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        
        _output.WriteLine("Waiting for whisper container to be running...");
        await notificationService.WaitForResourceAsync("whisper", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(2));
        _output.WriteLine("Whisper container is running");
        
        _output.WriteLine("Waiting for notes-backend to be running...");
        await notificationService.WaitForResourceAsync("notes-backend", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(1));
        _output.WriteLine("Backend is running");

        // Create HTTP client for the backend service with longer timeout
        _backendClient = _app.CreateHttpClient("notes-backend");
        _backendClient.Timeout = TimeSpan.FromMinutes(10); // Whisper может долго загружать модель и обрабатывать
        
        _output.WriteLine($"Backend client created: {_backendClient.BaseAddress}");

        // Verify test audio file exists
        var testAudioPath = GetTestAudioFilePath();
        _output.WriteLine($"Using test audio file: {testAudioPath}");

        // Give Whisper container time to fully initialize and download model if needed
        // The tiny model should download quickly, but first run might take 1-2 minutes
        _output.WriteLine("Waiting 30 seconds for Whisper to download and load model...");
        await Task.Delay(TimeSpan.FromSeconds(30));
        _output.WriteLine("Initialization complete");
    }

    public async Task DisposeAsync()
    {
        _backendClient?.Dispose();

        if (_app != null)
        {
            _output.WriteLine("Stopping Aspire application...");
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Functional")]
    public async Task TranscribeAsync_ThroughBackendApi_ReturnsTranscription()
    {
        // Arrange
        var testAudioPath = GetTestAudioFilePath();
        
        using var form = new MultipartFormDataContent();
        var audioBytes = await File.ReadAllBytesAsync(testAudioPath);
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "audio", Path.GetFileName(testAudioPath));
        form.Add(new StringContent("en"), "language");

        // Act
        _output.WriteLine("Sending transcription request to backend...");
        var response = await _backendClient!.PostAsync("/api/whisper/transcribe", form);

        // Assert
        Assert.True(response.IsSuccessStatusCode, 
            $"Expected successful response, but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var result = await response.Content.ReadFromJsonAsync<WhisperTranscriptionResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        Assert.NotEmpty(result.Text);

        _output.WriteLine($"Transcription result: '{result.Text}'");
        _output.WriteLine($"Language detected: {result.Language}");
        _output.WriteLine($"Duration: {result.Duration}");

        // Verify the transcription contains the expected text from JFK's speech
        var transcriptionLower = result.Text.ToLowerInvariant();
        Assert.True(transcriptionLower.Contains(ExpectedTranscriptionFragment), 
            $"Expected transcription to contain '{ExpectedTranscriptionFragment}', but got: '{result.Text}'");

        _output.WriteLine($"✓ Transcription contains expected fragment: '{ExpectedTranscriptionFragment}'");
    }

    [Fact]
    [Trait("Category", "Functional")]
    public async Task TranscribeAsync_WithInvalidFile_ReturnsBadRequest()
    {
        // Arrange
        using var form = new MultipartFormDataContent();
        var invalidContent = new ByteArrayContent([1, 2, 3, 4, 5]);
        invalidContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(invalidContent, "audio", "invalid.wav");

        // Act
        _output.WriteLine("Sending invalid transcription request to backend...");
        var response = await _backendClient!.PostAsync("/api/whisper/transcribe", form);

        // Assert - we expect either BadRequest or an error response
        Assert.False(response.IsSuccessStatusCode, 
            "Expected error response for invalid audio file");
        
        _output.WriteLine($"Received expected error response: {response.StatusCode}");
    }
}
