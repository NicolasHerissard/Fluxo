namespace Finance.Android17.Application;

public interface IAppLockService
{
    Task<bool> HasPinAsync();
    Task SetPinAsync(string pin, CancellationToken cancellationToken = default);
    Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default);
}
