using Microsoft.AspNetCore.Mvc;
using NotesCommander.Backend.Extensions;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Services;
using NotesCommander.Backend.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<NoteStorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<MediaStorage>();
builder.Services.AddSingleton<NoteStore>();
builder.Services.AddHostedService<RecognitionHostedService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapDefaultEndpoints();
app.MapNotesApi();

app.Run();
