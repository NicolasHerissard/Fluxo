namespace Finance.Android17.Application;

/// <summary>Règle du code PIN : 4 à 8 chiffres.</summary>
public static class PinPolicy
{
    public const int MinLength = 4;
    public const int MaxLength = 8;

    public static string RequirementText => $"entre {MinLength} et {MaxLength} chiffres";

    public static bool IsValid(string? pin) =>
        pin is { Length: >= MinLength and <= MaxLength } && pin.All(char.IsAsciiDigit);
}
