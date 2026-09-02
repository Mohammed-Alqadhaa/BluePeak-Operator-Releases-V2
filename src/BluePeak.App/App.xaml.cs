using System.Windows;

namespace BluePeak.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A capture flag lets the harness drive deterministic screenshots of every workspace.
        if (e.Args.Contains("--capture"))
        {
            var index = Array.IndexOf(e.Args, "--capture");
            string outDir = index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] : "captures";
            Tools.CaptureRunner.Schedule(this, outDir, e.Args.Contains("--capture-exit"));
        }
    }
}
