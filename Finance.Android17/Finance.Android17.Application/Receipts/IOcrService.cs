namespace Finance.Android17.Application;

public sealed record OcrTextResult(string Text);

/// <summary>Reconnaissance de texte sur la photo d'un ticket (assurée par la plateforme).</summary>
public interface IOcrService
{
    Task<OcrTextResult?> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default);
}
