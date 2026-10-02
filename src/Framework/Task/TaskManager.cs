using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using DeluxeJournal.Events;
using DeluxeJournal.Framework.Data;
using DeluxeJournal.Task;
using DeluxeJournal.Framework.Events;

namespace DeluxeJournal.Framework.Task
{
    internal class TaskManager
    {
        /// <summary>Data key for the tasks save data.</summary>
        public const string TasksDataKey = "tasks-data";

        /// <summary>Message type sent by a farmhand to request their saved tasks from the host.</summary>
        private const string RequestTasksMessage = "RequestTasks";

        /// <summary>Message type for a player's task list, sent by the host on request and by a farmhand on change.</summary>
        private const string TasksMessage = "Tasks";

        private readonly EventManager _eventManager;
        private readonly ITaskEvents _taskEvents;
        private readonly IDataHelper _dataHelper;
        private readonly IMultiplayerHelper _multiplayer;
        private readonly Config _config;
        private readonly ISemanticVersion _version;
        private readonly IDictionary<long, EventManagedTaskList> _tasks;
        private TaskData? _data;
        private bool _loaded;
        private bool _hostTasksReceived;
        private bool _tasksChanged;

        /// <summary><c>true</c> if the tasks have been loaded for the first time. Tasks are loaded on save started.</summary>
        public bool Loaded
        {
            get => _loaded;
            private set => _loaded = value;
        }

        /// <summary>A list of tasks for the active player.</summary>
        public IList<ITask> Tasks
        {
            get
            {
                long umid = Game1.player.UniqueMultiplayerID;

                if (!_tasks.ContainsKey(umid))
                {
                    _tasks[umid] = new(umid, _taskEvents, _eventManager.TaskListChanged);
                }

                return _tasks[umid];
            }
        }

        /// <summary>Task save data.</summary>
        private TaskData Data
        {
            get
            {
                // Delayed deserialization to allow for file migration using localized game data.
                if (_data == null)
                {
                    _data = _dataHelper.ReadGlobalData<TaskData>(TasksDataKey) ?? new(_version);
                    Loaded = true;
                }

                return _data;
            }
        }

        public TaskManager(EventManager eventManager, IDataHelper dataHelper, IMultiplayerHelper multiplayer, Config config, ISemanticVersion version)
        {
            _eventManager = eventManager;
            _taskEvents = new TaskEvents(eventManager);
            _dataHelper = dataHelper;
            _multiplayer = multiplayer;
            _config = config;
            _version = version;
            _tasks = new Dictionary<long, EventManagedTaskList>();

            eventManager.ModEvents.GameLoop.SaveLoaded += OnSaveLoaded;
            eventManager.ModEvents.GameLoop.Saving += OnSaving;
            eventManager.ModEvents.GameLoop.DayStarted += OnDayStarted;
            eventManager.ModEvents.GameLoop.DayEnding += OnDayEnding;
            eventManager.ModEvents.GameLoop.UpdateTicked += OnUpdateTicked;
            eventManager.ModEvents.Multiplayer.ModMessageReceived += OnModMessageReceived;
            eventManager.TaskListChanged.Add(OnTasksChanged);
            eventManager.TaskStatusChanged.Add(OnTasksChanged);
        }

        /// <summary>Sort local player tasks.</summary>
        public void SortTasks()
        {
            EventManagedTaskList tasks = (EventManagedTaskList)Tasks;
            RefreshGroups(tasks);
            tasks.Sort();
        }

