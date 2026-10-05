// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EvolveOS_ShellEnhancer.Models
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public class StartMenuPage : INotifyPropertyChanged
    {
        public ObservableCollection<AppCategory> PinnedCategories { get; } = new();
        public ObservableCollection<AppItem> RecentDocsCollection { get; } = new();

        private string _pageName = "";
        public string PageName
        {
            get => _pageName;
            set { if (_pageName != value) { _pageName = value; OnPropertyChanged(nameof(PageName)); } }
        }

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

        private double _indicatorSize = 6.0;
        public double IndicatorSize
        {
            get => _indicatorSize;
            set
            {
                if (_indicatorSize != value)
                {
                    _indicatorSize = value;
                    OnPropertyChanged(nameof(IndicatorSize));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}