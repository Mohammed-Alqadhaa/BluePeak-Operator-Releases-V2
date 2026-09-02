using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.App.Workspaces;
using BluePeak.Domain;
using BluePeak.Simulation;
using Xunit;

namespace BluePeak.UiTests;

[Collection("sta")]
public class WorkspaceTests
{
    private readonly StaHost _host;
    public WorkspaceTests(StaHost host) => _host = host;

    [Fact]
    public void Every_workspace_constructs_without_a_markup_or_binding_failure()
    {
        foreach (var definition in WorkspaceCatalog.All)
        {
            var view = _host.Run(() => definition.View);
            Assert.NotNull(view);
            Assert.IsAssignableFrom<UserControl>(view);
        }
    }

    [Fact]
    public void Every_workspace_lays_out_at_the_shipped_window_size()
    {
        foreach (var definition in WorkspaceCatalog.All)
        {
            _host.Run(() =>
            {
                var view = definition.View;
                var host = new Border { Child = null };
                // Detach from any previous parent before measuring.
                if (view.Parent is Border previous) previous.Child = null;
                host.Child = view;
                host.Measure(new Size(1342, 838));
                host.Arrange(new Rect(0, 0, 1342, 838));
                host.UpdateLayout();

                Assert.True(view.ActualWidth > 200, $"{definition.Id} laid out at {view.ActualWidth} wide");
                Assert.True(view.ActualHeight > 200, $"{definition.Id} laid out at {view.ActualHeight} high");
                host.Child = null;
            });
        }
    }

    /// <summary>
    /// The width the window guarantees a workspace: the shell's declared minimum, less the
    /// navigation rail and the window border.
    /// </summary>
    private const double GuaranteedWidth = 1380 - 218 - 2;
    private const double GuaranteedHeight = 760 - 38 - 24 - 2;

    [Theory]
    [InlineData(1342, 838)]           // the shipped default window
    [InlineData(GuaranteedWidth, GuaranteedHeight)]
    [InlineData(2402, 1318)]          // a large display
    public void Every_workspace_fills_its_region_at_supported_sizes(double width, double height)
    {
        foreach (var definition in WorkspaceCatalog.All)
        {
            _host.Run(() =>
            {
                var view = definition.View;
                if (view.Parent is Border previous) previous.Child = null;
                var host = new Border { Child = view };
                host.Measure(new Size(width, height));
                host.Arrange(new Rect(0, 0, width, height));
                host.UpdateLayout();

                Assert.True(view.ActualWidth >= width - 1,
                    $"{definition.Id} is {view.ActualWidth} wide in a {width} region");
                Assert.True(view.ActualHeight >= height - 1,
                    $"{definition.Id} is {view.ActualHeight} high in a {height} region");
                host.Child = null;
            });
        }
    }

