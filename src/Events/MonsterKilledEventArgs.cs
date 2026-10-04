using StardewValley;

namespace DeluxeJournal.Events
{
    public class MonsterKilledEventArgs(Farmer player, string monsterName) : EventArgs
    {
        /// <summary>The player credited with the kill.</summary>
        public Farmer Player { get; } = player;

        /// <summary>The internal name of the monster. Also the key in the <c>Data/Monsters</c> asset.</summary>
        public string MonsterName { get; } = monsterName;
    }
}
