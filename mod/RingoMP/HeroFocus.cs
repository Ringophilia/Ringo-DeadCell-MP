using System.Reflection;
using System.Runtime.CompilerServices;
using dc.en;
using dc.en.mob;
using dc.pr;
using dc.tool.skill;
using ModCore.Modules;

namespace RingoMP
{
    /// Mobs and bosses read `game.hero` in about 500 places. Rather than patching
    /// each, `game.hero` points at the hero a mob is focused on while that mob's
    /// code runs. Its skills run as separate processes, so those are wrapped too.
    public sealed class HeroFocus
    {
        const long ReconsiderMs = 2000;

        delegate void MobOrig(Mob self);

        static readonly string[] BossMethods = ["preUpdate", "fixedUpdate", "postUpdate", "behaviourAi"];
        static readonly string[] MobMethods = ["fixedUpdate", "behaviourAi"];
        const string MobClass = "en.Mob";
        const string BossNamespace = "dc.en.mob.boss";
        const string HaxePackagePrefix = "dc.";

        sealed class Choice
        {
            public Hero? Hero;
            public long Until;
        }

        readonly CoopSession session;
        readonly ConditionalWeakTable<Mob, Choice> choices = new();

        /// Control for performance tests: hooks stay in, the swap does not happen.
        static readonly bool Disabled = Environment.GetEnvironmentVariable("RINGO_NO_FOCUS") == "1";

        public HeroFocus(CoopSession session)
        {
            this.session = session;
        }

        public int Install()
        {
            var hooks = HashlinkHooks.Instance;
            var handler = (Action<MobOrig, Mob>)Around;
            var count = 0;
            foreach (var type in typeof(Mob).Assembly.GetTypes().Where(IsBossPart))
            {
                var name = type.FullName![HaxePackagePrefix.Length..];
                foreach (var method in BossMethods.Where(m => Declares(type, m)))
                {
                    hooks.CreateHook(name, method, handler, true);
                    count++;
                }
            }
            foreach (var method in MobMethods)
            {
                hooks.CreateHook(MobClass, method, handler, true);
                count++;
            }
            Hook_OldMobSkill.update += (orig, self) =>
            {
                if (self.owner is Mob mob)
                {
                    Run(mob, () => orig(self));
                }
                else
                {
                    orig(self);
                }
            };
            return count;
        }

        static bool IsBossPart(Type type) =>
            type.IsSubclassOf(typeof(Mob)) && !type.IsAbstract
            && (type.IsSubclassOf(typeof(Boss)) || type.Namespace?.StartsWith(BossNamespace) == true);

        static bool Declares(Type type, string method) =>
            type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Type.EmptyTypes) != null;

        void Around(MobOrig orig, Mob self) => Run(self, () => orig(self));

        void Run(Mob mob, Action body)
        {
            var game = dc.pr.Game.Class.ME;
            if (Disabled || game == null || !session.Remotes.Values.Any(r => ReferenceEquals(r._level, mob._level)))
            {
                body();
                return;
            }
            var saved = game.hero;
            game.hero = Choose(mob, saved);
            try
            {
                body();
            }
            finally
            {
                game.hero = saved;
            }
        }

        /// The nearest standing hero, held for a moment so a mob does not flicker
        /// between two players standing at the same distance.
        Hero Choose(Mob mob, Hero main)
        {
            var choice = choices.GetOrCreateValue(mob);
            var now = Environment.TickCount64;
            if (choice.Hero == null || now >= choice.Until || choice.Hero.destroyed)
            {
                Hero best = main;
                foreach (var remote in session.Remotes.Values)
                {
                    if (!remote.Downed && !remote.Away && ReferenceEquals(remote._level, mob._level) && Distance(mob, remote) < Distance(mob, best))
                    {
                        best = remote;
                    }
                }
                choice.Hero = best;
                choice.Until = now + ReconsiderMs;
            }
            return choice.Hero;
        }

        static int Distance(Mob mob, Hero hero) =>
            System.Math.Abs(mob.cx - hero.cx) + System.Math.Abs(mob.cy - hero.cy);
    }
}
