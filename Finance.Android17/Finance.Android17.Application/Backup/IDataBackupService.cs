namespace Finance.Android17.Application;

public interface IDataBackupService
{
    Task ExportEncryptedAsync(string password, CancellationToken cancellationToken = default);
    Task<bool> ImportEncryptedAsync(string password, CancellationToken cancellationToken = default);
}
