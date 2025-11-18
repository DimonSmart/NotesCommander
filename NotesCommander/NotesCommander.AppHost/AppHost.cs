var builder = DistributedApplication.CreateBuilder(args);

// Use "tiny" model for testing, "base" for production
var whisperModel = builder.Configuration["WhisperModel"] ?? "base";

// Add Whisper speech recognition Docker container
var whisper = builder.AddContainer("whisper", "fedirz/faster-whisper-server", "latest-cpu")
    .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "http")
    .WithBindMount(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "huggingface"), "/root/.cache/huggingface")
    .WithEnvironment("WHISPER__MODEL", whisperModel)
    .WithEnvironment("WHISPER__INFERENCE_DEVICE", "cpu");

var backend = builder.AddProject<Projects.NotesCommander_Backend>("notes-backend")
    .WithEnvironment("Whisper__BaseUrl", whisper.GetEndpoint("http"));

// MAUI project cannot be added to AppHost
// builder.AddProject<Projects.NotesCommander>("notescommander")
//     .WithReference(backend);

builder.Build().Run();
