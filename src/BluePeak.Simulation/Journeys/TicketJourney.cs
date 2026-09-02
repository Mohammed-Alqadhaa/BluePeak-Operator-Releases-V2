using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

internal static class TicketJourney
{
    public static Journey Create() => new()
    {
        Id = "journey.ticket",
        Name = "Service desk contact",
        Discipline = "Service Desk",
        Question = "A user says the portal is broken. Is this a new fault, or one we already own?",
        Summary = "The most expensive mistake at first line is diagnosing a symptom that already has a cause. "
                + "This journey follows one contact from intake to the moment it is correctly attached to an "
                + "existing major incident, and shows what would have been wasted by treating it as new.",
        TicketId = "TKT-88223",
        IncidentId = "INC-4412",
        Weight = Severity.High,
        ModulePath = new[] { "mod.ingress", "mod.control", "mod.workload", "mod.resolution", "mod.evidence" },
        Stages = new[]
        {
            B.Stage("s0", "Contact arrives", StageKind.Establish, 4.0,
                "One person cannot sign in. At this moment the machine looks entirely normal from the outside.",
                B.Wide(24, 15, 10.2f),
                links: new[] { B.Bus("mod.ingress", "portal request", HealthState.Healthy, 0.7f, 1.2f) },
                detail: new[]
                {
                    B.Row("Ticket", "TKT-88223 — cannot sign in to customer portal, redirect loop"),
                    B.Row("Requester", "J. Whitfield, Customer Service"),
                    B.Row("Channel", "Self-service portal with a screen recording attached"),
                    B.Row("Priority as raised", "Normal"),
                    B.Row("First-line question", "Is this a device problem, an account problem, or a service problem?")
                }),

            B.Stage("s0b", "Open the machine", StageKind.Disassemble, 3.4,
                "Rather than guessing, the operator opens the estate along the path the user described: ingress, then the application, then whatever the application needs.",
                B.Wide(44, 23, 11.8f, 0.7f, 40),
                poses: B.Bloom(0.56f, 0.45f, 0.7f,
                    ("mod.ingress", ModulePose.Extracted(1.0f, 0.1f, 0, 0, 0.45f, 1f)),
                    ("mod.workload", ModulePose.Extracted(1.0f, 0.1f, 0, 0, 0.45f, 1f))),
                links: new[]
                {
                    B.Bus("mod.ingress", "portal request", HealthState.Healthy, 0.8f, 1.2f),
                    B.Bus("mod.workload", "sign-in", HealthState.Degraded, 0.8f, 0.6f),
                    B.Bus("mod.control", "open signals", HealthState.Critical, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Path to walk", "Ingress → application → identity → resolution"),
                    B.Row("First-line boundary", "Reproduce, classify, check for an existing cause. Do not diagnose infrastructure."),
                    B.Row("Time budget", "6 minutes before escalation is the cheaper option"),
                    B.Row("Trap to avoid", "Treating an intermittent symptom as a device fault", HealthState.Degraded)
                }),

            B.Stage("s1", "Reproduce before you theorise", StageKind.Inspect, 4.4,
                "The ingress module opens. The request is admitted normally and the portal shell is served, which is why the user believes the site is up.",
                B.Look("mod.ingress", 1.5f, 6.2f, 10),
                poses: B.Focus(0.22f, ("mod.ingress", ModulePose.Extracted(1.5f, 1f, -16, 6))),
                links: new[] { B.Bus("mod.ingress", "HTTP 200 shell", HealthState.Healthy, 1f, 1.4f) },
                detail: new[]
                {
                    B.Row("Protocol", "HTTPS"),
                    B.Row("Expected", "Portal shell returns HTTP 200", HealthState.Healthy),
                    B.Row("Actual", "Portal shell returns HTTP 200 in 41 ms", HealthState.Healthy),
                    B.Row("Reproduced", "Two browsers, one managed device, one incognito session"),
                    B.Row("Ruled out", "Device, browser profile, cached credentials", HealthState.Healthy),
                    B.Row("Not ruled out", "Anything behind the sign-in redirect", HealthState.Degraded)
                },
                focus: "mod.ingress", verdict: HealthState.Healthy, service: "net-edge"),

            B.Stage("s2", "Ask the control ring first", StageKind.Trace, 4.6,
                "Before diagnosing anything, the operator asks what is already known. The control ring holds open signals for the exact path this ticket describes.",
                B.Look("mod.control", 1.7f, 6.4f, 16, -18, 34),
                poses: B.Focus(0.18f, ("mod.control", ModulePose.Extracted(1.7f, 1f, -18, 8))),
                links: new[]
                {
                    B.Bus("mod.control", "open signals", HealthState.Critical, 1f, 1.6f),
                    B.Data("mod.control", "mod.workload", "app-web degraded", HealthState.Degraded, 0.8f, 1.2f),
                    B.Data("mod.control", "mod.resolution", "svc-dns critical", HealthState.Critical, 0.9f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Open major incident", "INC-4412 — declared 61 minutes ago", HealthState.Critical),
                    B.Row("Declared blast radius", "app-api, app-web, app-collab, idp-fed, svc-dns"),
                    B.Row("This ticket's service", "app-web — inside the declared blast radius", HealthState.Critical),
                    B.Row("Matching contacts", "3 other tickets with the same signature in 49 minutes"),
                    B.Row("Correct action", "Attach to the incident. Do not open a parallel diagnosis.", HealthState.Healthy)
                },
                focus: "mod.control", verdict: HealthState.Critical, service: "ctl-telemetry"),

            B.Stage("s3", "What the ticket is actually seeing", StageKind.Inspect, 4.6,
                "The workload module opens far enough to show why sign-in fails while everything else on the page works.",
                B.Look("mod.workload", 1.6f, 6.2f, 9),
                poses: B.Focus(0.18f,
                    ("mod.workload", ModulePose.Extracted(1.6f, 1f, -18, 4)),
                    ("mod.control", ModulePose.Extracted(0.8f, 0.5f, 0, 0, 0f, 0.5f))),
                links: new[]
                {
                    B.Bus("mod.workload", "sign-in redirect", HealthState.Degraded, 1f, 0.5f),
                    B.Trust("mod.workload", "mod.identity", "SAML AuthnRequest", HealthState.Degraded, 0.8f, 0.4f)
                },
                detail: new[]
                {
                    B.Row("Protocol", "SAML 2.0 redirect binding"),
                    B.Row("Expected", "Assertion consumed, session established", HealthState.Healthy),
                    B.Row("Actual", "Redirect returns without a session on roughly half of attempts", HealthState.Degraded),
                    B.Row("Why intermittent", "The identity service is serving from a cache that has not yet expired"),
                    B.Row("Consequence for first line", "Intermittent symptoms produce 'cannot reproduce' closures. This one is real."),
                    B.Row("Impact", "Every federated sign-in, not just this user", HealthState.Degraded)
                },
                focus: "mod.workload", verdict: HealthState.Degraded, service: "app-web"),

            B.Stage("s4", "The cause already has an owner", StageKind.Diagnose, 5.0,
                "The dependency chain reaches the same absent resolver zone that every other contact this hour has reached.",
                B.Look("mod.resolution", 1.9f, 5.8f, 7, -22, 32),
                poses: B.Focus(0.12f,
                    ("mod.resolution", ModulePose.Extracted(1.9f, 1f, 24, -5, 0.15f)),
                    ("mod.workload", ModulePose.Extracted(0.9f, 0.4f, 0, 0, 0f, 0.5f))),
                links: new[]
                {
                    B.Dep("mod.workload", "mod.resolution", "shared dependency", HealthState.Critical, 1f, 0.3f),
                    B.Bus("mod.resolution", "SERVFAIL", HealthState.Critical, 1f, 0.15f)
                },
                detail: new[]
                {
                    B.Row("First failure", "Internal DNS resolvers — svc-dns", HealthState.Critical),
                    B.Row("Owned by", "INC-4412, commander R. Achebe"),
                    B.Row("Correction in flight", "CHG-2304 awaiting Emergency CAB"),
                    B.Row("This ticket's role", "Impact evidence for the incident, not an independent investigation"),
                    B.Row("Avoided work", "Password reset, profile rebuild, browser reinstall, escalation to the application team", HealthState.Healthy),
                    B.Row("Estimated waste avoided", "40-60 minutes per contact, across 4 contacts")
                },
                focus: "mod.resolution", verdict: HealthState.Critical, evidence: "EV-1002", service: "svc-dns"),

            B.Stage("s5", "Set expectations with the requester", StageKind.Act, 4.2,
                "The ticket is linked, the priority is corrected and the requester is told what is happening in terms they can use.",
                B.Wide(70, 22, 11.2f, 0.6f, 40),
                poses: B.Bloom(0.5f, 0.3f, 0.4f,
                    ("mod.workload", ModulePose.Extracted(1.2f, 0.5f, 0, 0, 0f, 0.95f)),
                    ("mod.resolution", ModulePose.Extracted(1.2f, 0.5f, 0, 0, 0f, 0.95f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.workload", "incident linkage", HealthState.Degraded, 0.9f, 1.2f),
                    B.Dep("mod.workload", "mod.resolution", "awaiting correction", HealthState.Critical, 0.8f, 0.4f)
                },
                detail: new[]
                {
                    B.Row("Ticket state", "In progress, linked to INC-4412"),
                    B.Row("Priority", "Raised from Normal to High — inside a critical incident blast radius"),
                    B.Row("Assignment", "Held at second line. Not escalated to the application team."),
                    B.Row("Requester update", "A shared component used for sign-in is failing. It is being corrected under an emergency change. No action is needed on your device."),
                    B.Row("Workaround offered", "None that is honest. A false workaround costs more than a clear wait.", HealthState.Degraded),
                    B.Row("SLA", "Measured against the incident, not the individual contact")
                }),

            B.Stage("s6", "Verify with the person who reported it", StageKind.Verify, 4.4,
                "Resolution is confirmed on the original symptom, by the original requester, on the original device.",
                B.Between("mod.control", "mod.workload", 8.6f, 20, 40),
                poses: B.Focus(0.22f,
                    ("mod.control", ModulePose.Extracted(1.4f, 1f, -14, 8)),
                    ("mod.workload", ModulePose.Extracted(0.9f, 0.4f, 0, 0, 0f, 0.85f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.workload", "synthetic sign-in", HealthState.Healthy, 1f, 1.8f),
                    B.Bus("mod.workload", "session established", HealthState.Healthy, 1f, 1.6f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Synthetic federated sign-in succeeds 10 times consecutively", HealthState.Healthy),
                    B.Row("Check 2", "Requester confirms sign-in on their own device", HealthState.Healthy),
                    B.Row("Check 3", "No further contacts with the same signature for 30 minutes", HealthState.Healthy),
                    B.Row("Not accepted", "Closing on the incident's resolution without contacting the requester", HealthState.Degraded),
                    B.Row("State", "Resolved, pending closure window")
                },
                focus: "mod.control", verdict: HealthState.Healthy),

            B.Stage("s7", "Record and close", StageKind.Reassemble, 4.6,
                "The contact is sealed with its linkage intact, so the next identical call is recognised in seconds rather than diagnosed again.",
                B.Wide(24, 15, 10.2f),
                links: new[]
                {
                    B.Data("mod.control", "mod.evidence", "attest", HealthState.Healthy, 0.9f, 1.2f),
                    B.Bus("mod.workload", "service", HealthState.Healthy, 0.8f, 1.4f)
                },
                detail: new[]
                {
                    B.Row("Sealed", "EV-1002 dependency walk, linked to both the ticket and the incident"),
                    B.Row("Knowledge", "Signature added: portal redirect loop plus healthy anonymous pages equals federation dependency failure"),
                    B.Row("Reusable", "Next matching contact is attached at first line without escalation", HealthState.Healthy),
                    B.Row("Authority", "Local operator record — impact evidence, not root cause authority", HealthState.Degraded)
                },
                verdict: HealthState.Healthy)
        }
    };
}
