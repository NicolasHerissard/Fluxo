namespace Finance.Android17.Application;

/// <summary>Situation du mois en cours : dépenses, revenus, net, solde actuel, solde prévu en fin de mois et taux d'épargne.</summary>
public sealed record FinancialSummary(
    long ExpenseTotalMinor,
    long IncomeTotalMinor,
    long NetTotalMinor,
    long CurrentBalanceMinor,
    long ForecastBalanceMinor,
    double SavingsRate);
