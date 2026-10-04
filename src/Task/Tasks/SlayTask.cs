using DeluxeJournal.Events;

using static DeluxeJournal.Task.TaskParameterAttribute;

namespace DeluxeJournal.Task.Tasks
{
    internal class SlayTask : TaskBase
    {
        public class Factory : TaskFactory
        {
            [TaskParameter(TaskParameterNames.Monster, TaskParameterTag.MonsterList)]
            public IList<string>? MonsterNames { get; set; }

            [TaskParameter(TaskParameterNames.Count, TaskParameterTag.Count, Constraints = Constraint.GE1)]
            public int Count { get; set; } = 1;

            public override SmartIconFlags EnabledSmartIcons => SmartIconFlags.Monster;

            public override bool EnableSmartIconCount => true;

            protected override void InitializeInternal(ITask task)
            {
                if (task is SlayTask slayTask)
                {
                    MonsterNames = slayTask.MonsterNames;
                    Count = slayTask.MaxCount;
                }
            }

            protected override ITask? CreateInternal(string name)
            {
                return MonsterNames != null && MonsterNames.Count > 0 ? new SlayTask(name, MonsterNames, Count) : null;
            }
        }

        /// <summary>
        /// The internal names of the monsters to be slain, used as the keys in the <c>Data/Monsters</c> asset.
        /// Holds all of the targets when created from an Adventurer's Guild monster eradication goal.
        /// </summary>
        public IList<string> MonsterNames { get; set; }

        /// <summary>Serialization constructor.</summary>
        public SlayTask() : base(TaskTypes.Slay)
        {
            MonsterNames = Array.Empty<string>();
        }

        public SlayTask(string name, IList<string> monsterNames, int count) : base(TaskTypes.Slay, name)
        {
            MonsterNames = monsterNames;
            MaxCount = count;
        }

        public override bool ShouldShowProgress()
        {
            return true;
        }

        public override void EventSubscribe(ITaskEvents events)
        {
            events.MonsterKilled += OnMonsterKilled;
        }

        public override void EventUnsubscribe(ITaskEvents events)
        {
            events.MonsterKilled -= OnMonsterKilled;
        }

        private void OnMonsterKilled(object? sender, MonsterKilledEventArgs e)
        {
            if (CanUpdate() && IsTaskOwner(e.Player) && MonsterNames.Contains(e.MonsterName))
            {
                IncrementCount();
            }
        }
    }
}