        /// <summary>Load the task list from save data.</summary>
        public void Load()
        {
            _data = null;
            long umid;

            ClearTasks();

            if (Constants.SaveFolderName is string saveFolderName && Data.Tasks.ContainsKey(saveFolderName))
            {
                foreach (long key in Data.Tasks[saveFolderName].Keys)
                {
                    // UMID is set to 0 when data is converted from legacy versions (<= 1.0.3)
                    umid = (key == 0) ? Game1.player.UniqueMultiplayerID : key;

                    if (!_tasks.ContainsKey(umid))
                    {
                        _tasks[umid] = new(umid, _taskEvents, _eventManager.TaskListChanged);
                    }

                    foreach (ITask task in Data.Tasks[saveFolderName][key])
                    {
                        task.OwnerUniqueMultiplayerID = umid;
                        _tasks[umid].Add(task.Copy());
                    }

                    RefreshGroups(_tasks[umid]);
                    _tasks[umid].Sort();
                }
            }
        }

        /// <summary>Save the task list.</summary>
        public void Save()
        {
            if (Loaded && Constants.SaveFolderName is string saveFolderName)
            {
                Data.Version = _version.ToString();
                Data.Tasks[saveFolderName] = _tasks
                    .Where(entry => entry.Value.Count > 0)
                    .ToDictionary(entry => entry.Key, entry => (IList<ITask>)entry.Value.ToList());

                _dataHelper.WriteGlobalData(TasksDataKey, Data);
            }
        }

        /// <inheritdoc cref="RefreshGroups(IList{ITask})"/>
        public void RefreshGroups()
        {
            RefreshGroups(Tasks);
        }

        /// <summary>Remove the task lists of all players.</summary>
        private void ClearTasks()
        {
            // Each TaskList must be cleared in order to unsubscribe from task events
            foreach (EventManagedTaskList tasks in _tasks.Values)
            {
                tasks.Clear();
            }

            _tasks.Clear();
        }

        /// <summary>Replace the task list of a player.</summary>
        private void SetTasks(long umid, IEnumerable<ITask> tasks)
        {
            if (!_tasks.TryGetValue(umid, out EventManagedTaskList? taskList))
            {
                taskList = new(umid, _taskEvents, _eventManager.TaskListChanged);
                _tasks[umid] = taskList;
            }

            taskList.Clear();

            foreach (ITask task in tasks)
            {
                task.OwnerUniqueMultiplayerID = umid;
                taskList.Add(task);
            }
        }

        /// <summary>Send the task list of a player to another player.</summary>
        /// <param name="umid">The unique multiplayer ID of the player that owns the tasks.</param>
        /// <param name="playerId">The unique multiplayer ID of the receiving player.</param>
        private void SendTasks(long umid, long playerId)
        {
            TaskData data = new(_version);
            data.Tasks[Constants.SaveFolderName ?? string.Empty] = new Dictionary<long, IList<ITask>>()
            {
                { umid, _tasks.TryGetValue(umid, out EventManagedTaskList? tasks) ? tasks.ToList() : new List<ITask>() }
            };

            _multiplayer.SendMessage(data, TasksMessage, new[] { _multiplayer.ModID }, new[] { playerId });
        }

        /// <summary>Send the local farmhand's tasks to the host if they have changed.</summary>
        private void SendChangedTasks()
        {
            if (_tasksChanged && Context.IsWorldReady)
            {
                _tasksChanged = false;
                SendTasks(Game1.player.UniqueMultiplayerID, Game1.MasterPlayer.UniqueMultiplayerID);
            }
        }

        /// <summary>Store the tasks sent by a farmhand, to be written with the host's save data.</summary>
        /// <param name="playerId">The unique multiplayer ID of the farmhand that sent the tasks.</param>
        /// <param name="data">Received task data.</param>
        private void ReceiveFarmhandTasks(long playerId, TaskData data)
        {
            foreach ((long umid, IList<ITask> tasks) in data.Tasks.Values.SelectMany(players => players))
            {
                if (umid == playerId && umid != Game1.player.UniqueMultiplayerID)
                {
                    SetTasks(umid, tasks);
                }
                else
                {
                    _eventManager.Monitor.Log($"Ignored tasks for player {umid} sent by player {playerId}.", LogLevel.Warn);
                }
            }
        }

