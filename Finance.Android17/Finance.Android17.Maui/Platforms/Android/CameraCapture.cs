using Android.App;
using Android.Content;
using Android.Provider;
using AndroidX.Fragment.App;
using JavaFile = Java.IO.File;
using AndroidXFileProvider = AndroidX.Core.Content.FileProvider;

namespace Finance.Android17.Maui;

internal static class CameraCapture
{
    public static Task<string?> CapturePhotoAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity is not FragmentActivity fragmentActivity)
            return Task.FromResult<string?>(null);

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fragment = new CameraCaptureFragment(completion);
        fragmentActivity.SupportFragmentManager.BeginTransaction().Add(fragment, CameraCaptureFragment.FragmentTag).Commit();
        return completion.Task;
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
        StartActivityForResult(intent, RequestCode);
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
