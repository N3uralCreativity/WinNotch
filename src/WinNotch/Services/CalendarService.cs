using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinNotch.Models;

namespace WinNotch.Services;

/// <summary>
/// Provides calendar events. Currently reads from Outlook COM if available,
/// otherwise returns an empty list. Can be extended for Microsoft Graph, .ics files, etc.
/// </summary>
public class CalendarService
{
    public event Action? EventsUpdated;

    public List<CalendarEvent> TodayEvents { get; private set; } = new();

    private System.Windows.Threading.DispatcherTimer? _refreshTimer;

    public void Initialize()
    {
        RefreshEvents();

        // Refresh every 5 minutes
        _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _refreshTimer.Tick += (_, _) => RefreshEvents();
        _refreshTimer.Start();
    }

    public void RefreshEvents()
    {
        TodayEvents = FetchOutlookEvents();
        EventsUpdated?.Invoke();
    }

    private static List<CalendarEvent> FetchOutlookEvents()
    {
        try
        {
            // Try Outlook COM interop (works if Outlook is installed)
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType == null) return new List<CalendarEvent>();

            dynamic outlook = Activator.CreateInstance(outlookType)!;
            dynamic ns = outlook.GetNamespace("MAPI");
            // olFolderCalendar = 9
            dynamic calFolder = ns.GetDefaultFolder(9);
            dynamic items = calFolder.Items;
            items.Sort("[Start]");
            items.IncludeRecurrences = true;

            var now = DateTime.Now;
            var endOfDay = now.Date.AddDays(1).AddSeconds(-1);

            // Restrict to today's events
            string filter = $"[Start] >= '{now:g}' AND [Start] <= '{endOfDay:g}'";
            dynamic restricted = items.Restrict(filter);

            var events = new List<CalendarEvent>();
            foreach (dynamic item in restricted)
            {
                try
                {
                    events.Add(new CalendarEvent
                    {
                        Title = item.Subject ?? "Untitled",
                        Start = item.Start,
                        End = item.End,
                        IsAllDay = item.AllDayEvent,
                        CalendarColor = "#4A90D9"
                    });
                }
                catch { }
                if (events.Count >= 5) break; // Limit to 5 events
            }
            return events;
        }
        catch
        {
            return new List<CalendarEvent>();
        }
    }
}
