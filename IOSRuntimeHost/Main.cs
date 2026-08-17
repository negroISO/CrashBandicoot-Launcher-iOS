using Foundation;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using UIKit;

namespace CrashBandicoot.IOSRuntime;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
}

[Register("SceneDelegate")]
public sealed class SceneDelegate : UIWindowSceneDelegate
{
    public override UIWindow? Window { get; set; }
    UILabel _status = null!;
    PauseOverlay? _pauseOverlay;
    NSTimer? _padTimer;
    bool _lifecyclePaused;

    public override void WillConnect(UIScene scene, UISceneSession session,
                                     UISceneConnectionOptions options)
    {
        var windowScene = (UIWindowScene)scene;
        var bounds = UIScreen.MainScreen.Bounds;
        Window = new UIWindow(bounds) { WindowScene = windowScene };
        var surfaceView = new GlesView(bounds)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
            BackgroundColor = UIColor.FromRGB(8, 20, 30),
        };
        var label = new UILabel(bounds)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
            BackgroundColor = UIColor.Clear,
            Text = "Crash iOS host: UIKit scene ready",
            TextAlignment = UITextAlignment.Center,
            TextColor = UIColor.White,
        };
        surfaceView.AddSubview(label);
        Window.RootViewController = new UIViewController { View = surfaceView };
        Window.MakeKeyAndVisible();
        _status = label;
        surfaceView.Initialize();
        _padTimer = NSTimer.CreateRepeatingScheduledTimer(
            1.0 / 60.0, _ => IOSGamepad.Publish());
        NSRunLoop.Main.AddTimer(_padTimer, NSRunLoopMode.Common);
        IOSGamepad.StartPressed += HandlePauseController;
        IOSGamepad.CrossPressed += HandlePauseConfirm;
        IOSGamepad.SelectPressed += HandlePauseMap;
        StartGeneratedGameIfAvailable(new GlesSurface(surfaceView));
    }

    void SetStatus(string status)
    {
        BeginInvokeOnMainThread(() => _status.Text = status);
    }

    void HandlePauseController() =>
        BeginInvokeOnMainThread(TogglePauseOverlay);

    void HandlePauseConfirm()
    {
        if (_pauseOverlay is { } overlay)
            BeginInvokeOnMainThread(() => ClosePauseOverlay(overlay));
    }

    void HandlePauseMap()
    {
        if (_pauseOverlay is { } overlay)
            BeginInvokeOnMainThread(() =>
            {
                Runtime.RequestExitToMap();
                ClosePauseOverlay(overlay);
            });
    }

    void TogglePauseOverlay()
    {
        if (_pauseOverlay is { } activeOverlay)
        {
            ClosePauseOverlay(activeOverlay);
            return;
        }

        var window = Window;
        if (window is null) return;

        var overlay = new PauseOverlay(window.Bounds)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
        };
        overlay.ResumeRequested += () => ClosePauseOverlay(overlay);
        overlay.MapRequested += () =>
        {
            Console.WriteLine("[CrashIOSPause] map requested");
            Runtime.RequestExitToMap();
            ClosePauseOverlay(overlay);
        };
        window.AddSubview(overlay);
        _pauseOverlay = overlay;
        IOSGamepad.MenuActive = true;
        RecompOne.Runtime.Host.FrameClock.PauseTiming();
        IOSAudioOutput.Current?.PauseOutput();
    }

    void ClosePauseOverlay(PauseOverlay overlay)
    {
        IOSGamepad.MenuActive = false;
        RecompOne.Runtime.Host.FrameClock.ResumeTiming();
        IOSAudioOutput.Current?.ResumeOutput();
        overlay.RemoveFromSuperview();
        if (ReferenceEquals(_pauseOverlay, overlay))
            _pauseOverlay = null;
    }

    public override void DidDisconnect(UIScene scene)
    {
        IOSGamepad.StartPressed -= HandlePauseController;
        IOSGamepad.CrossPressed -= HandlePauseConfirm;
        IOSGamepad.SelectPressed -= HandlePauseMap;
        _padTimer?.Invalidate();
        _padTimer = null;
        base.DidDisconnect(scene);
    }

    public override void WillResignActive(UIScene scene)
    {
        PauseForLifecycle();
    }

    public override void DidEnterBackground(UIScene scene)
    {
        PauseForLifecycle();
    }

    public override void WillEnterForeground(UIScene scene)
    {
        ResumeFromLifecycle();
    }

    void PauseForLifecycle()
    {
        if (_lifecyclePaused) return;
        _lifecyclePaused = true;
        RecompOne.Runtime.Host.FrameClock.PauseTiming();
        IOSAudioOutput.Current?.PauseOutput();
    }

    void ResumeFromLifecycle()
    {
        if (!_lifecyclePaused) return;
        _lifecyclePaused = false;
        RecompOne.Runtime.Host.FrameClock.ResumeTiming();
        IOSAudioOutput.Current?.ResumeOutput();
    }

    void StartGeneratedGameIfAvailable(GlesSurface surface)
    {
        var cuePath = Program.CuePath;
#if CRASH_IOS_GENERATED
        if (!string.IsNullOrWhiteSpace(cuePath) && File.Exists(cuePath))
        {
            SetStatus("Starting generated AOT game...");
            Task.Run(() =>
            {
                try
                {
                    GeneratedGame.Run(cuePath, surface, SetStatus);
                }
                catch (Exception error)
                {
                    SetStatus($"Generated game failed: {error.GetBaseException().Message}");
                }
            });
            return;
        }
        SetStatus("CRASH_CUE_PATH is not set or readable");
#else
        SetStatus("UIKit scene ready; offline generated sources omitted");
#endif
    }
}

public static class Program
{
    internal static string? CuePath { get; private set; }

    public static void Main(string[] args)
    {
        var runtimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "runtime");
        AppPaths.SetRoot(runtimeRoot);
        AppPaths.EnsureCreated();
        ConfigManager.Load();
        // The iOS bring-up target optimizes for deterministic frame pacing and
        // correctness. The 4x desktop default is unnecessarily expensive on the
        // GLES-on-Metal compatibility path and amplifies frame-time spikes.
        ConfigManager.View.InternalResolution = 1;
        ConfigManager.View.TextureFilter = ViewConfig.TextureFilterOff;
        ConfigManager.View.TextureFilterStrength = 0f;
        ConfigManager.View.Dedither = false;
        ConfigManager.View.Dejitter = false;
        ConfigManager.View.IntegerScale = true;
        ConfigManager.View.PresentNearest = true;
        ConfigManager.View.Widescreen = false;
        var cuePath = Environment.GetEnvironmentVariable("CRASH_CUE_PATH");
        if (string.IsNullOrWhiteSpace(cuePath) || !File.Exists(cuePath))
        {
            var localDiscDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "disc");
            cuePath = Directory.Exists(localDiscDirectory)
                ? Directory.EnumerateFiles(localDiscDirectory, "*.cue").FirstOrDefault()
                : null;
        }
        CuePath = cuePath;
        if (!string.IsNullOrWhiteSpace(cuePath) && File.Exists(cuePath))
            ConfigManager.Game.CdPath = cuePath;
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
