using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class CategoriesSettingsViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Save_AddsACategory_WithTheChosenColorAndBudget()
    {
        var vm = _app.CreateCategories();
        await vm.LoadAsync();

        vm.NewCategoryName = "Santé";
        vm.CategoryBudget = "80";
        vm.SelectSuggestedColorCommand.Execute("#C62828");
        await vm.SaveCategoryCommand.ExecuteAsync(null);

        var created = (await _app.Categories.GetAllAsync()).Single(x => x.Name == "Santé");
        Assert.Equal("#C62828", created.Color);
        Assert.Equal(8_000, created.BudgetMinor);
        Assert.Equal("Catégorie ajoutée.", vm.StatusMessage);
        Assert.Contains(vm.Categories, x => x.Name == "Santé");
    }

    [Fact]
    public async Task HexAndRgbInputs_StayInSync_EvenAfterAnInvalidHex()
    {
        var vm = _app.CreateCategories();

        vm.ColorHexInput = "#102030";
        Assert.Equal(16, vm.Red);
        Assert.Equal(32, vm.Green);
        Assert.Equal(48, vm.Blue);

        vm.ColorHexInput = "zzz";   // illisible : ignoré, sans bloquer les mises à jour suivantes
        vm.Red = 255;
        Assert.Equal("#FF2030", vm.SelectedColor);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Save_RejectsAnEmptyName_AsAnError()
    {
        var vm = _app.CreateCategories();

        await vm.SaveCategoryCommand.ExecuteAsync(null);

        Assert.True(vm.StatusIsError);
        Assert.Equal("Indiquez un nom de catégorie.", vm.StatusMessage);
    }

    [Fact]
    public async Task Delete_OfACategoryInUse_ShowsTheExplanation()
    {
        var food = await _app.CategoryAsync("Alimentation");
        await _app.Expenses.AddAsync(500, "Pain", new DateTime(2026, 10, 2), food.Id);
        var vm = _app.CreateCategories();
        await vm.LoadAsync();

        await vm.DeleteCategoryCommand.ExecuteAsync(vm.Categories.Single(x => x.Name == "Alimentation"));

        Assert.True(vm.StatusIsError);
        Assert.Equal("Cette catégorie est utilisée par une transaction.", vm.StatusMessage);
    }

    [Fact]
    public async Task Delete_OfACategory_IsCancelledWhenNotConfirmed()
    {
        var vm = _app.CreateCategories();
        await vm.LoadAsync();
        vm.ConfirmDeleteAsync = (_, _) => Task.FromResult(false);
        var count = vm.Categories.Count;

        await vm.DeleteCategoryCommand.ExecuteAsync(vm.Categories.First());

        Assert.Equal(count, vm.Categories.Count);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }
}
