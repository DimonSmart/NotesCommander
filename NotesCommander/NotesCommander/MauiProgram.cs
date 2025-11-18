using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NotesCommander.Pages;
using NotesCommander.Services;
using Syncfusion.Maui.Toolkit.Hosting;

namespace NotesCommander;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
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

#if DEBUG
		builder.Logging.AddDebug();
		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

                builder.Services.AddSingleton<VoiceNoteRepository>();
                builder.Services.AddSingleton<CategoryRepository>();
                builder.Services.AddSingleton<TagRepository>();
                builder.Services.AddSingleton<SeedDataService>();
                builder.Services.AddSingleton<ModalErrorHandler>();
                builder.Services.AddSingleton<IVoiceNoteService, VoiceNoteService>();
                builder.Services.AddHttpClient(NoteSyncService.HttpClientName, client =>
                {
                        var backendUrl = Environment.GetEnvironmentVariable("NOTESCOMMANDER_BACKEND_URL") ?? "http://localhost:5192";
                        client.BaseAddress = new Uri(backendUrl);
                });
                builder.Services.AddSingleton<NoteSyncService>();
                builder.Services.AddSingleton<MainPageModel>();
                builder.Services.AddSingleton<ManageMetaPageModel>();
                builder.Services.AddSingleton<SettingsPageModel>();
                builder.Services.AddTransient<NoteDetailPageModel>();
                builder.Services.AddTransient<NoteCapturePage>();
                builder.Services.AddTransient<NoteDetailPage>();

                var app = builder.Build();
                _ = app.Services.GetRequiredService<NoteSyncService>();

                return app;
        }
}
