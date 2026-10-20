using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<RecurringIncome> RecurringIncomes => Set<RecurringIncome>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<FinancialProfile> FinancialProfiles => Set<FinancialProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasKey(x => x.Id);
        modelBuilder.Entity<Category>().Property(x => x.Name).HasMaxLength(80).IsRequired();
        modelBuilder.Entity<Expense>().HasKey(x => x.Id);
        modelBuilder.Entity<Expense>().Property(x => x.Description).HasMaxLength(240);
        modelBuilder.Entity<Expense>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Expense>().Ignore(x => x.Amount);
        modelBuilder.Entity<Subscription>().HasKey(x => x.Id);
        modelBuilder.Entity<Subscription>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<Subscription>().Ignore(x => x.Amount);
        modelBuilder.Entity<Subscription>().Ignore(x => x.MonthlyEquivalentMinor);
        modelBuilder.Entity<Income>().HasKey(x => x.Id);
        modelBuilder.Entity<Income>().Property(x => x.Description).HasMaxLength(240);
        modelBuilder.Entity<Income>().Ignore(x => x.Amount);
        modelBuilder.Entity<RecurringIncome>().HasKey(x => x.Id);
        modelBuilder.Entity<RecurringIncome>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<RecurringIncome>().Ignore(x => x.Amount);
        modelBuilder.Entity<RecurringIncome>().Ignore(x => x.MonthlyEquivalentMinor);
        modelBuilder.Entity<SavingsGoal>().HasKey(x => x.Id);
        modelBuilder.Entity<SavingsGoal>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<SavingsGoal>().Ignore(x => x.Progress);
        modelBuilder.Entity<FinancialProfile>().HasKey(x => x.Id);
    }
}
