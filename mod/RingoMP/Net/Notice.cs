using dc;
using dc.pr;
using ModCore.Utilities;

namespace RingoMP.Net
{
    /// A line above this player's hero, in the game's language.
    public static class Notice
    {
        public static void Say(string chinese, string english)
        {
            if (Game.Class.ME?.hero is { } hero)
            {
                var text = Lang.Class.LANG?.ToString().StartsWith("zh") == true ? chinese : english;
                hero.say(text.AsHaxeString(), null, null, null);
            }
        }
    }
}
