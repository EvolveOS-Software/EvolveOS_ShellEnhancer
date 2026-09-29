// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EvolveOS_ShellEnhancer.Models
{
    public class AppCategory : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public AppCategory()
        {
            Tabs.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(SelectedTabApps));
            };
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name = "New Category";
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private bool _isTabbed;
        public bool IsTabbed
        {
            get => _isTabbed;
            set
            {
                if (_isTabbed != value)
                {
                    _isTabbed = value;
                    OnPropertyChanged(nameof(IsTabbed));
                    OnPropertyChanged(nameof(StandardVisibility));
                    OnPropertyChanged(nameof(TabbedVisibility));
                }
            }
        }

        public Visibility StandardVisibility => IsTabbed ? Visibility.Collapsed : Visibility.Visible;
        public Visibility TabbedVisibility => IsTabbed ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<AppCategory> Tabs { get; set; } = new ObservableCollection<AppCategory>();

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged(nameof(SelectedTabIndex));
                    OnPropertyChanged(nameof(SelectedTabApps));
                }
            }
        }

        private string? _tabColor;
        public string? TabColor
        {
            get => _tabColor;
            set
            {
                if (_tabColor != value)
                {
                    _tabColor = value;
                    OnPropertyChanged(nameof(TabColor));
                    OnPropertyChanged(nameof(TabColorBrush));
                }
            }
        }

        public SolidColorBrush TabColorBrush
        {
            get
            {
                if (string.IsNullOrEmpty(TabColor)) return new SolidColorBrush(Colors.Transparent);
                try
                {
                    var color = ColorHelper.FromArgb(
                        255,
                        Convert.ToByte(TabColor.Substring(3, 2), 16),
                        Convert.ToByte(TabColor.Substring(5, 2), 16),
                        Convert.ToByte(TabColor.Substring(7, 2), 16)
                    );
                    return new SolidColorBrush(color);
                }
                catch { return new SolidColorBrush(Colors.Transparent); }
            }
        }

        public ObservableCollection<AppItem>? SelectedTabApps => (Tabs != null && Tabs.Count > _selectedTabIndex && _selectedTabIndex >= 0) ? Tabs[_selectedTabIndex].Apps : null;

        public ObservableCollection<AppItem> Apps { get; set; } = new();
    }
}