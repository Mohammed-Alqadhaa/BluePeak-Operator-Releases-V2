using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BluePeak.App.Services;

/// <summary>
/// Session preferences. Everything here changes observable behaviour; there are no settings
/// that exist only to fill the page.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    private static readonly Lazy<AppSettings> Instance = new(() => new AppSettings());
    public static AppSettings Current => Instance.Value;
    private AppSettings() { }

    private bool _reduceMotion;
    private bool _showFrameRate = true;
    private bool _linkFlow = true;
    private bool _idleDrift = true;
    private bool _autoPlayJourneys = true;

    /// <summary>Suppresses ambient motion. Journey choreography still runs; it carries meaning.</summary>
    public bool ReduceMotion
    {
        get => _reduceMotion;
        set => Set(ref _reduceMotion, value);
    }

    public bool ShowFrameRate
    {
        get => _showFrameRate;
        set => Set(ref _showFrameRate, value);
    }

    /// <summary>Animated flow markers along dependency links in the simulator.</summary>
    public bool LinkFlow
    {
        get => _linkFlow;
        set => Set(ref _linkFlow, value);
    }

    /// <summary>Slow camera drift while the simulator is idle on the journey list.</summary>
    public bool IdleDrift
    {
        get => _idleDrift;
        set => Set(ref _idleDrift, value);
    }

    public bool AutoPlayJourneys
    {
        get => _autoPlayJourneys;
        set => Set(ref _autoPlayJourneys, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Changed?.Invoke();
    }
}
