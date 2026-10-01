using StardewModdingAPI;
using StardewModdingAPI.Events;
using DeluxeJournal.Task;

namespace DeluxeJournal.Framework.Integrations
{
    /// <summary>Mirrors the player's Deluxe Journal tasks into Quest Journal, when that mod is installed.</summary>
    internal sealed class QuestJournalIntegration
    {
        private const string QuestJournalId = "RafiaBee.QuestJournal";

        private const string Placement = "Deluxe Journal";

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly string _ownerId;
        private readonly HashSet<string> _lastKeys = new();
        private IQuestJournalApi? _api;

        public QuestJournalIntegration(IModHelper helper, IMonitor monitor, string ownerId)
        {
            _helper = helper;
            _monitor = monitor;
            _ownerId = ownerId;
        }

        /// <summary>Connect to Quest Journal once all mods are loaded.</summary>
        public void Register()
        {
            _helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            _api = _helper.ModRegistry.GetApi<IQuestJournalApi>(QuestJournalId);

            if (_api == null)
            {
                return;
            }

            _monitor.Log("RafiaBee.QuestJournal found. Deluxe Journal tasks will show up in its journal too.", LogLevel.Info);

            _helper.Events.GameLoop.SaveLoaded += (_, _) => Resync();
            _helper.Events.GameLoop.DayStarted += (_, _) => Sync();

            if (DeluxeJournalMod.EventManager is { } events)
            {
                events.TaskListChanged.Add((_, _) => Sync());
                events.TaskStatusChanged.Add((_, _) => Sync());
            }
        }

        private void Resync()
        {
            // Quest Journal keeps one set of entries for all screens, so only the first screen's player is mirrored.
            if (_api == null || Context.ScreenId != 0)
            {
                return;
            }

            _api.ClearEntries(_ownerId);
            _lastKeys.Clear();
            Sync();
        }

        private void Sync()
        {
            if (_api == null || !Context.IsWorldReady || Context.ScreenId != 0)
            {
                return;
            }

            IList<ITask>? tasks = DeluxeJournalMod.TaskManager?.Tasks;

            if (tasks == null)
            {
                return;
            }

            HashSet<string> current = new();
            Dictionary<string, int> nameCounts = new();

            foreach (ITask task in tasks)
            {
                if (task.IsHeader || !task.Active)
                {
                    continue;
                }

                if (task.Complete && task.RenewPeriod == ITask.Period.Never)
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(task.Name) ? "(unnamed)" : task.Name.Trim();
                int seen = nameCounts.TryGetValue(name, out int c) ? c : 0;
                nameCounts[name] = seen + 1;
                string key = seen == 0 ? name : $"{name} #{seen + 1}";

                current.Add(key);
                _api.AddOrUpdateEntry(BuildEntry(task, key));
            }

            foreach (string stale in _lastKeys)
            {
                if (!current.Contains(stale))
                {
                    _api.RemoveEntry(_ownerId, stale);
                }
            }

            _lastKeys.Clear();
            _lastKeys.UnionWith(current);
        }

        private JournalEntry BuildEntry(ITask task, string key)
        {
            bool showProgress = task.ShouldShowProgress() && task.MaxCount > 0;
            string name = task.Name ?? string.Empty;

            return new JournalEntry
            {
                OwnerId = _ownerId,
                Key = key,
                Title = name,
                BannerTitle = _helper.Translation.Get("questjournal.banner"),
                Description = ResolveRepeat(task),
                Objective = showProgress ? $"{name} {task.Count}/{task.MaxCount}" : name,
                Source = "Deluxe Journal",
                Placement = Placement,
                Completed = task.Complete,
                Progress = showProgress ? task.Count : 0,
                MaxProgress = showProgress ? task.MaxCount : 0,
                OnComplete = () => CompleteTask(task),
                OnCancel = () => CancelTask(task)
            };
        }

        private string ResolveRepeat(ITask task)
        {
            ITask.Period period = task.RenewPeriod;

            if (period == ITask.Period.Never)
            {
                return string.Empty;
            }

            string label;

            if (period == ITask.Period.Custom)
            {
                int days = task.RenewCustomInterval;
                string key = days == 1 ? "ui.tasks.renew.day" : "ui.tasks.renew.days";
                label = _helper.Translation.Get(key, new { count = days });
            }
            else
            {
                label = _helper.Translation.Get($"ui.tasks.options.renew.{period}");
            }

            return _helper.Translation.Get("questjournal.repeats", new { period = label });
        }

        private void CompleteTask(ITask task)
        {
            if (task.Complete)
            {
                return;
            }

            task.Complete = true;
            Sync();
        }

        private void CancelTask(ITask task)
        {
            // Removing a task that is not in the list would still unsubscribe it from its events.
            if (DeluxeJournalMod.TaskManager?.Tasks is IList<ITask> tasks && tasks.Contains(task))
            {
                tasks.Remove(task);
            }

            Sync();
        }
    }
}
