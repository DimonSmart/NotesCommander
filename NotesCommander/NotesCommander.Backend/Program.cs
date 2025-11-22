using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using NotesCommander.Backend.Extensions;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Services;
using NotesCommander.Backend.Storage;

// Initialize SQLite
SQLitePCL.Batteries.Init();

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<NoteStorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<WhisperOptions>(builder.Configuration.GetSection("Whisper"));
builder.Services.AddSingleton<MediaStorage>();
builder.Services.AddSingleton<NoteStore>();

// Configure Whisper client - use BaseUrl from configuration (set by Aspire)
// Note: No resilience handler - transcription is a long operation where retries don't make sense
builder.Services.AddHttpClient<WhisperClient>((serviceProvider, client) =>
{
    var whisperOptions = serviceProvider.GetRequiredService<IOptions<WhisperOptions>>().Value;
    var baseUrl = whisperOptions.BaseUrl;
    
    // BaseUrl from configuration (Aspire sets this via environment variable)
    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    
    // Set timeout for Whisper transcription (model loading + transcription can take 2+ minutes)
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHostedService<RecognitionHostedService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapDefaultEndpoints();
app.MapNotesApi();
app.MapWhisperApi();

app.Run();
