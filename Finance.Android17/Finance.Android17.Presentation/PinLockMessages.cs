using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Messages affichés quand le PIN est refusé, avec l'attente imposée après trop d'échecs.</summary>
internal static class PinLockMessages
{
    public static string Wait(TimeSpan remaining)
    {
        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return seconds < 60 ? $"{seconds} s" : $"{(int)Math.Ceiling(seconds / 60.0)} min";
    }

    public static string TooManyAttempts(TimeSpan remaining) => $"Trop d’essais. Réessayez dans {Wait(remaining)}.";

    /// <summary>Message après un PIN refusé : l'attente restante si le verrouillage vient de s'activer, sinon <paramref name="incorrect"/>.</summary>
    public static async Task<string> AfterFailureAsync(IAppLockService appLock, string incorrect)
    {
        var remaining = await appLock.GetLockoutRemainingAsync();
        return remaining > TimeSpan.Zero ? TooManyAttempts(remaining) : incorrect;
    }
}
