using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using DeluxeJournal.Events;
using DeluxeJournal.Framework.Events;

namespace DeluxeJournal.Patching
{
    /// <summary>Patches for <see cref="Stats"/>.</summary>
    internal class StatsPatch : PatchBase<StatsPatch>
    {
        private EventManager EventManager { get; }

        public StatsPatch(EventManager eventManager, IMonitor monitor) : base(monitor)
        {
            EventManager = eventManager;
            Instance = this;
        }

        /// <remarks>
        /// Only called on <see cref="Game1.stats"/> of the player that killed the monster, either directly or,
        /// for a farmhand whose kill was processed by the host, when receiving the host's message.
        /// </remarks>
        private static void Postfix_monsterKilled(string name)
        {
            try
            {
                Instance.EventManager.MonsterKilled.Raise(Game1.player, new MonsterKilledEventArgs(Game1.player, name));
            }
            catch (Exception ex)
            {
                Instance.LogError(ex, nameof(Postfix_monsterKilled));
            }
        }

        public override void Apply(Harmony harmony)
        {
            Patch(harmony,
                original: AccessTools.Method(typeof(Stats), nameof(Stats.monsterKilled)),
                postfix: new HarmonyMethod(typeof(StatsPatch), nameof(Postfix_monsterKilled))
            );
        }
    }
}
