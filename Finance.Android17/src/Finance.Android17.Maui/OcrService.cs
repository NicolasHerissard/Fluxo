using Finance.Android17.Application;

#if ANDROID
using Android.App;
using Android.Runtime;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
#endif

namespace Finance.Android17.Maui;

public sealed class OcrService : IOcrService
{
    public Task<OcrTextResult?> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
#if ANDROID
        return AndroidOcrService.RecognizeAsync(imagePath, cancellationToken);
#else
        return Task.FromResult<OcrTextResult?>(null);
#endif
    }
}

#if ANDROID
internal static class AndroidOcrService
{
    public static async Task<OcrTextResult?> RecognizeAsync(string imagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = InputImage.FromFilePath(Android.App.Application.Context, Android.Net.Uri.Parse(imagePath)!);
        using var recognizer = TextRecognition.GetClient(new TextRecognizerOptions.Builder().Build());
        var result = await AwaitTextAsync(recognizer.Process(image), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result is null) return null;
        var text = string.Join(Environment.NewLine, result.TextBlocks.SelectMany(block => block.Lines).Select(line => line.Text));
        return new OcrTextResult(text);
    }

    private static async Task<Xamarin.Google.MLKit.Vision.Text.Text?> AwaitTextAsync(Android.Gms.Tasks.Task task, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?>(TaskCreationOptions.RunContinuationsAsynchronously);
        task.AddOnCompleteListener(new CompletionListener(completion));
        return await completion.Task.WaitAsync(cancellationToken);
    }

    private sealed class CompletionListener(TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?> completion) : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (!task.IsSuccessful)
            {
                completion.TrySetException(new InvalidOperationException("ML Kit n’a pas pu lire l’image."));
                return;
            }

            completion.TrySetResult(task.Result?.JavaCast<Xamarin.Google.MLKit.Vision.Text.Text>());
        }
    }
}
#endif
