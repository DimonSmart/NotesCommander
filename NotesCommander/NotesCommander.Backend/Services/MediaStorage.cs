using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NotesCommander.Backend.Storage;

namespace NotesCommander.Backend.Services;

public sealed class MediaStorage
{
    private readonly string _mediaDirectory;

    public MediaStorage(IOptions<NoteStorageOptions> options)
    {
        _mediaDirectory = options.Value.MediaDirectory;
        Directory.CreateDirectory(_mediaDirectory);
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var destination = Path.Combine(_mediaDirectory, fileName);

        await using var destinationStream = File.Create(destination);
        await file.CopyToAsync(destinationStream, cancellationToken);

        return destination;
    }
}
