using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class ExpensesViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    private async Task<Finance.Android17.Presentation.ExpensesViewModel> LoadedAsync()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateExpenses();
        await vm.LoadAsync();
        return vm;
    }

    private static string[] Descriptions(Finance.Android17.Presentation.ExpensesViewModel vm) =>
        vm.Expenses.Select(x => x.Description).ToArray();

    [Fact]
    public async Task Load_ListsEveryExpense_NewestFirst()
    {
        var vm = await LoadedAsync();

        Assert.Equal(["Train", "Courses", "Pain", "Marché"], Descriptions(vm));
        Assert.Equal("4 transactions · plus récentes", vm.ResultSummary);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.HasActiveFilters);
        Assert.Equal("Filtres", vm.FilterButtonText);
        Assert.Equal(["Alimentation", "Autre", "Logement", "Loisirs", "Transport"], vm.CategoryFilters.Select(x => x.Name));
    }

    [Fact]
    public async Task Load_OnAnEmptyDatabase_ExplainsThereIsNothingYet()
    {
        var vm = _app.CreateExpenses();

        await vm.LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.Equal("Aucune transaction enregistrée", vm.EmptyTitle);
        Assert.Equal("0 transactions · plus récentes", vm.ResultSummary);
    }

    [Theory]
    [InlineData("pain", new[] { "Pain" })]
    [InlineData("MARCHE", new[] { "Marché" })]
    [InlineData("alimentation", new[] { "Courses", "Pain", "Marché" })]
    [InlineData("courses alimentation", new[] { "Courses" })]
    [InlineData("12,50", new[] { "Pain" })]
    [InlineData("octobre", new[] { "Train", "Courses", "Pain" })]
    [InlineData("15/10/2026", new[] { "Train" })]
    [InlineData("introuvable", new string[0])]
    public async Task Search_IsDebounced_AndIgnoresCaseAccentsAndWordOrder(string search, string[] expected)
    {
        var vm = await LoadedAsync();

        vm.SearchText = search;
        await TestText.WaitUntilAsync(() => !vm.IsBusy && vm.Expenses.Count == expected.Length && Descriptions(vm).SequenceEqual(expected));

        Assert.Equal(expected, Descriptions(vm));
    }

    [Fact]
    public async Task Search_WithoutResult_SuggestsWideningTheSearch()
    {
        var vm = await LoadedAsync();

        vm.SearchText = "introuvable";
        await TestText.WaitUntilAsync(() => !vm.IsBusy && vm.IsEmpty);

        Assert.Equal("Aucun résultat", vm.EmptyTitle);
    }

    [Fact]
    public async Task CategoryFilter_KeepsOnlySelectedCategories_AndShowsAChip()
    {
        var vm = await LoadedAsync();
        var transport = vm.CategoryFilters.Single(x => x.Name == "Transport");

        vm.ToggleCategoryCommand.Execute(transport);

        Assert.True(transport.IsSelected);
        Assert.Equal("✓ Transport", transport.DisplayName);
        Assert.Equal("Afficher 1 résultat", vm.ApplyButtonText);

        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Equal(["Train"], Descriptions(vm));
        var chip = Assert.Single(vm.ActiveFilters);
        Assert.Equal($"category:{transport.Id}", chip.Key);
        Assert.Equal("Transport", chip.Label);
        Assert.True(vm.HasActiveFilters);
        Assert.Equal("Filtres (1)", vm.FilterButtonText);
    }

    [Theory]
    [InlineData("Aujourd’hui", new string[0])]
    [InlineData("7 derniers jours", new[] { "Train" })]
    [InlineData("Ce mois", new[] { "Train", "Courses", "Pain" })]
    [InlineData("Cette année", new[] { "Train", "Courses", "Pain", "Marché" })]
    [InlineData("Toutes les dates", new[] { "Train", "Courses", "Pain", "Marché" })]
    public async Task Period_FiltersRelativeToToday(string period, string[] expected)
    {
        var vm = await LoadedAsync();

        vm.SelectPeriodCommand.Execute(period);
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Equal(expected, Descriptions(vm));
    }

    [Fact]
    public async Task CustomPeriod_IsSwappedWhenReversed_AndShownAsAChip()
    {
        var vm = await LoadedAsync();

        vm.SelectPeriodCommand.Execute("Période personnalisée");
        vm.CustomFrom = new DateTime(2026, 9, 30);
        vm.CustomTo = new DateTime(2026, 9, 1);
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.True(vm.IsCustomPeriod);
        Assert.Equal(["Marché"], Descriptions(vm));
        Assert.Equal(new DateTime(2026, 9, 1), vm.CustomFrom);
        Assert.Equal("period", Assert.Single(vm.ActiveFilters).Key);
        Assert.Equal("01/09 – 30/09", vm.ActiveFilters[0].Label);
    }

    [Fact]
    public async Task AmountBounds_FilterOnMinimumAndMaximum()
    {
        var vm = await LoadedAsync();

        vm.MinimumAmount = "100";
        await vm.ApplyFiltersCommand.ExecuteAsync(null);
        Assert.Equal(["Courses"], Descriptions(vm));
        Assert.Equal("Min. 100 EUR", Assert.Single(vm.ActiveFilters).Label);

        vm.MinimumAmount = string.Empty;
        vm.MaximumAmount = "50,00";
        await vm.ApplyFiltersCommand.ExecuteAsync(null);
        Assert.Equal(["Train", "Pain"], Descriptions(vm));
        Assert.Equal("Max. 50,00 EUR", Assert.Single(vm.ActiveFilters).Label);
    }

    [Fact]
    public async Task InvalidAmountBound_IsIgnored()
    {
        var vm = await LoadedAsync();

        vm.MinimumAmount = "abc";
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Equal(4, vm.Expenses.Count);
        Assert.Empty(vm.ActiveFilters);
    }

    [Theory]
    [InlineData("Plus anciennes", new[] { "Marché", "Pain", "Courses", "Train" })]
    [InlineData("Montant décroissant", new[] { "Courses", "Marché", "Train", "Pain" })]
    [InlineData("Montant croissant", new[] { "Pain", "Train", "Marché", "Courses" })]
    [InlineData("Nom A–Z", new[] { "Courses", "Marché", "Pain", "Train" })]
    [InlineData("Nom Z–A", new[] { "Train", "Pain", "Marché", "Courses" })]
    public async Task Sort_OrdersTheResults(string sort, string[] expected)
    {
        var vm = await LoadedAsync();

        vm.SelectedSort = sort;
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Equal(expected, Descriptions(vm));
        Assert.Equal($"4 transactions · {sort.ToLowerInvariant()}", vm.ResultSummary);
        Assert.Equal("sort", Assert.Single(vm.ActiveFilters).Key);
    }

    [Fact]
    public async Task ResetFilters_RestoresTheDefaultView()
    {
        var vm = await LoadedAsync();
        vm.ToggleCategoryCommand.Execute(vm.CategoryFilters.Single(x => x.Name == "Transport"));
        vm.MaximumAmount = "10";
        vm.SelectedSort = "Nom A–Z";
        await vm.ApplyFiltersCommand.ExecuteAsync(null);
        Assert.Empty(vm.Expenses);
        Assert.Equal("Aucun résultat", vm.EmptyTitle);

        await vm.ResetFiltersCommand.ExecuteAsync(null);

        Assert.Equal(["Train", "Courses", "Pain", "Marché"], Descriptions(vm));
        Assert.Empty(vm.ActiveFilters);
        Assert.Equal("Aucune transaction enregistrée", vm.EmptyTitle);
    }

    [Fact]
    public async Task RemoveFilter_ClearsOnlyThatFilter()
    {
        var vm = await LoadedAsync();
        vm.MinimumAmount = "100";
        vm.SelectedSort = "Montant croissant";
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        await vm.RemoveFilterCommand.ExecuteAsync(vm.ActiveFilters.Single(x => x.Key == "minimum"));

        Assert.Equal(string.Empty, vm.MinimumAmount);
        Assert.Equal("Montant croissant", vm.SelectedSort);
        Assert.Equal(["Pain", "Train", "Marché", "Courses"], Descriptions(vm));
    }

    [Fact]
    public async Task Delete_AsksForConfirmation_AndKeepsTheExpenseWhenDeclined()
    {
        var vm = await LoadedAsync();
        string? asked = null;
        vm.ConfirmDeleteAsync = (title, _) => { asked = title; return Task.FromResult(false); };
        var pain = vm.Expenses.Single(x => x.Description == "Pain");

        await vm.DeleteCommand.ExecuteAsync(pain);

        Assert.Equal("Supprimer cette transaction ?", asked);
        Assert.Contains("Pain", Descriptions(vm));
        Assert.False(vm.IsUndoVisible);
    }

    [Fact]
    public async Task Delete_DeletesWhenConfirmed()
    {
        var vm = await LoadedAsync();
        vm.ConfirmDeleteAsync = (_, _) => Task.FromResult(true);

        await vm.DeleteCommand.ExecuteAsync(vm.Expenses.Single(x => x.Description == "Pain"));

        Assert.DoesNotContain("Pain", Descriptions(vm));
        Assert.True(vm.IsUndoVisible);
    }

    [Fact]
    public async Task Delete_ThenUndo_RestoresTheExpense()
    {
        var vm = await LoadedAsync();
        var pain = vm.Expenses.Single(x => x.Description == "Pain");

        await vm.DeleteCommand.ExecuteAsync(pain);

        Assert.Equal(["Train", "Courses", "Marché"], Descriptions(vm));
        Assert.True(vm.IsUndoVisible);
        Assert.Equal("Transaction supprimée", vm.UndoMessage);

        await vm.UndoDeleteCommand.ExecuteAsync(null);

        Assert.Equal(["Train", "Courses", "Pain", "Marché"], Descriptions(vm));
        Assert.False(vm.IsUndoVisible);
    }

    [Fact]
    public async Task Edit_RaisesEditRequested_WithTheExpenseId()
    {
        var vm = await LoadedAsync();
        Guid? requested = null;
        vm.EditRequested += (_, id) => requested = id;
        var stored = (await _app.Expenses.GetAllAsync()).Single(x => x.Description == "Courses");

        vm.EditCommand.Execute(vm.Expenses.Single(x => x.Description == "Courses"));

        Assert.Equal(stored.Id, requested);
    }

    [Fact]
    public async Task Rows_HideAmounts_WhenPrivacyIsOn()
    {
        _app.Settings.HideAmounts = true;
        var vm = await LoadedAsync();

        Assert.All(vm.Expenses, row => Assert.Equal("••••", row.Amount));
    }

    [Fact]
    public async Task Rows_ShowCategoryInitialAndColor()
    {
        var vm = await LoadedAsync();
        var transport = await _app.CategoryAsync("Transport");

        var row = vm.Expenses.Single(x => x.Description == "Train");

        Assert.Equal("T", row.CategoryInitial);
        Assert.Equal(transport.Color, row.CategoryColor);
        Assert.Equal("15 oct. 2026", row.Date);
    }

    [Theory]
    [InlineData("élan", "É")]
    [InlineData("😀 Fêtes", "😀")]
    public async Task Rows_InitialIsTheFirstTextElement_Uppercased(string categoryName, string expected)
    {
        await _app.Categories.AddAsync(categoryName, "#336699");
        var category = await _app.CategoryAsync(categoryName);
        await _app.Expenses.AddAsync(500, "Test", new DateTime(2026, 10, 1), category.Id);
        var vm = _app.CreateExpenses();

        await vm.LoadAsync();

        Assert.Equal(expected, vm.Expenses.Single(x => x.Description == "Test").CategoryInitial);
    }

    [Fact]
    public async Task TwoDeletionsInARow_CanBothBeUndone_LastFirst()
    {
        var vm = await LoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(vm.Expenses.Single(x => x.Description == "Pain"));
        await vm.DeleteCommand.ExecuteAsync(vm.Expenses.Single(x => x.Description == "Train"));
        Assert.Equal(["Courses", "Marché"], Descriptions(vm));

        await vm.UndoDeleteCommand.ExecuteAsync(null);
        Assert.Equal(["Train", "Courses", "Marché"], Descriptions(vm));
        Assert.True(vm.IsUndoVisible);

        await vm.UndoDeleteCommand.ExecuteAsync(null);
        Assert.Equal(["Train", "Courses", "Pain", "Marché"], Descriptions(vm));
        Assert.False(vm.IsUndoVisible);
    }
}
