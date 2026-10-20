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
		builder.Services.AddSingleton<ISecureValueStore, MauiSecureValueStore>();
		builder.Services.AddSingleton<IFileExchange, MauiFileExchange>();
		builder.Services.AddInfrastructure();
		builder.Services.AddSingleton<FinanceService>();
		builder.Services.AddSingleton<AppSettingsService>();
		builder.Services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<AppSettingsService>());
		builder.Services.AddSingleton<IBiometricService, BiometricService>();
		builder.Services.AddSingleton<IOcrService, OcrService>();
		builder.Services.AddSingleton<IReminderService, ReminderService>();
		// Les vues MAUI ne doivent jamais être réutilisées après le remplacement de
		// Window.Page : leur MauiContext Android est alors détruit.
		builder.Services.AddTransient<LockViewModel>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ExpensesViewModel>();
		builder.Services.AddTransient<IncomeViewModel>();
		builder.Services.AddTransient<SubscriptionsViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<AddExpenseViewModel>();
		builder.Services.AddTransient<LockPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ExpensesPage>();
		builder.Services.AddTransient<IncomePage>();
		builder.Services.AddTransient<SubscriptionsPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<AddExpensePage>();
		builder.Services.AddTransient<AppShell>();
		return builder.Build();
	}
}
