// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace EvolveOS_ShellEnhancer.Models
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public class StartMenuPage : INotifyPropertyChanged
    {
        public ObservableCollection<AppCategory> PinnedCategories { get; } = new();
        public ObservableCollection<AppItem> RecentDocsCollection { get; } = new();

        private int _pageIndex;
        public int PageIndex
        {
            get => _pageIndex;
            set { if (_pageIndex != value) { _pageIndex = value; OnPropertyChanged(nameof(PageIndex)); } }
        }

        private double _indicatorOpacity = 0.3;
        public double IndicatorOpacity
        {
            get => _indicatorOpacity;
            set { if (_indicatorOpacity != value) { _indicatorOpacity = value; OnPropertyChanged(nameof(IndicatorOpacity)); } }
        }

        private Visibility _recentDocsVisibility = Visibility.Collapsed;
        public Visibility RecentDocsVisibility
        {
            get => _recentDocsVisibility;
            set { if (_recentDocsVisibility != value) { _recentDocsVisibility = value; OnPropertyChanged(nameof(RecentDocsVisibility)); } }
        }

        private double _recentDocsChevronAngle = 0;
        public double RecentDocsChevronAngle
        {
            get => _recentDocsChevronAngle;
            set { if (_recentDocsChevronAngle != value) { _recentDocsChevronAngle = value; OnPropertyChanged(nameof(RecentDocsChevronAngle)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}