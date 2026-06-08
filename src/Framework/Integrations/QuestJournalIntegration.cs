using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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

        public void Register()
        {
            _helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            _api = _helper.ModRegistry.GetApi<IQuestJournalApi>(QuestJournalId);
            if (_api == null)
                return;

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
            if (_api == null)
                return;
            _api.ClearEntries(_ownerId);
            _lastKeys.Clear();
            Sync();
        }

        private void Sync()
        {
            if (_api == null || !Context.IsWorldReady)
                return;

            var tasks = DeluxeJournalMod.TaskManager?.Tasks;
            if (tasks == null)
                return;

            var current = new HashSet<string>();
            var nameCounts = new Dictionary<string, int>();

            foreach (ITask task in tasks)
            {
                if (task.IsHeader || !task.Active)
                    continue;

                if (task.Complete && task.RenewPeriod == ITask.Period.Never)
                    continue;

                string name = string.IsNullOrWhiteSpace(task.Name) ? "(unnamed)" : task.Name.Trim();
                int seen = nameCounts.TryGetValue(name, out int c) ? c : 0;
                nameCounts[name] = seen + 1;
                string key = seen == 0 ? name : $"{name} #{seen + 1}";

                current.Add(key);
                _api.AddOrUpdateEntry(BuildEntry(task, key));
            }

            foreach (string stale in _lastKeys)
                if (!current.Contains(stale))
                    _api.RemoveEntry(_ownerId, stale);

            _lastKeys.Clear();
            _lastKeys.UnionWith(current);
        }
        private const string BannerLabel = "Deluxe Journal Task";

        private JournalEntry BuildEntry(ITask task, string key)
        {
            bool showProgress = task.ShouldShowProgress() && task.MaxCount > 0;
            string name = task.Name ?? string.Empty;
            return new JournalEntry
            {
                OwnerId = _ownerId,
                Key = key,
                Title = name,
                BannerTitle = BannerLabel,
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
                return string.Empty;

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

            return $"Repeats: {label}";
        }

        private void CompleteTask(ITask task)
        {
            if (task.Complete)
                return;
            task.Complete = true;
            Sync();
        }

        private void CancelTask(ITask task)
        {
            DeluxeJournalMod.TaskManager?.Tasks.Remove(task);
            Sync();
        }
    }
}
