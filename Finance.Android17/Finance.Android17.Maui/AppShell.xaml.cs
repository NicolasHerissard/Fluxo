namespace Finance.Android17.Maui;

public partial class AppShell : Shell
{
	public AppShell(DashboardPage dashboard, ExpensesPage expenses, IncomePage income, SubscriptionsPage subscriptions, SettingsPage settings)
	{
		InitializeComponent();
		var tabs = new TabBar();
		tabs.Items.Add(CreateTab("Accueil", "home", "tab_home.png", dashboard));
		tabs.Items.Add(CreateTab("Transactions", "expenses", "tab_expenses.png", expenses));
		tabs.Items.Add(CreateTab("Revenus", "income", "tab_income.png", income));
		tabs.Items.Add(CreateTab("Abonnements", "subscriptions", "tab_subscriptions.png", subscriptions));
		tabs.Items.Add(CreateTab("Réglages", "settings", "tab_settings.png", settings));
		Items.Add(tabs);
	}

	private static ShellContent CreateTab(string title, string route, string icon, Page page) => new() { Title = title, Route = route, Icon = icon, Content = page };
}
