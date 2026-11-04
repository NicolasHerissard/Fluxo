using Finance.Android17.Application;

namespace Finance.Android17.Maui;

#pragma warning disable CA1416, CA1422
public sealed class BiometricService : IBiometricService
{
#if ANDROID
    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(29) || Platform.CurrentActivity is not { } activity) return false;
            var manager = activity.GetSystemService(Android.Content.Context.BiometricService) as Android.Hardware.Biometrics.BiometricManager;
            if (manager is null) return false;
            // Dès Android 11, seule une biométrie « forte » (classe 3) est acceptée : un déverrouillage facial ou digital de classe 2 se laisse tromper plus facilement.
            return OperatingSystem.IsAndroidVersionAtLeast(30)
                ? manager.CanAuthenticate((int)Android.Hardware.Biometrics.BiometricManagerAuthenticators.BiometricStrong) == Android.Hardware.Biometrics.BiometricCode.Success
                : manager.CanAuthenticate() == Android.Hardware.Biometrics.BiometricCode.Success;
        }
    }

    public async Task<bool> AuthenticateAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || Platform.CurrentActivity is not { } activity) return false;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var signal = new Android.OS.CancellationSignal();
            cancellationToken.Register(signal.Cancel);
            var builder = new Android.Hardware.Biometrics.BiometricPrompt.Builder(activity)
                .SetTitle(reason)
                .SetSubtitle("Utilisez votre empreinte ou votre visage")
                .SetNegativeButton("Utiliser le PIN", activity.MainExecutor!, new NegativeListener(completion));
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                builder.SetAllowedAuthenticators((int)Android.Hardware.Biometrics.BiometricManagerAuthenticators.BiometricStrong);
            var prompt = builder.Build();
            prompt.Authenticate(signal, activity.MainExecutor!, new AuthenticationCallback(completion));
        });
        return await completion.Task.ConfigureAwait(false);
    }

    private sealed class AuthenticationCallback(TaskCompletionSource<bool> completion) : Android.Hardware.Biometrics.BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(Android.Hardware.Biometrics.BiometricPrompt.AuthenticationResult? result) => completion.TrySetResult(true);
        public override void OnAuthenticationError(Android.Hardware.Biometrics.BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString) => completion.TrySetResult(false);
        public override void OnAuthenticationFailed() { }
    }

    private sealed class NegativeListener(TaskCompletionSource<bool> completion) : Java.Lang.Object, Android.Content.IDialogInterfaceOnClickListener
    {
        public void OnClick(Android.Content.IDialogInterface? dialog, int which) => completion.TrySetResult(false);
    }
#else
    public bool IsAvailable => false;
    public Task<bool> AuthenticateAsync(string reason, CancellationToken cancellationToken = default) => Task.FromResult(false);
#endif
}
#pragma warning restore CA1416, CA1422
