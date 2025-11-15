using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace NotesCommander.Backend.Models;

public sealed class NoteUploadRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? CategoryLabel { get; set; }
        = string.Empty;

    public string? OriginalText { get; set; }
        = string.Empty;

    public IFormFile? Audio { get; set; }
        = default;

    public List<IFormFile> Photos { get; set; }
        = [];
}
