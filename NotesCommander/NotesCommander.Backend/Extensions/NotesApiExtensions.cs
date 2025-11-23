using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NotesCommander.Backend.Models;
using NotesCommander.Backend.Mappers;
using NotesCommander.Backend.Services;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.Extensions;

public static class NotesApiExtensions
{
    public static IEndpointRouteBuilder MapNotesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notes");

        group.MapPost("/", UploadNoteAsync)
            .Accepts<NoteUploadRequest>("multipart/form-data")
            .Produces<NoteResponse>(StatusCodes.Status201Created)
            .WithName("UploadNote");

        group.MapPost("/{id:guid}/recognize", StartRecognitionAsync)
            .Produces<NoteResponse>()
            .WithName("RequestRecognition");

        group.MapGet("/{id:guid}", GetNoteAsync)
            .Produces<NoteResponse>()
            .WithName("GetNote");

        return app;
    }

    private static async Task<Results<Created<NoteResponse>, BadRequest<string>>> UploadNoteAsync(
        [FromForm] NoteUploadRequest request,
        NoteStore store,
        MediaStorage storage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.BadRequest("Title is required");
        }

        var audioPath = request.Audio is null
            ? null
            : await storage.SaveAsync(request.Audio, cancellationToken);

        var photoPaths = new List<string>();
        var photos = request.Photos ?? [];
        foreach (var photo in photos)
        {
            var saved = await storage.SaveAsync(photo, cancellationToken);
            photoPaths.Add(saved);
        }

        var note = new NoteRecord
        {
            Title = request.Title.Trim(),
            CategoryLabel = string.IsNullOrWhiteSpace(request.CategoryLabel) ? "Входящие" : request.CategoryLabel.Trim(),
            OriginalText = request.OriginalText,
            AudioPath = audioPath,
            PhotoPaths = photoPaths,
            RecognitionStatus = NoteRecognitionStatus.Uploaded
        };

        var savedNote = await store.CreateAsync(note, cancellationToken);
        var response = NoteMapper.ToResponse(savedNote);

        return TypedResults.Created($"/notes/{savedNote.Id}", response);
    }

    private static async Task<Results<Ok<NoteResponse>, NotFound>> StartRecognitionAsync(
        Guid id,
        NoteStore store,
        CancellationToken cancellationToken)
    {
        var note = await store.GetAsync(id, cancellationToken);
        if (note is null)
        {
            return TypedResults.NotFound();
        }

        if (note.RecognitionStatus is NoteRecognitionStatus.Completed or NoteRecognitionStatus.Recognizing)
        {
            return TypedResults.Ok(NoteMapper.ToResponse(note));
        }

        await store.UpdateStatusAsync(id, NoteRecognitionStatus.Queued, null, null, null, cancellationToken);
        note = await store.GetAsync(id, cancellationToken);

        return TypedResults.Ok(NoteMapper.ToResponse(note!));
    }

    private static async Task<Results<Ok<NoteResponse>, NotFound>> GetNoteAsync(
        Guid id,
        NoteStore store,
        CancellationToken cancellationToken)
    {
        var note = await store.GetAsync(id, cancellationToken);
        return note is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(NoteMapper.ToResponse(note));
    }
}
