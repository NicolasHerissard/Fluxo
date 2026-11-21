using Android.App;
using Android.Content;
using Android.Provider;
using AndroidX.Fragment.App;
using JavaFile = Java.IO.File;
using AndroidXFileProvider = AndroidX.Core.Content.FileProvider;

namespace Finance.Android17.Maui;

internal static class CameraCapture
{
    public static async Task<string?> CapturePhotoAsync()
    {
        // L'application déclare la permission CAMERA : Android refuse alors de lancer l'appareil photo (SecurityException,
        // fermeture de l'app) tant qu'elle n'a pas été accordée à l'exécution.
        var status = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.Camera>();
        if (status != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
            status = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Camera>();
        if (status != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
            throw new InvalidOperationException("Autorisez l’appareil photo pour Fluxo dans les réglages d’Android pour scanner un ticket.");

        var activity = Platform.CurrentActivity;
        if (activity is not FragmentActivity fragmentActivity)
            return null;

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fragment = new CameraCaptureFragment(completion);
        fragmentActivity.SupportFragmentManager.BeginTransaction().Add(fragment, CameraCaptureFragment.FragmentTag).Commit();
        return await completion.Task;
    }
}

internal sealed class CameraCaptureFragment : AndroidX.Fragment.App.Fragment
{
    internal const string FragmentTag = "finance.camera.capture";
    internal const string CaptureDirectory = "camera";
    private const int RequestCode = 1741;
    private readonly TaskCompletionSource<string?> _completion;
    private string? _path;

    public CameraCaptureFragment(TaskCompletionSource<string?> completion) => _completion = completion;

    // Requis par Android pour recréer le fragment si l'application est recréée pendant la prise de photo.
    public CameraCaptureFragment() : this(new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously)) { }

    public override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var activity = Activity ?? throw new InvalidOperationException("Activité Android indisponible.");
        // Un sous-dossier dédié : le FileProvider n'expose que lui (voir file_paths.xml), pas tout le cache de l'application.
        var directory = new JavaFile(activity.CacheDir, CaptureDirectory);
        directory.Mkdirs();
        var file = new JavaFile(directory, $"receipt-{Guid.NewGuid():N}.jpg");
        _path = file.AbsolutePath;
        var uri = AndroidXFileProvider.GetUriForFile(activity, $"{activity.PackageName}.fileprovider", file);
        var intent = new Intent(MediaStore.ActionImageCapture);
        intent.PutExtra(MediaStore.ExtraOutput, uri);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        try { StartActivityForResult(intent, RequestCode); }
        catch (Exception ex)
        {
            _completion.TrySetException(new InvalidOperationException("Impossible d’ouvrir l’appareil photo : " + ex.Message));
            ParentFragmentManager.BeginTransaction().Remove(this).CommitAllowingStateLoss();
        }
    }

    public override void OnActivityResult(int requestCode, int resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != RequestCode) return;

        var path = _path;
        if (resultCode == (int)Result.Ok && path is not null && System.IO.File.Exists(path))
            _completion.TrySetResult(path);
        else
        {
            if (path is not null && System.IO.File.Exists(path)) System.IO.File.Delete(path);
            _completion.TrySetResult(null);
        }

        ParentFragmentManager.BeginTransaction().Remove(this).CommitAllowingStateLoss();
    }
}
