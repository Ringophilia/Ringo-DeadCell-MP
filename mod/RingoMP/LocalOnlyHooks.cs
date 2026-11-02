using dc.en;
using dc.en.inter;
using dc.hxd;

namespace RingoMP
{
    /// These Hero methods speak to the person at this machine: the HUD, screen
    /// shake, slow motion, gamepad rumble, keyboard lights. A remote hero has no
    /// such person here. They are final in Haxe, so RemoteHero cannot override them.
    public static class LocalOnlyHooks
    {
        public static void Install()
        {
            // These move the whole game to another level; a remote hero must not set them off.
            Hook_FallToLevelTrigger.trigger += (orig, self, by) => { if (by is not RemoteHero) orig(self, by); };
            Hook_HiddenTrigger.trigger += (orig, self, by) => { if (by is not RemoteHero) orig(self, by); };
            // A virtual pad has no device behind it, and rumble reads the device unchecked.
            Hook_Pad.rumble += (orig, self, strength, seconds) => { if (self.d != null) orig(self, strength, seconds); };
            Hook_Hero.damageFeedback += (orig, self, a, ratio) => { if (self is not RemoteHero) orig(self, a, ratio); };
            Hook_Hero.updateLifeBar += (orig, self) => { if (self is not RemoteHero) orig(self); };
            Hook_Hero.refreshInfectionHud += (orig, self) => { if (self is not RemoteHero) orig(self); };
            Hook_Hero.hudInitItems += (orig, self) => { if (self is not RemoteHero) orig(self); };
            Hook_Hero.hudOnItemPick += (orig, self, a, b, c) => { if (self is not RemoteHero) orig(self, a, b, c); };
            Hook_Hero.hudSetMoney += (orig, self, v) => { if (self is not RemoteHero) orig(self, v); };
            Hook_Hero.hudSetCells += (orig, self, v) => { if (self is not RemoteHero) orig(self, v); };
            Hook_Hero.hudSetSurvival += (orig, self, v, w) => { if (self is not RemoteHero) orig(self, v, w); };
            Hook_Hero.hudSetBrutality += (orig, self, v, w) => { if (self is not RemoteHero) orig(self, v, w); };
            Hook_Hero.hudSetTactic += (orig, self, v, w) => { if (self is not RemoteHero) orig(self, v, w); };
            Hook_Hero.hudUpdateKeyStock += (orig, self) => { if (self is not RemoteHero) orig(self); };
            Hook_Hero.hudUpdatePerkStock += (orig, self) => { if (self is not RemoteHero) orig(self); };
        }
    }
}
