namespace Finance.Android17.Application;

/// <summary>
/// Le coffre de la plateforme ne restitue plus la clé de la base alors que le fichier existe. L'interface peut alors proposer,
/// après confirmation, de repartir d'une base neuve (<see cref="IDatabaseRecovery"/>).
/// </summary>
public sealed class DatabaseKeyUnavailableException(string message) : InvalidOperationException(message);
