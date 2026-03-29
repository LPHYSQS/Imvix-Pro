using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class HistoryState : ObservableObject
    {
        private readonly ConversionHistoryService _historyService;
        private readonly ConversionSummaryCoordinator _summaryCoordinator;
        private readonly List<ConversionHistoryEntry> _historyCache = [];

        public HistoryState(
            ConversionHistoryService historyService,
            ConversionSummaryCoordinator summaryCoordinator)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _summaryCoordinator = summaryCoordinator ?? throw new ArgumentNullException(nameof(summaryCoordinator));
            RecentConversions.CollectionChanged += OnRecentConversionsCollectionChanged;
        }

        public ObservableCollection<RecentConversionItem> RecentConversions { get; } = [];

        public bool HasRecentConversions => RecentConversions.Count > 0;

        public bool IsEmpty => !HasRecentConversions;

        public void Load(Func<string, string> translate)
        {
            Replace(_historyService.Load(), translate);
        }

        public void Append(ConversionHistoryEntry entry, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(entry);
            Replace(_historyService.Append(entry), translate);
        }

        public void RefreshPresentation(Func<string, string> translate)
        {
            Replace(_historyCache.ToArray(), translate);
        }

        private void Replace(IReadOnlyList<ConversionHistoryEntry> entries, Func<string, string> translate)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(translate);
            _historyCache.Clear();
            _historyCache.AddRange(entries.OrderByDescending(entry => entry.Timestamp));

            RecentConversions.Clear();
            foreach (var entry in _historyCache)
            {
                RecentConversions.Add(_summaryCoordinator.CreateHistoryItem(entry, translate));
            }
        }

        [RelayCommand(CanExecute = nameof(CanClearRecentConversions))]
        private void ClearRecentConversions()
        {
            Replace(_historyService.Clear(), static _ => string.Empty);
        }

        private bool CanClearRecentConversions()
        {
            return RecentConversions.Count > 0;
        }

        private void OnRecentConversionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasRecentConversions));
            OnPropertyChanged(nameof(IsEmpty));
            ClearRecentConversionsCommand.NotifyCanExecuteChanged();
        }
    }
}
