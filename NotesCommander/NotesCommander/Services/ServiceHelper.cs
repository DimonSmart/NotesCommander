using Microsoft.Extensions.DependencyInjection;

namespace NotesCommander.Services;

public static class ServiceHelper
{
    public static TService GetService<TService>() where TService : class
        => Application.Current!.Handler.MauiContext!.Services.GetRequiredService<TService>();
}
