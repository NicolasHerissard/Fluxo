using Finance.Android17.Domain;

namespace Finance.Android17.Tests.Domain;

public class EntityTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Touch_DatesTheUpdate_AndRenewsTheVersion()
    {
        var expense = new Expense();
        var created = expense.CreatedAtUtc;
        var version = expense.Version;

        expense.Touch(Now);

        Assert.Equal(Now, expense.UpdatedAtUtc);
        Assert.NotEqual(version, expense.Version);
        Assert.Equal(created, expense.CreatedAtUtc);
        Assert.Null(expense.DeletedAtUtc);
    }

    [Fact]
    public void MarkDeleted_FlagsTheEntity_AndRenewsTheVersion()
    {
        var category = new Category();
        var version = category.Version;

        category.MarkDeleted(Now);

        Assert.Equal(Now, category.DeletedAtUtc);
        Assert.Equal(Now, category.UpdatedAtUtc);
        Assert.NotEqual(version, category.Version);
    }

    [Fact]
    public void MarkRestored_ClearsTheDeletion_AndRenewsTheVersion()
    {
        var subscription = new Subscription();
        subscription.MarkDeleted(Now);
        var version = subscription.Version;

        subscription.MarkRestored(Now.AddHours(1));

        Assert.Null(subscription.DeletedAtUtc);
        Assert.Equal(Now.AddHours(1), subscription.UpdatedAtUtc);
        Assert.NotEqual(version, subscription.Version);
    }

    [Fact]
    public void NewEntities_GetDistinctIdentifiersAndVersions()
    {
        var first = new Income();
        var second = new Income();

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Version, second.Version);
    }
}
