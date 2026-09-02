namespace BluePeak.Simulation.Journeys;

/// <summary>
/// The registry of available journeys. Adding a scenario means adding one definition here;
/// nothing in the renderer, the transport or the inspector needs to change.
/// </summary>
public static class JourneyCatalog
{
    private static readonly Lazy<IReadOnlyList<Journey>> Lazy = new(() => new List<Journey>
    {
        TicketJourney.Create(),
        DnsJourney.Create(),
        AuthJourney.Create(),
        NetworkJourney.Create(),
        SocJourney.Create(),
        AutomationJourney.Create()
    });

    public static IReadOnlyList<Journey> All => Lazy.Value;

    public static Journey? ById(string? id) =>
        id is null ? null : All.FirstOrDefault(j => string.Equals(j.Id, id, StringComparison.OrdinalIgnoreCase));

    public static Journey Default => All[1];

    /// <summary>Journey attached to an incident, case or ticket, used for cross-workspace launch.</summary>
    public static Journey? ForSubject(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return All.FirstOrDefault(j =>
            string.Equals(j.IncidentId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(j.CaseId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(j.TicketId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(j.ChangeId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(j.RunbookId, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(j.DiagnosticPathId, id, StringComparison.OrdinalIgnoreCase));
    }
}
