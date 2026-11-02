using dc;
using dc.en;
using dc.en.hero;
using dc.pr;
using dc.tool;
using ModCore.Utilities;
using Serilog;

namespace RingoMP
{
    /// Owns the remote hero: its creation, its stay in the level, its way to the next one.
    public sealed class CoopSession
    {
        const int KeepAliveEveryFrames = 10;
        const int KeepAliveRangeCells = 40;
        const double KeepAliveFrames = 120;

        readonly ILogger log;
        bool insideHook;
        int frame;

        public RemoteHero? Remote { get; private set; }

        /// Set by a networked game before the remote hero spawns.
        public Action<int>? RemoteHit { get; set; }

        public CoopSession(ILogger logger)
        {
            log = logger;
            Hook_Hero.clean += OnHeroClean;
            Hook_Game.initHero += OnInitHero;
            LocalOnlyHooks.Install();
            log.Information("RINGO hero focus wrapped {n} mob methods", new HeroFocus(this).Install());
        }

        /// The main hero must be awake: before that the level is not active yet.
        public static bool CanSpawn(Game? game) =>
            game?.hero != null && game.curLevel != null && game.hero.awake && game.hero.cx >= 0;

        public RemoteHero Spawn(Game game)
        {
            var main = game.hero;
            var remote = new RemoteHero(game, new VirtualController());
            remote.wakeup(game.curLevel, main.cx, main.cy);
            remote.controller = remote.Input.Access;
            remote.isAlwaysUpdated = true;
            remote.ReportHit = RemoteHit;
            Remote = remote;
            log.Information("RINGO remote hero spawned at {x},{y}", remote.cx, remote.cy);
            return remote;
        }

        /// The game that held the remote hero is gone.
        public void Forget() => Remote = null;

        public static void Give(Hero hero, string itemId)
        {
            var item = InventItem.Class.fromItem.Invoke(itemId.AsHaxeString(), null);
            hero.inventory.add(item);
            hero.inventory.equip(item);
            bool updateHud = false, duringInit = false, duringTransform = false;
            hero.onEquipedItemsChange(ref updateHud, ref duringInit, ref duringTransform);
        }

        public void Update()
        {
            var remote = Remote;
            if (remote == null)
            {
                return;
            }
            if (++frame % KeepAliveEveryFrames == 0 && remote._level != null)
            {
                KeepNearbyAlive(remote);
            }
        }

        /// The game freezes whatever is off the local screen, but the remote hero
        /// may be fighting there. A recent on-screen time keeps those entities running.
        static void KeepNearbyAlive(RemoteHero remote)
        {
            var entities = remote._level.entities;
            for (var i = entities.length - 1; i >= 0; i--)
            {
                if (entities.getDyn(i) is Entity e
                    && System.Math.Abs(e.cx - remote.cx) < KeepAliveRangeCells
                    && System.Math.Abs(e.cy - remote.cy) < KeepAliveRangeCells)
                {
                    e.onScreenRecent = KeepAliveFrames;
                }
            }
        }

        void OnHeroClean(Hook_Hero.orig_clean orig, Hero self)
        {
            orig(self);
            if (!insideHook && Remote is { } remote && !ReferenceEquals(self, remote)
                && ReferenceEquals(self, Game.Class.ME?.hero) && remote._level != null)
            {
                insideHook = true;
                remote.clean();
                insideHook = false;
                log.Information("RINGO remote hero left its level");
            }
        }

        /// The game can only place `game.hero`, so the remote hero stands in for one call.
        Hero OnInitHero(Hook_Game.orig_initHero orig, Game self, Level lvl, int cx, int cy, UsableBody from, bool fromDeadBody, Level oldLevel)
        {
            var result = orig(self, lvl, cx, cy, from, fromDeadBody, oldLevel);
            if (!insideHook && Remote is { } remote && remote._level == null)
            {
                insideHook = true;
                var main = self.hero;
                self.hero = remote;
                orig(self, lvl, cx, cy, null, true, oldLevel);
                self.hero = main;
                insideHook = false;
                log.Information("RINGO remote hero entered level at {x},{y}", remote.cx, remote.cy);
            }
            return result;
        }
    }
}
