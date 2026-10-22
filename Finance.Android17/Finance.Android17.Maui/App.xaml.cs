using Microsoft.Extensions.DependencyInjection;

using Finance.Android17.Application;

namespace Finance.Android17.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private readonly ISettingsService _settings;
	private Window? _window;
	private DateTime? _inactiveSince;

	public App(IServiceProvider services, ISettingsService settings)
	{
		InitializeComponent();
		_services = services;
		_settings = settings;
		MauiFileExchange.PurgeExports();
		UserAppTheme = AppSettingsService.ToAppTheme(settings.Theme);
		RequestedThemeChanged += (_, _) => SystemBarAppearance.Apply();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		_window = new Window(CreateLockPage());
		_window.HandlerChanged += (_, _) => SystemBarAppearance.Apply();
		_window.Deactivated += (_, _) => _inactiveSince = DateTime.UtcNow;
		_window.Activated += OnWindowActivated;
		return _window;
	}

	private void OnWindowActivated(object? sender, EventArgs e)
	{
		var inactiveSince = _inactiveSince;
		_inactiveSince = null;
		if (inactiveSince is not DateTime inactive || _window?.Page is not AppShell ||
			DateTime.UtcNow - inactive < TimeSpan.FromMinutes(_settings.AutoLockMinutes))
			return;

		// Attendre le prochain passage de la boucle UI évite de remplacer la Shell
		// pendant qu'Android termine encore sa restauration de navigation.
		_window.Dispatcher.Dispatch(ShowLock);
	}

	private LockPage CreateLockPage()
	{
		var page = _services.GetRequiredService<LockPage>();
		page.Unlocked += OnUnlocked;
		return page;
	}

	private void OnUnlocked(object? sender, EventArgs e) => ShowMain();

	public void ShowMain()
	{
		if (_window is not null)
		{
			_window.Page = _services.GetRequiredService<AppShell>();
			SystemBarAppearance.Apply();
		}
	}

	public void ShowLock()
	{
		if (_window is null || _window.Page is LockPage) return;
		MauiFileExchange.PurgeExports();
		_window.Page = CreateLockPage();
		SystemBarAppearance.Apply();
	}
}
