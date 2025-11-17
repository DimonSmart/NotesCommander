using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotesCommander.Backend.Services;
using Xunit;
using Xunit.Abstractions;

namespace NotesCommander.Backend.Tests.Services;

/// <summary>
/// Functional tests for Whisper speech recognition integration.
/// These tests require Docker to be running and will pull the Whisper container image.
/// Run with: dotnet test --filter Category=Functional
/// </summary>
public sealed class WhisperIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IContainer? _whisperContainer;
    private const int WhisperPort = 8000;
    private const string TestAudioFileName = "samples_jfk.wav";
    private const string ExpectedTranscriptionFragment = "ask not what your country can do for you";

    private static string GetTestAudioFilePath()
    {
        // Get the path to the test audio file in TestData folder
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", TestAudioFileName);
        if (!File.Exists(testDataPath))
        {
            throw new FileNotFoundException($"Test audio file not found: {testDataPath}");
        }
        return testDataPath;
    }

    public WhisperIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("Starting Whisper container...");

        // Create Whisper container
        _whisperContainer = new ContainerBuilder()
            .WithImage("fedirz/faster-whisper-server:latest-cpu")
            .WithPortBinding(WhisperPort, true)
            .WithEnvironment("WHISPER__MODEL", "tiny") // Use tiny model for faster tests
            .WithEnvironment("WHISPER__INFERENCE_DEVICE", "cpu")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(WhisperPort)
                    .ForPath("/health")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .Build();

        await _whisperContainer.StartAsync();

        _output.WriteLine($"Whisper container started on port {_whisperContainer.GetMappedPublicPort(WhisperPort)}");

        // Verify test audio file exists
        var testAudioPath = GetTestAudioFilePath();
        _output.WriteLine($"Using test audio file: {testAudioPath}");
    }

    public async Task DisposeAsync()
    {
        if (_whisperContainer != null)
        {
            _output.WriteLine("Stopping Whisper container...");
            await _whisperContainer.StopAsync();
            await _whisperContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Functional")]
    public async Task TranscribeAsync_WithValidAudioFile_ReturnsTranscription()
    {
        // Arrange
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{_whisperContainer!.GetMappedPublicPort(WhisperPort)}")
        };

        var options = Options.Create(new WhisperOptions
        {
            Model = "tiny",
            Language = "en"
        });

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
        });
        var logger = loggerFactory.CreateLogger<WhisperClient>();

        var whisperClient = new WhisperClient(httpClient, options, logger);

        // Act
        _output.WriteLine("Starting transcription...");
        var testAudioPath = GetTestAudioFilePath();
        var result = await whisperClient.TranscribeAsync(testAudioPath, language: "en");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        Assert.NotEmpty(result.Text);

        _output.WriteLine($"Transcription result: '{result.Text}'");
        _output.WriteLine($"Language detected: {result.Language}");
        _output.WriteLine($"Duration: {result.Duration}");

        // Verify the transcription contains the expected text from JFK's speech
        var transcriptionLower = result.Text.ToLowerInvariant();
        Assert.Contains(ExpectedTranscriptionFragment, transcriptionLower);

        _output.WriteLine($"✓ Transcription contains expected fragment: '{ExpectedTranscriptionFragment}'");
    }

    [Fact]
    [Trait("Category", "Functional")]
    public async Task TranscribeAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{_whisperContainer!.GetMappedPublicPort(WhisperPort)}")
        };

        var options = Options.Create(new WhisperOptions
        {
            Model = "tiny",
            Language = "en"
        });

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
        });
        var logger = loggerFactory.CreateLogger<WhisperClient>();

        var whisperClient = new WhisperClient(httpClient, options, logger);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await whisperClient.TranscribeAsync("non-existent-file.wav");
        });
    }
}

