using System.ComponentModel;
using System.Runtime.CompilerServices;
using BluePeak.App.Services;

namespace BluePeak.App.Shell;

/// <summary>Implemented by a workspace that can adopt the current subject when navigated to.</summary>
public interface IFocusAware
{
    void ApplyFocus(FocusSubject subject);
}

/// <summary>
/// Implemented by a workspace that owns expensive resources. The simulator uses this to
/// suspend and resume its render loop without losing playback position.
/// </summary>
public interface ILifecycleAware
{
    void OnActivated();
    void OnDeactivated();
}

public sealed class Navigator : INotifyPropertyChanged
{
    private static readonly Lazy<Navigator> Instance = new(() => new Navigator());
    public static Navigator Current => Instance.Value;
    private Navigator() { }

    private readonly Stack<string> _back = new();
    private WorkspaceDefinition? _workspace;

    public WorkspaceDefinition? Workspace
    {
        get => _workspace;
        private set
        {
            if (ReferenceEquals(_workspace, value)) return;
            if (_workspace?.IsRealised == true && _workspace.View is ILifecycleAware leaving)
                leaving.OnDeactivated();

            _workspace = value;
            Raise();
            Raise(nameof(View));
            Raise(nameof(CanGoBack));

            if (value?.View is ILifecycleAware entering) entering.OnActivated();
            Navigated?.Invoke(value);
        }
    }

    public System.Windows.Controls.UserControl? View => _workspace?.View;
    public bool CanGoBack => _back.Count > 0;

    public event Action<WorkspaceDefinition?>? Navigated;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Navigate(string workspaceId, bool recordHistory = true)
    {
        var target = WorkspaceCatalog.ById(workspaceId);
        if (target is null || ReferenceEquals(target, _workspace)) return;
        if (recordHistory && _workspace is not null) _back.Push(_workspace.Id);
        Workspace = target;
    }

    /// <summary>Navigate and carry a subject, so the destination opens already on the right row.</summary>
    public void NavigateWithSubject(string workspaceId, FocusKind kind, string id, string label, string? detail = null)
    {
        FocusService.Current.Set(kind, id, label, detail);
        Navigate(workspaceId);
        if (View is IFocusAware aware) aware.ApplyFocus(FocusService.Current.Subject);
    }

    /// <summary>Push the current subject into an already-open workspace without navigating.</summary>
    public void PushFocus()
    {
        if (View is IFocusAware aware) aware.ApplyFocus(FocusService.Current.Subject);
    }

    public void Back()
    {
        if (_back.Count == 0) return;
        var id = _back.Pop();
        Workspace = WorkspaceCatalog.ById(id);
        Raise(nameof(CanGoBack));
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? nameof(Workspace)));
}
