using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NotesCommander.MauiServiceDefaults;
using NotesCommander.Pages;
using NotesCommander.Services;
using Plugin.Maui.Audio;
using Syncfusion.Maui.Toolkit.Hosting;

namespace NotesCommander;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit(options =>
            {
#if WINDOWS
                options.SetShouldEnableSnackbarOnWindows(true);
#endif
            })
            .ConfigureSyncfusionToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
#if WINDOWS
                Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
                {
                    handler.PlatformView.SingleSelectionFollowsFocus = false;
                });

                Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
                {
                    if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
                    {
                        contentPanel.IsTabStop = true;
                    }
                });
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
            });

        // Aspire service discovery + HttpClient defaults for MAUI
        builder.Services.AddMauiServiceDefaults();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddLogging(configure => configure.AddDebug());
#endif

        var backendBaseUrl = builder.Configuration["Backend:BaseUrl"]
            ?? builder.Configuration["NOTESCOMMANDER_BACKEND_URL"]
            ?? "https+http://notes-backend";

        builder.Services.AddSingleton<VoiceNoteRepository>();
        builder.Services.AddSingleton<CategoryRepository>();
        builder.Services.AddSingleton<TagRepository>();
        builder.Services.AddSingleton<SeedDataService>();
        builder.Services.AddSingleton<IErrorHandler, ModalErrorHandler>();
        builder.Services.AddSingleton<IVoiceNoteService, VoiceNoteService>();
        builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
        builder.Services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();

        builder.Services.AddHttpClient(NoteSyncService.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(backendBaseUrl);
        });

        builder.Services.AddSingleton<NoteSyncService>();
        builder.Services.AddSingleton<MainPageModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<ManageMetaPageModel>();
        builder.Services.AddSingleton<ManageMetaPage>();
        builder.Services.AddSingleton<SettingsPageModel>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<NoteDetailPageModel>();
        builder.Services.AddTransient<NoteCapturePage>();
        builder.Services.AddTransient<NoteDetailPage>();

        var app = builder.Build();
        _ = app.Services.GetRequiredService<NoteSyncService>();

        return app;
    }
}
