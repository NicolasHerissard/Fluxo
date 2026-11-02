namespace Finance.Android17.Application;

public enum NotificationTopic { SubscriptionDue, MonthlyBudget, CategoryBudget, Overdraft }

public enum NotificationSeverity { Info, Warning, Critical }

/// <summary>
/// Une alerte à montrer dans le centre de notifications. <see cref="Key"/> identifie l'alerte telle qu'elle est
/// aujourd'hui (elle change avec le mois, le niveau ou la date d'échéance) : c'est elle qui sert à savoir si
/// l'utilisateur l'a déjà vue. Le texte est composé par la présentation à partir des champs numériques.
/// </summary>
/// <param name="Subject">Nom de l'abonnement ou de la catégorie concernée (vide pour le budget global et le découvert).</param>
/// <param name="AmountMinor">Montant en cause : prélèvement, dépenses du budget ou solde prévu.</param>
/// <param name="LimitMinor">Budget de référence (0 quand il n'y en a pas).</param>
/// <param name="DaysAway">Jours restants avant l'échéance (0 : aujourd'hui) ; 0 pour les autres alertes.</param>
public sealed record AppNotification(
    string Key,
    NotificationTopic Topic,
    NotificationSeverity Severity,
    string Subject,
    long AmountMinor,
    long LimitMinor,
    int DaysAway);
