using StardewModdingAPI;

namespace QuickSave.API
{
    public interface IQuickSaveAPI
    {
        public event SavingDelegate SavingEvent;
        public event SavingExtraDataDelegate SavingExtraDataEvent;
        public event SavedDelegate SavedEvent;
        public bool IsSaving { get; }

        public event LoadingDelegate LoadingEvent;
        public event LoadedDelegate LoadedEvent;
        public bool IsLoading { get; }

        public bool TrySave(IManifest requester, string? saveFileName = null);
        public bool TryLoad(IManifest requester, string? saveFileName = null);

        public delegate void SavingDelegate(object sender, ISavingEventArgs e);
        public delegate void SavingExtraDataDelegate(object sender, ISavingExtraDataEventArgs e);
        public delegate void SavedDelegate(object sender, ISavedEventArgs e);
        public delegate void LoadingDelegate(object sender, ILoadingEventArgs e);
        public delegate void LoadedDelegate(object sender, ILoadedEventArgs e);
    }

    public interface ISavingEventArgs {}
    public interface ISavingExtraDataEventArgs {}
    public interface ISavedEventArgs {}
    public interface ILoadingEventArgs {}
    public interface ILoadedEventArgs {}
}