        /// <summary>Load the local farmhand's tasks sent by the host.</summary>
        /// <param name="data">Received task data.</param>
        private void ReceiveHostTasks(TaskData data)
        {
            long umid = Game1.player.UniqueMultiplayerID;

            SetTasks(umid, data.Tasks.Values
                .SelectMany(players => players)
                .Where(entry => entry.Key == umid)
                .SelectMany(entry => entry.Value));

            // The day has already started by the time the host responds.
            RenewTasks();
            _hostTasksReceived = true;
        }

        /// <summary>Refresh the task groups.</summary>
        /// <remarks>
        /// Tasks are grouped by headers and are given the same <see cref="ITask.Group"/> value as the header
        /// they fall under.
        /// </remarks>
        private static void RefreshGroups(IList<ITask> tasks)
        {
            int group = 0;
            int colorIndex = -1;

            foreach (ITask task in tasks)
            {
                if (task.IsHeader)
                {
                    group++;
                    colorIndex = task.ColorIndex;
                }

                task.Group = group;
                task.GroupColorIndex = colorIndex;
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _hostTasksReceived = false;
            _tasksChanged = false;

            if (Context.IsMainPlayer)
            {
                Load();
            }
            else if (!Context.IsOnHostComputer)
            {
                // Farmhand tasks are saved by the host, so they are requested on join and sent back when changed
                ClearTasks();
                _multiplayer.SendMessage(string.Empty, RequestTasksMessage, new[] { _multiplayer.ModID }, new[] { Game1.MasterPlayer.UniqueMultiplayerID });
            }
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                Save();
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            RenewTasks();
        }

        /// <summary>Renew and validate the local player's tasks at the start of the day.</summary>
        private void RenewTasks()
        {
            int pushGroup = 0;
            int pushIndex = 0;

            RefreshGroups();

            for (int i = Tasks.Count - 1; i >= 0; i--)
            {
                ITask task = Tasks[i];

                if (!task.Active && task.RenewPeriod != ITask.Period.Never && task.DaysRemaining() <= 0)
                {
                    task.Active = true;

                    if (_config.PushRenewedTasksToTheTop)
                    {
                        if (task.Group != pushGroup)
                        {
                            for (pushIndex = i; pushIndex > 0; pushIndex--)
                            {
                                ITask groupTask = Tasks[pushIndex - 1];

                                if (groupTask.IsHeader)
                                {
                                    pushGroup = groupTask.Group;
                                    break;
                                }
                            }

                            if (pushIndex == 0)
                            {
                                pushGroup = 0;
                            }
                        }

                        if (pushIndex < i)
                        {
                            Tasks.RemoveAt(i++);
                            Tasks.Insert(pushIndex, task);
                            continue;
                        }
                    }
                }

                task.Validate();
            }

            SortTasks();
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            for (int i = Tasks.Count - 1; i >= 0; i--)
            {
                ITask task = Tasks[i];

                if (task.RenewPeriod != ITask.Period.Never && task.Complete)
                {
                    task.Complete = task.Active = false;
                }

                if (task.Complete)
                {
                    Tasks.RemoveAt(i);
                }
            }

            // Make sure the host has the farmhand's tasks before saving
            SendChangedTasks();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            SendChangedTasks();
        }

        private void OnTasksChanged(object? sender, EventArgs e)
        {
            if (_hostTasksReceived)
            {
                _tasksChanged = true;
            }
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != _multiplayer.ModID)
            {
                return;
            }

            if (Context.IsMainPlayer)
            {
                if (e.Type == RequestTasksMessage)
                {
                    SendTasks(e.FromPlayerID, e.FromPlayerID);
                }
                else if (e.Type == TasksMessage)
                {
                    ReceiveFarmhandTasks(e.FromPlayerID, e.ReadAs<TaskData>());
                }
            }
            else if (e.Type == TasksMessage && e.FromPlayerID == Game1.MasterPlayer.UniqueMultiplayerID)
            {
                ReceiveHostTasks(e.ReadAs<TaskData>());
            }
        }
    }
}