    [Fact]
    public void No_grid_column_is_pushed_outside_its_container_at_the_minimum_window_size()
    {
        // DesiredSize is clamped to whatever constraint Measure was given, so measuring at the
        // target size can never report an overflow, and measuring unconstrained just reports how
        // wide the prose would like to be. The precise signal is a Grid whose arranged columns
        // add up to more than the Grid itself: that is exactly when a fixed or MinWidth column
        // gets pushed off the right edge and clips. This is how the clipped SOC inspector was found.
        foreach (var definition in WorkspaceCatalog.All)
        {
            _host.Run(() =>
            {
                var view = definition.View;
                if (view.Parent is Border previous) previous.Child = null;
                var host = new Border { Child = view };
                host.Measure(new Size(GuaranteedWidth, GuaranteedHeight));
                host.Arrange(new Rect(0, 0, GuaranteedWidth, GuaranteedHeight));
                host.UpdateLayout();

                foreach (var grid in Descendants(view).OfType<Grid>())
                {
                    if (grid.ColumnDefinitions.Count < 2 || grid.ActualWidth < 1) continue;
                    double columns = grid.ColumnDefinitions.Sum(c => c.ActualWidth);
                    Assert.True(columns <= grid.ActualWidth + 1.0,
                        $"{definition.Id}: a grid is {grid.ActualWidth:0} px wide but its columns need "
                        + $"{columns:0} px, so the rightmost column is clipped");
                }

                host.Child = null;
            });
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    [Fact]
    public void Views_are_created_once_and_reused_across_navigation()
    {
        _host.Run(() =>
        {
            var noc = WorkspaceCatalog.ById("noc")!;
            var first = noc.View;
            Navigator.Current.Navigate("noc");
            Navigator.Current.Navigate("soc");
            Navigator.Current.Navigate("noc");
            Assert.Same(first, noc.View);
        });
    }

    [Fact]
    public void Navigating_the_whole_rail_repeatedly_is_stable()
    {
        _host.Run(() =>
        {
            for (int pass = 0; pass < 3; pass++)
                foreach (var definition in WorkspaceCatalog.All)
                {
                    Navigator.Current.Navigate(definition.Id);
                    Assert.Equal(definition.Id, Navigator.Current.Workspace?.Id);
                    Assert.NotNull(Navigator.Current.View);
                }
        });
    }

    [Fact]
    public void Back_navigation_returns_to_the_previous_workspace()
    {
        _host.Run(() =>
        {
            Navigator.Current.Navigate("overview");
            Navigator.Current.Navigate("noc");
            Navigator.Current.Navigate("evidence");
            Navigator.Current.Back();
            Assert.Equal("noc", Navigator.Current.Workspace?.Id);
            Navigator.Current.Back();
            Assert.Equal("overview", Navigator.Current.Workspace?.Id);
        });
    }

    [Theory]
    [InlineData("noc", FocusKind.Service, "svc-dns")]
    [InlineData("incidents", FocusKind.Incident, "INC-4412")]
    [InlineData("tickets", FocusKind.Ticket, "TKT-88223")]
    [InlineData("soc", FocusKind.Case, "CASE-118")]
    [InlineData("changes", FocusKind.Change, "CHG-2304")]
    [InlineData("automation", FocusKind.Runbook, "RB-021")]
    [InlineData("evidence", FocusKind.Evidence, "EV-1001")]
    [InlineData("diagnostics", FocusKind.Service, "DX-2205")]
    [InlineData("simulator", FocusKind.Journey, "journey.soc")]
    public void A_carried_subject_is_adopted_by_the_destination_workspace(string workspace, FocusKind kind, string id)
    {
        _host.Run(() =>
        {
            Navigator.Current.NavigateWithSubject(workspace, kind, id, id, "ui test");
            Assert.Equal(workspace, Navigator.Current.Workspace?.Id);
            Assert.Equal(id, FocusService.Current.Subject.Id);

            var view = Navigator.Current.View;
            Assert.IsAssignableFrom<IFocusAware>(view);
            // Adopting a subject must not throw and must leave the view usable.
            ((IFocusAware)view!).ApplyFocus(FocusService.Current.Subject);
        });
    }

    [Fact]
    public void The_service_desk_triage_decision_actually_changes_the_record()
    {
        _host.Run(() =>
        {
            var model = EstateService.Current.Model;
            var ticket = model.Tickets.First(t => t.Id == "TKT-88229");
            var before = ticket.State;
            Assert.Null(ticket.LinkedIncidentId);

            var view = (ServiceDeskView)WorkspaceCatalog.ById("servicedesk")!.View;
            view.ApplyFocus(new FocusSubject(FocusKind.Ticket, ticket.Id, ticket.Subject));

            // Treat as new fault: an unowned contact must gain an owner.
            var method = typeof(ServiceDeskView).GetMethod("NewFault_Click",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            method.Invoke(view, new object?[] { null, new RoutedEventArgs() });

            Assert.NotEqual(before, ticket.State);
            Assert.NotEqual("Unassigned", ticket.Assignee);
            Assert.Contains(ticket.Timeline, t => t.Text.Contains("independent fault"));
        });
    }
}
