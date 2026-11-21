using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Finance.Android17.Application;
using Finance.Android17.Infrastructure;
using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID
		// Les champs sont dessinés par les Border XAML de l'application. Retirer
		// le soulignement Android évite une deuxième bordure incohérente.
		var transparent = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FinanceInput", (handler, _) => handler.PlatformView.BackgroundTintList = transparent);
		Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("FinanceInput", (handler, _) => handler.PlatformView.BackgroundTintList = transparent);
		Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("FinanceInput", (handler, _) => handler.PlatformView.BackgroundTintList = transparent);
		Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("FinanceInput", (handler, _) => handler.PlatformView.BackgroundTintList = transparent);
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif
		builder.Services
			.AddPlatformServices()
			.AddInfrastructure()
			.AddApplication()
			.AddPresentation()
			.AddPages();
		return builder.Build();
	}

	/// <summary>Adaptateurs vers la plateforme (stockage sécurisé, partage de fichiers, biométrie, OCR, rappels, préférences).</summary>
	private static IServiceCollection AddPlatformServices(this IServiceCollection services)
	{
		services.AddSingleton<ISecureValueStore, MauiSecureValueStore>();
		services.AddSingleton<IFileExchange, MauiFileExchange>();
		services.AddSingleton<AppSettingsService>();
		services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<AppSettingsService>());
		services.AddSingleton<IBiometricService, BiometricService>();
		services.AddSingleton<IOcrService, OcrService>();
		services.AddSingleton<IReminderService, ReminderService>();
		services.AddSingleton<IAlertPublisher, AlertPublisher>();
		services.AddSingleton<IDailyCheckScheduler, DailyCheckScheduler>();
		services.AddSingleton<IAppUpdateService, AppUpdateService>();
		return services;
	}

	/// <summary>Pages et Shell, transients pour la même raison que les ViewModels (voir <c>AddPresentation</c>).</summary>
	private static IServiceCollection AddPages(this IServiceCollection services)
	{
		services.AddTransient<LockPage>();
		services.AddTransient<DashboardPage>();
		services.AddTransient<ExpensesPage>();
		services.AddTransient<IncomePage>();
		services.AddTransient<SubscriptionsPage>();
		services.AddTransient<SettingsPage>();
		services.AddTransient<AddExpensePage>();
		services.AddTransient<CategoriesSettingsPage>();
		services.AddTransient<DataSettingsPage>();
			services.AddTransient<NotificationsPage>();
		services.AddTransient<AppShell>();
		return services;
	}
}
