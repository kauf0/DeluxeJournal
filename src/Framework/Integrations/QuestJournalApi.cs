using System;
using System.Collections.Generic;

namespace DeluxeJournal.Framework.Integrations
{
    /// <summary>Quest Journal's public API (RafiaBee.QuestJournal). Lets a mod add its own entries to the journal.</summary>
    public interface IQuestJournalApi
    {
        void AddOrUpdateEntry(IJournalEntry entry);
        void RemoveEntry(string ownerId, string key);
        void ClearEntries(string ownerId);
        bool IsPinned(string ownerId, string key);
        void SetPinned(string ownerId, string key, bool pinned);
    }

    public interface IJournalEntry
    {
        string OwnerId { get; }
        string Key { get; }
        string Title { get; }
        string BannerTitle { get; }
        string Description { get; }
        string Objective { get; }
        IReadOnlyList<string> Steps { get; }
        int Progress { get; }
        int MaxProgress { get; }
        string Source { get; }
        string Category { get; }
        int? DeadlineDays { get; }
        bool Completed { get; }
        string Placement { get; }
        Action? OnComplete { get; }
        Action? OnCancel { get; }
    }

    public sealed class JournalEntry : IJournalEntry
    {
        public string OwnerId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BannerTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public IReadOnlyList<string> Steps { get; set; } = new List<string>();
        public int Progress { get; set; }
        public int MaxProgress { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? DeadlineDays { get; set; }
        public bool Completed { get; set; }
        public string Placement { get; set; } = string.Empty;
        public Action? OnComplete { get; set; }
        public Action? OnCancel { get; set; }
    }
}
