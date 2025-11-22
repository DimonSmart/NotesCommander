using Aspire.Hosting;

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

// Public dev tunnel for mobile simulators/emulators
var publicDevTunnel = builder.AddDevTunnel("devtunnel-public")
    .WithAnonymousAccess()
    .WithReference(backend.GetEndpoint("https"));

// Register MAUI project so Aspire can push service discovery + dev tunnel config
var mauiApp = builder.AddMauiProject("notescommander", @"../NotesCommander/NotesCommander.csproj");

mauiApp.AddWindowsDevice()
    .WithReference(backend);

// mauiApp.AddMacCatalystDevice()
//    .WithReference(backend);

// mauiApp.AddiOSSimulator()
//    .WithOtlpDevTunnel()
//    .WithReference(backend, publicDevTunnel);

mauiApp.AddAndroidEmulator()
    .WithOtlpDevTunnel()
    .WithReference(backend, publicDevTunnel);

builder.Build().Run();
