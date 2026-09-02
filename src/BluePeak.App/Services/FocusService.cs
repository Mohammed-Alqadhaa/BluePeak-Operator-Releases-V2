using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BluePeak.App.Services;

public enum FocusKind { None, Service, Incident, Ticket, Case, Alert, Entity, Change, Runbook, Evidence, Journey }

/// <summary>
/// The subject the operator is currently reasoning about. This is what makes the product
/// one platform rather than thirteen screens: moving between workspaces carries the subject,
/// so an incident selected in Overview is already selected in NOC, Diagnostics and Evidence.
/// </summary>
public sealed record FocusSubject(FocusKind Kind, string Id, string Label, string? Detail = null)
{
    public static readonly FocusSubject None = new(FocusKind.None, "", "");
    public bool IsSet => Kind != FocusKind.None && !string.IsNullOrEmpty(Id);
}

public sealed class FocusService : INotifyPropertyChanged
{
    private static readonly Lazy<FocusService> Instance = new(() => new FocusService());
    public static FocusService Current => Instance.Value;
    private FocusService() { }

    private FocusSubject _subject = FocusSubject.None;
    public FocusSubject Subject
    {
        get => _subject;
        private set
        {
            if (_subject == value) return;
            _subject = value;
            Raise();
            Raise(nameof(HasSubject));
            Changed?.Invoke(value);
        }
    }

    public bool HasSubject => _subject.IsSet;

    /// <summary>Trail of recently focused subjects, newest first. Bounded and de-duplicated.</summary>
    public List<FocusSubject> Trail { get; } = new();

    public event Action<FocusSubject>? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Set(FocusKind kind, string id, string label, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var subject = new FocusSubject(kind, id, label, detail);
        Trail.RemoveAll(s => s.Id == id && s.Kind == kind);
        Trail.Insert(0, subject);
        if (Trail.Count > 8) Trail.RemoveRange(8, Trail.Count - 8);
        Subject = subject;
        Raise(nameof(Trail));
    }

    public void Clear()
    {
        Subject = FocusSubject.None;
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? nameof(Subject)));
}
