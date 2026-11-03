using dc.pr;
using ModCore.Utilities;

namespace RingoMP.Net
{
    /// A line above this player's hero, in the game's language.
    public static class Notice
    {
        public static void Say(Phrase phrase)
        {
            if (Game.Class.ME?.hero is { } hero)
            {
                hero.say(Translations.Get(phrase).AsHaxeString(), null, null, null);
            }
        }
    }
}
