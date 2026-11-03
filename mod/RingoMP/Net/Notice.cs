using dc.pr;
using ModCore.Utilities;

namespace RingoMP.Net
{
    /// A line above this player's hero, in the game's language.
    public static class Notice
    {
        public static void Say(Phrase phrase)
        {
            // Between levels a hero has nowhere to show it, and the game would crash trying.
            if (Game.Class.ME?.hero is { destroyed: false, _level: { destroyed: false } } hero)
            {
                hero.say(Translations.Get(phrase).AsHaxeString(), null, null, null);
            }
        }
    }
}
