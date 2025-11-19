using Microsoft.Extensions.Logging;
using NotesCommander.Services;

namespace NotesCommander;

public partial class App : Application
{
	private readonly ILogger<App> _logger;
	private readonly IErrorHandler _errorHandler;

	public App(ILogger<App> logger, IErrorHandler errorHandler)
	{
		InitializeComponent();

		_logger = logger;
		_errorHandler = errorHandler;

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
			_logger.LogError(ex, "Unhandled exception occurred");
			_errorHandler.HandleError(ex);
		}
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		_logger.LogError(e.Exception, "Unobserved task exception occurred");
		_errorHandler.HandleError(e.Exception);
		e.SetObserved();
	}
}