using Microsoft.Extensions.Logging;
using NotesCommander.Services;

namespace NotesCommander;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			var logger = ServiceHelper.GetService<ILogger<App>>();
			var errorHandler = ServiceHelper.GetService<IErrorHandler>();
			logger?.LogError(ex, "Unhandled exception occurred");
			errorHandler?.HandleError(ex);
		}
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		var logger = ServiceHelper.GetService<ILogger<App>>();
		var errorHandler = ServiceHelper.GetService<IErrorHandler>();
		logger?.LogError(e.Exception, "Unobserved task exception occurred");
		errorHandler?.HandleError(e.Exception);
		e.SetObserved();
	}
}