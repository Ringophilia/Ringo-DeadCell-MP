using dc.ui;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace RingoMP
{
    /// One line of the lobby: what it says, a second line under it, and what choosing it does.
    /// The id names a line whose words change, so that the cursor stays on it.
    public sealed record LobbyEntry(string Label, string? Detail = null, Action? Choose = null, bool Heading = false, string? Id = null)
    {
        public string Key => Id ?? Label;
    }

    /// The lobby in the game's own options window, so it looks like the game and
    /// answers the mouse, the keyboard and the gamepad the way the options do.
    public class LobbyWindow : OptionsBase
    {
        const double ScrollerHeight = 1;

        // The game builds the first page while the window is still being made.
        [ThreadStatic] static Func<IReadOnlyList<LobbyEntry>>? opening;

        readonly Func<IReadOnlyList<LobbyEntry>> page;
        List<LobbyEntry> lines = [];

        LobbyWindow(Pause? pause, Func<IReadOnlyList<LobbyEntry>> page) : base(pause)
        {
            this.page = page;
            killOnBack = true;
            // Leaving the window asks which page it was on, and this one is the window's only page.
            curSection = new OptionsSection.AM_Main();
            mainSection = curSection;
            curWidgetId = -1;
            Refresh();
        }

        public static LobbyWindow Open(Pause? pause, Func<IReadOnlyList<LobbyEntry>> page)
        {
            opening = page;
            try
            {
                return new LobbyWindow(pause, page);
            }
            finally
            {
                opening = null;
            }
        }

        /// Builds the page again where it stands. The cursor stays on the line it was on,
        /// wherever that moved to, or else starts on something to choose, not on a line
        /// that only tells.
        public void Refresh()
        {
            var was = curWidgetId >= 0 && curWidgetId < lines.Count ? lines[curWidgetId].Key : null;
            var next = Entries().Where(e => !e.Heading).ToList();
            var at = next.FindIndex(e => e.Key == was);
            curWidgetId = at >= 0 ? at : System.Math.Max(0, next.FindIndex(e => e.Choose != null));
            setSection(curSection);
        }

        IReadOnlyList<LobbyEntry> Entries() => (page ?? opening)?.Invoke() ?? [];

        public override void buildCurSection()
        {
            title.text = Translations.Get(Phrase.MenuMultiplayer).AsHaxeString();
            createScroller(ScrollerHeight);
            var flow = scrollerFlow;
            var entries = Entries();
            lines = entries.Where(e => !e.Heading).ToList();
            foreach (var entry in entries)
            {
                if (entry.Heading)
                {
                    addSeparator(entry.Label.AsHaxeString(), flow);
                    continue;
                }
                var spacing = 0;
                var choose = entry.Choose;
                addSimpleWidget(entry.Label.AsHaxeString(), entry.Detail?.AsHaxeString(), new HlAction(() => choose?.Invoke()), ref spacing, flow);
            }
        }
    }
}
