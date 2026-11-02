using dc.cine;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// What the link goes through. The host starts the run for both. The link is cut
    /// and comes back; it is cut again while the host moves on alone, and the client
    /// catches up; then it is cut for good and each player goes on alone.
    public sealed class LinkScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const double SyncSeconds = 3;
        const double LinkBackSeconds = 25;
        const double GiveUpSeconds = 6;
        const double CutSettleSeconds = 1;

        SteamTransport Link => (SteamTransport)Net.Transport;

        // Only the host's game starts by itself: its start must carry the client along.
        protected override bool CanStart => !IsClient && base.CanStart;

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            Check("the host started the client's run", CurrentGame != null);
            foreach (var step in StillInStep("at the start"))
            {
                yield return step;
            }

            if (!IsClient)
            {
                Link.Cut(forGood: false);
            }
            yield return Expect("this game saw the link go down", () => Net.LinkDown || !Net.Transport.Connected, LinkBackSeconds);
            yield return Expect("the link came back", () => Net.Transport.Connected && !Net.LinkDown, LinkBackSeconds);
            yield return Expect("the levels still match", () => Net.LevelsMatch, LinkBackSeconds);
            foreach (var step in StillInStep("after the first cut"))
            {
                yield return step;
            }

            if (!IsClient)
            {
                var next = Doors()[0].destLevel;
                Link.Cut(forGood: false);
                LevelTransition.Class.@goto.Invoke(next);
            }
            yield return NewLevel();
            Arrived();
            foreach (var step in StillInStep("after catching up"))
            {
                yield return step;
            }

            Net.GiveUpSeconds = GiveUpSeconds;
            yield return Seconds(CutSettleSeconds);
            if (IsClient)
            {
                Link.Cut(forGood: true);
            }
            yield return Expect("gone on alone", () => Net.PeerLeft && Session.Remote == null, LinkBackSeconds);
            Check("this game owns its mobs now", Net.Role == NetRole.Host);
            Check("no mob is left a puppet", Net.Registry!.Mobs.All(m => m.destroyed || !m.aiLocked()));
            yield return Seconds(SyncSeconds);
            Check("the hero is alive and well", Main.life > 0 && !Main.destroyed);
        }

        IEnumerable<Func<bool>> StillInStep(string when)
        {
            var heroPackets = Net.HeroPackets;
            var mobPackets = Net.MobPackets;
            yield return Seconds(SyncSeconds);
            Log.Information("RINGO {role} {w}: heroPackets+{h} mobPackets+{m} mobErr={e:0.00}",
                RoleName, when, Net.HeroPackets - heroPackets, Net.MobPackets - mobPackets, Net.LastMobError);
            Check($"hero state flows {when}", Net.HeroPackets > heroPackets);
            Check($"mob state flows {when}", !IsClient || Net.MobPackets > mobPackets);
        }
    }
}
