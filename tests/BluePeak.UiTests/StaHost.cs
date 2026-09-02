using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace BluePeak.UiTests;

/// <summary>
/// A single STA thread hosting one WPF Application with the product's real resource
/// dictionaries loaded, so views are exercised exactly as they are at runtime rather than
/// against a stubbed theme. Shared by every UI test in the assembly.
/// </summary>
public sealed class StaHost : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private readonly ManualResetEventSlim _ready = new(false);

    public StaHost()
    {
        _thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            // Exactly the dictionaries App.xaml merges, in the same order.
            foreach (var source in new[]
                     {
                         "Design/Tokens.xaml", "Design/Icons.xaml", "Design/Typography.xaml",
                         "Design/Controls.xaml", "Design/Workspace.xaml"
                     })
            {
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/BluePeak.Operator;component/{source}", UriKind.Absolute)
                });
            }

            AddConverter(application, "HealthBrush", new BluePeak.App.Design.HealthBrushConverter());
            AddConverter(application, "SeverityBrush", new BluePeak.App.Design.SeverityBrushConverter());
            AddConverter(application, "BoolVis", new BluePeak.App.Design.BoolToVisibilityConverter());
            AddConverter(application, "NullVis", new BluePeak.App.Design.NullToVisibilityConverter());
            AddConverter(application, "CountVis", new BluePeak.App.Design.CountToVisibilityConverter());
            AddConverter(application, "Ago", new BluePeak.App.Design.AgoConverter());
            AddConverter(application, "Clock", new BluePeak.App.Design.ClockConverter());
            AddConverter(application, "EnumLabel", new BluePeak.App.Design.EnumLabelConverter());
            AddConverter(application, "Op", new BluePeak.App.Design.OpacityConverter());
            AddConverter(application, "FractionWidth", new BluePeak.App.Design.FractionWidthConverter());
            AddConverter(application, "GateBrush", new BluePeak.App.Design.GateBrushConverter());
            AddConverter(application, "ResultBrush", new BluePeak.App.Design.EvidenceResultBrushConverter());
            AddConverter(application, "AuthorityBrush", new BluePeak.App.Design.AuthorityBrushConverter());
            AddConverter(application, "Wash", new BluePeak.App.Design.WashBrushConverter());
            AddConverter(application, "IconLookup", new BluePeak.App.Design.IconLookupConverter());

            _dispatcher = Dispatcher.CurrentDispatcher;
            _ready.Set();
            Dispatcher.Run();
        });

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(30));
    }

    private static void AddConverter(Application application, string key, object converter) =>
        application.Resources[key] = converter;

    public Dispatcher Dispatcher => _dispatcher ?? throw new InvalidOperationException("STA host did not start");

    public T Run<T>(Func<T> action) => Dispatcher.Invoke(action);

    public void Run(Action action) => Dispatcher.Invoke(action);

    /// <summary>Lets the dispatcher drain queued work, including layout and render callbacks.</summary>
    public void Pump(int milliseconds = 60)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(8);
        }
    }

    public void Dispose()
    {
        try { _dispatcher?.InvokeShutdown(); } catch { /* shutting down */ }
    }
}

[CollectionDefinition("sta")]
public sealed class StaCollection : ICollectionFixture<StaHost>;
