namespace Finance.Android17.Application;

public interface IBiometricService
{
    bool IsAvailable { get; }
    Task<bool> AuthenticateAsync(string reason, CancellationToken cancellationToken = default);
}
