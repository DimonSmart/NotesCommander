using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using NotesCommander.Services;

namespace NotesCommander.PageModels;

public partial class SettingsPageModel : ObservableObject
{
	private readonly ModalErrorHandler _errorHandler;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PermissionsStatusText))]
	private bool hasMicrophonePermission;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PermissionsStatusText))]
	private bool hasMediaPermission;

	public string PermissionsStatusText => HasMicrophonePermission && HasMediaPermission
		? "Все разрешения получены"
		: "Требуются дополнительные разрешения";

	public string MicrophonePermissionStatus => HasMicrophonePermission
		? "✓ Разрешено"
		: "✗ Не разрешено";

	public string MediaPermissionStatus => HasMediaPermission
		? "✓ Разрешено"
		: "✗ Не разрешено";

	public SettingsPageModel(ModalErrorHandler errorHandler)
	{
		_errorHandler = errorHandler;
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
				await AppShell.DisplayToastAsync("На Windows разрешения предоставляются автоматически");
				return;
			}

			var status = await RequestPermissionAsync<Permissions.Microphone>();
			HasMicrophonePermission = status == PermissionStatus.Granted;

			if (HasMicrophonePermission)
			{
				await AppShell.DisplayToastAsync("Доступ к микрофону предоставлен");
			}
			else
			{
				await AppShell.DisplaySnackbarAsync("Не удалось получить доступ к микрофону");
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
				await AppShell.DisplayToastAsync("На Windows разрешения предоставляются автоматически");
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
				await AppShell.DisplayToastAsync("Доступ к медиа предоставлен");
			}
			else
			{
				await AppShell.DisplaySnackbarAsync("Не удалось получить доступ к медиа");
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
}
