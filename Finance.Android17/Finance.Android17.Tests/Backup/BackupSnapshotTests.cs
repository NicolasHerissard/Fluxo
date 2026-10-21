using System.Text.Json;
using Finance.Android17.Application;

namespace Finance.Android17.Tests.Backup;

public class BackupSnapshotTests
{
    [Fact]
    public void FormatVersion1_DeserializesWithEmptyV2Data()
    {
        const string json = """
            {
              "FormatVersion": 1,
              "CreatedAtUtc": "2026-10-03T10:00:00Z",
              "Categories": [],
              "Expenses": [],
              "Subscriptions": []
            }
            """;

        var snapshot = JsonSerializer.Deserialize<FinanceBackupSnapshot>(json);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.FormatVersion);
        Assert.Null(snapshot.Incomes);
        Assert.Equal(0, snapshot.InitialBalanceMinor);
    }
}
