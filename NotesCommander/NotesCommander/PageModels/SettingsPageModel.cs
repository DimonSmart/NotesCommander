using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using NotesCommander.Services;
using System.Collections.Generic;

namespace NotesCommander.PageModels;

public sealed record DiagnosticSetting(string Label, string Value, string Source);

public partial class SettingsPageModel : ObservableObject
{
    private readonly IErrorHandler _errorHandler;
    public IReadOnlyList<DiagnosticSetting> DiagnosticSettings { get; }
    public string EffectiveBackendBaseUrl { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PermissionsStatusText))]
    private bool hasMicrophonePermission;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PermissionsStatusText))]
    private bool hasMediaPermission;

    public string PermissionsStatusText => HasMicrophonePermission && HasMediaPermission
        ? "All permissions granted"
        : "Some permissions are missing";

    public string MicrophonePermissionStatus => HasMicrophonePermission
        ? "Microphone: granted"
        : "Microphone: not granted";

    public string MediaPermissionStatus => HasMediaPermission
        ? "Media/Storage: granted"
        : "Media/Storage: not granted";

    public SettingsPageModel(IErrorHandler errorHandler, IConfiguration configuration)
    {
        _errorHandler = errorHandler;
        EffectiveBackendBaseUrl = ResolveBackendBaseUrl(configuration);
        DiagnosticSettings = BuildDiagnostics(configuration, EffectiveBackendBaseUrl);
        CheckPermissions();
    }

    [RelayCommand]
    private async Task RequestMicrophonePermission()
    {
        try
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                HasMicrophonePermission = true;
                await AppShell.DisplayToastAsync("Windows: microphone permission assumed granted.");
                return;
            }

            var status = await RequestPermissionAsync<Permissions.Microphone>();
            HasMicrophonePermission = status == PermissionStatus.Granted;

            if (HasMicrophonePermission)
            {
                await AppShell.DisplayToastAsync("Microphone permission granted.");
            }
            else
            {
                await AppShell.DisplaySnackbarAsync("Microphone permission denied.");
            }

            OnPropertyChanged(nameof(MicrophonePermissionStatus));
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task RequestMediaPermission()
    {
        try
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                HasMediaPermission = true;
                await AppShell.DisplayToastAsync("Windows: media permission assumed granted.");
                return;
            }

            PermissionStatus mediaStatus;
            if (DeviceInfo.Current.Platform == DevicePlatform.Android)
            {
                mediaStatus = await RequestPermissionAsync<Permissions.StorageWrite>();
            }
            else
            {
                mediaStatus = await RequestPermissionAsync<Permissions.Photos>();
            }

            HasMediaPermission = mediaStatus == PermissionStatus.Granted;

            if (HasMediaPermission)
            {
                await AppShell.DisplayToastAsync("Media permission granted.");
            }
            else
            {
                await AppShell.DisplaySnackbarAsync("Media permission denied.");
            }

            OnPropertyChanged(nameof(MediaPermissionStatus));
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task RequestAllPermissions()
    {
        await RequestMicrophonePermission();
        await RequestMediaPermission();
    }

    [RelayCommand]
    private void ResetSeedData()
    {
        Preferences.Default.Remove("is_seeded");
        AppShell.DisplayToastAsync("Seed data reset. Reopen app to reseed.").FireAndForgetSafeAsync();
    }

    private async void CheckPermissions()
    {
        try
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            {
                HasMicrophonePermission = true;
                HasMediaPermission = true;
                return;
            }

            var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            HasMicrophonePermission = micStatus == PermissionStatus.Granted;

            PermissionStatus mediaStatus;
            if (DeviceInfo.Current.Platform == DevicePlatform.Android)
            {
                mediaStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            }
            else
            {
                mediaStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
            }

            HasMediaPermission = mediaStatus == PermissionStatus.Granted;

            OnPropertyChanged(nameof(MicrophonePermissionStatus));
            OnPropertyChanged(nameof(MediaPermissionStatus));
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    private static async Task<PermissionStatus> RequestPermissionAsync<TPermission>() where TPermission : Permissions.BasePermission, new()
    {
        var status = await Permissions.CheckStatusAsync<TPermission>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<TPermission>();
        }

        return status;
    }

    private static string ResolveBackendBaseUrl(IConfiguration configuration)
    {
        return configuration["Backend:BaseUrl"]
            ?? configuration["NOTESCOMMANDER_BACKEND_URL"]
            ?? "https+http://notes-backend";
    }

    private static IReadOnlyList<DiagnosticSetting> BuildDiagnostics(IConfiguration configuration, string effectiveBaseUrl)
    {
        static string Format(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

        return new List<DiagnosticSetting>
        {
            new("Эффективный адрес бэкенда", Format(effectiveBaseUrl), "Используется приложением"),
            new("Backend:BaseUrl (appsettings)", Format(configuration["Backend:BaseUrl"]), "Конфигурация MAUI"),
            new("NOTESCOMMANDER_BACKEND_URL", Format(Environment.GetEnvironmentVariable("NOTESCOMMANDER_BACKEND_URL")), "Переменная окружения"),
            new("DOTNET_ENVIRONMENT", Format(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")), "Переменная окружения"),
            new("ASPNETCORE_ENVIRONMENT", Format(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")), "Переменная окружения"),
            new("DOTNET_LAUNCH_PROFILE", Format(Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE")), "Переменная окружения"),
        };
    }
}
