using Finance.Android17.Domain;
using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class AddExpenseViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Load_SortsCategoriesByRecentUse_ThenByName()
    {
        var transport = await _app.CategoryAsync("Transport");
        var leisure = await _app.CategoryAsync("Loisirs");
        await _app.Finance.AddExpenseAsync(500, "Ciné", new DateTime(2026, 10, 1), leisure.Id);
        await _app.Finance.AddExpenseAsync(500, "Bus", new DateTime(2026, 10, 10), transport.Id);
        var vm = _app.CreateAddExpense();

        await vm.LoadAsync();

        Assert.Equal(["Transport", "Loisirs", "Alimentation", "Autre", "Logement"], vm.Categories.Select(x => x.Name));
        Assert.Equal(vm.Categories.Select(x => x.Name), vm.CategoryOptions.Select(x => x.Name));
        Assert.Equal("Transport", vm.SelectedCategory?.Name);
    }

    [Fact]
    public async Task Load_SelectsTheRememberedCategory_ElseTheDefaultOne()
    {
        var housing = await _app.CategoryAsync("Logement");
        var leisure = await _app.CategoryAsync("Loisirs");
        _app.Settings.LastCategoryId = housing.Id.ToString();
        _app.Settings.DefaultCategoryId = leisure.Id.ToString();

        var remembering = _app.CreateAddExpense();
        await remembering.LoadAsync();
        Assert.Equal("Logement", remembering.SelectedCategory?.Name);
        Assert.True(remembering.CategoryOptions.Single(x => x.Name == "Logement").IsSelected);
        Assert.Equal("✓", remembering.CategoryOptions.Single(x => x.Name == "Logement").SelectionMark);

        _app.Settings.RememberLastCategory = false;
        var defaulting = _app.CreateAddExpense();
        await defaulting.LoadAsync();
        Assert.Equal("Loisirs", defaulting.SelectedCategory?.Name);
    }

    [Fact]
    public async Task SelectingACategory_IsRemembered_WhenEnabled()
    {
        var vm = _app.CreateAddExpense();
        await vm.LoadAsync();

        vm.SelectCategoryCommand.Execute(vm.CategoryOptions.Single(x => x.Name == "Loisirs"));

        Assert.Equal((await _app.CategoryAsync("Loisirs")).Id.ToString(), _app.Settings.LastCategoryId);
    }

    [Fact]
    public async Task SaveButtonText_FollowsTheAmount()
    {
        var vm = _app.CreateAddExpense();
        await vm.LoadAsync();
        Assert.Equal("Ajouter la dépense", vm.SaveButtonText);

        vm.Amount = "12,5";

        Assert.Equal("Ajouter 12,50 €", vm.SaveButtonText.Fr());
    }

    [Theory]
    [InlineData("", "Indiquez un montant valide.")]
    [InlineData("abc", "Indiquez un montant valide.")]
    [InlineData("0", "Indiquez un montant valide.")]
    [InlineData("-5", "Indiquez un montant valide.")]
    public async Task Save_RejectsInvalidAmounts(string amount, string message)
    {
        var vm = _app.CreateAddExpense();
        await vm.LoadAsync();

        vm.Amount = amount;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.ErrorMessage);
        Assert.Empty(await _app.Finance.GetExpensesAsync());
    }

    [Fact]
    public async Task Save_StoresTheExpense_AndRaisesSaved()
    {
        var vm = _app.CreateAddExpense();
        await vm.LoadAsync();
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        vm.Amount = "1234,567";
        vm.Description = "Courses";
        vm.Date = new DateTime(2026, 10, 18);
        await vm.SaveCommand.ExecuteAsync(null);

        var expense = Assert.Single(await _app.Finance.GetExpensesAsync());
        Assert.Equal(123_457, expense.AmountMinor);
        Assert.Equal("Courses", expense.Description);
        Assert.Equal(new DateTime(2026, 10, 18), expense.OccurredOn);
        Assert.Equal(vm.SelectedCategory?.Id, expense.CategoryId);
        Assert.True(saved);
        Assert.Equal(string.Empty, vm.Amount);
        Assert.Equal(new DateTime(2026, 10, 20), vm.Date);
    }

    [Fact]
    public async Task Save_WithAddAnotherBehavior_KeepsTheFormOpen()
    {
        _app.Settings.AfterSaveBehavior = "Ajouter une autre";
        var vm = _app.CreateAddExpense();
        await vm.LoadAsync();
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        vm.Amount = "5";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(saved);
        Assert.Equal("Dépense ajoutée. Vous pouvez saisir la suivante.", vm.StatusMessage);
        Assert.Single(await _app.Finance.GetExpensesAsync());
    }

    [Fact]
    public async Task DateShortcuts_SelectTodayYesterdayOrAnotherDay()
    {
        var vm = _app.CreateAddExpense();
        await vm.LoadAsync();
        Assert.True(vm.IsTodaySelected);

        vm.SelectDateCommand.Execute("Hier");
        Assert.Equal(new DateTime(2026, 10, 19), vm.Date);
        Assert.True(vm.IsYesterdaySelected);
        Assert.False(vm.IsTodaySelected);

        vm.SelectDateCommand.Execute("Autre");
        Assert.True(vm.IsCustomDateVisible);
        vm.Date = new DateTime(2026, 9, 3);
        Assert.True(vm.IsOtherDateSelected);
        Assert.Equal("03/09/2026", vm.OtherDateText);

        vm.SelectDateCommand.Execute("Aujourd’hui");
        Assert.True(vm.IsTodaySelected);
        Assert.Equal("Autre", vm.OtherDateText);
    }

    [Fact]
    public async Task BeginEdit_LoadsTheExpense_AndSaveUpdatesIt()
    {
        var transport = await _app.CategoryAsync("Transport");
        await _app.Finance.AddExpenseAsync(4_000, "Train", new DateTime(2026, 9, 15), transport.Id);
        var original = Assert.Single(await _app.Finance.GetExpensesAsync());
        var vm = _app.CreateAddExpense();

        await vm.BeginEditAsync(original);

        Assert.Equal("Modifier la dépense", vm.PageTitle);
        Assert.Equal("Enregistrer les modifications", vm.SaveButtonText);
        Assert.Equal("40,00", vm.Amount);
        Assert.Equal("Train", vm.Description);
        Assert.Equal("Transport", vm.SelectedCategory?.Name);
        Assert.True(vm.IsCustomDateVisible);

        vm.Amount = "45";
        await vm.SaveCommand.ExecuteAsync(null);

        var updated = Assert.Single(await _app.Finance.GetExpensesAsync());
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(4_500, updated.AmountMinor);
    }

    [Fact]
    public async Task ApplyReceipt_FillsAmountDateAndMerchant()
    {
        var vm = _app.CreateAddExpense("CARREFOUR MARKET\nPain 2,50 €\nTOTAL TTC 12,50 €\n29/09/2026");

        await vm.ApplyReceiptAsync("ticket.jpg");

        Assert.Equal("12,50", vm.Amount);
        Assert.Equal(new DateTime(2026, 9, 29), vm.Date);
        Assert.Equal("CARREFOUR MARKET", vm.Description);
        Assert.Equal("Ticket lu. Vérifiez les champs avant d’enregistrer.", vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ApplyReceipt_WithoutText_ReportsAnError()
    {
        var vm = _app.CreateAddExpense(ocrText: null);

        await vm.ApplyReceiptAsync("ticket.jpg");

        Assert.Equal("Aucun texte n’a été reconnu sur le ticket.", vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Amount);
    }
}
