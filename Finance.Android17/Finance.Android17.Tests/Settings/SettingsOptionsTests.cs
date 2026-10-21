using Finance.Android17.Application;

namespace Finance.Android17.Tests.Settings;

/// <summary>Ces valeurs sont enregistrées telles quelles dans les préférences : elles ne doivent jamais changer.</summary>
public class SettingsOptionsTests
{
    [Fact]
    public void Themes_KeepTheirStoredValues() =>
        Assert.Equal(["Système", "Clair", "Sombre"], ThemeOptions.All);

    [Fact]
    public void AfterSaveChoices_KeepTheirStoredValues() =>
        Assert.Equal(["Fermer", "Ajouter une autre"], AfterSaveOptions.All);

    [Fact]
    public void DefaultsOfTheTestSettings_AreTheDocumentedOnes()
    {
        var settings = new Support.FakeSettings();

        Assert.Equal(ThemeOptions.FollowSystem, settings.Theme);
        Assert.Equal(AfterSaveOptions.Close, settings.AfterSaveBehavior);
    }
}
