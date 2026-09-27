// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Windows.Storage.Streams;

namespace EvolveOS_ShellEnhancer.Models
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public class AppItem : INotifyPropertyChanged
    {
        private string? _name;
        public string? Name
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

        private string? _executablePath;
        public string? ExecutablePath
        {
            get => _executablePath;
            set
            {
                if (_executablePath != value)
                {
                    _executablePath = value;
                    OnPropertyChanged(nameof(ExecutablePath));
                    OnPropertyChanged(nameof(AppNameVisibility));
                    OnPropertyChanged(nameof(PinnedFolderVisibility));
                    OnPropertyChanged(nameof(StandardIconVisibility));
                    OnPropertyChanged(nameof(StandardNoIconVisibility));
                    OnPropertyChanged(nameof(AllAppsChevronVisibility));
                    OnPropertyChanged(nameof(IsFolderItem));
                }
            }
        }

        public string? FallbackGlyph { get; set; }
        public bool IsUwp { get; set; }

        public double IconScale { get; set; } = 1.0;

        public ObservableCollection<AppItem> FolderApps { get; } = new ObservableCollection<AppItem>();

        public AppItem()
        {
            FolderApps.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (AppItem item in e.NewItems) item.ParentFolderSize = this.FolderSize;
                }
                OnPropertyChanged(nameof(PreviewFolderApps));
            };
        }

        private ImageSource? _iconSource;
        public ImageSource? IconSource
        {
            get => _iconSource;
            set
            {
                if (_iconSource != value)
                {
                    _iconSource = value;
                    OnPropertyChanged(nameof(IconSource));
                    OnPropertyChanged(nameof(HasIcon));
                    OnPropertyChanged(nameof(HasNoIcon));
                    OnPropertyChanged(nameof(StandardIconVisibility));
                    OnPropertyChanged(nameof(StandardNoIconVisibility));
                }
            }
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(DisplayToolTip));
                }
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(ChevronGlyph));
                    OnPropertyChanged(nameof(ChevronAngle));
                }
            }
        }

        private bool _isIndented;
        public bool IsIndented
        {
            get => _isIndented;
            set
            {
                if (_isIndented != value)
                {
                    _isIndented = value;
                    OnPropertyChanged(nameof(IsIndented));
                    OnPropertyChanged(nameof(ItemMargin));
                }
            }
        }

        private int _folderSize = 1;
        public int FolderSize
        {
            get => _folderSize;
            set
            {
                if (_folderSize != value)
                {
                    _folderSize = value;
                    OnPropertyChanged(nameof(FolderSize));
                    OnPropertyChanged(nameof(CardWidth));
                    OnPropertyChanged(nameof(CardHeight));
                    OnPropertyChanged(nameof(FolderBoxSize));
                    OnPropertyChanged(nameof(AppIconSize));
                    OnPropertyChanged(nameof(AppFontSize));

                    foreach (var child in FolderApps) child.ParentFolderSize = value;
                }
            }
        }

        private int _parentFolderSize = 1;
        public int ParentFolderSize
        {
            get => _parentFolderSize;
            set
            {
                _parentFolderSize = value;
                OnPropertyChanged(nameof(FolderInnerIconSize));
            }
        }

        public double CardWidth => FolderSize == 2 ? 176 : 80;
        public double CardHeight => FolderSize == 2 ? 204 : 96;
        public double FolderBoxSize => FolderSize == 2 ? 136 : 48;

        public double FolderInnerIconSize => ParentFolderSize == 2 ? 32 : 16;
        public double FolderInnerCellSize => ParentFolderSize == 2 ? 64 : 22;

        public double AppIconSize => FolderSize == 2 ? 88 : 32;
        public double AppFontSize => FolderSize == 2 ? 64 : 32;

        public IEnumerable<AppItem> PreviewFolderApps => FolderApps.Take(4);

        public Visibility AppNameVisibility => Visibility.Visible;
        public Visibility PinnedFolderVisibility => ExecutablePath == "PINNED_FOLDER" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility StandardIconVisibility => ExecutablePath == "PINNED_FOLDER" ? Visibility.Collapsed : HasIcon;
        public Visibility StandardNoIconVisibility => ExecutablePath == "PINNED_FOLDER" ? Visibility.Collapsed : HasNoIcon;
        public Visibility AllAppsChevronVisibility => (FallbackGlyph == "\xE8B7" || (!string.IsNullOrEmpty(ExecutablePath) && Directory.Exists(ExecutablePath))) ? Visibility.Visible : Visibility.Collapsed;

        public string? DisplayToolTip => IsRunning ? null : Name;
        public double ChevronAngle => _isExpanded ? 180.0 : 0.0;
        public string ChevronGlyph => _isExpanded ? "\xE70E" : "\xE70D";

        internal IRandomAccessStreamReference? UwpLogoStreamRef { get; set; }

        public Visibility HasIcon => IconSource != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasNoIcon => IconSource == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsFolderItem => (!string.IsNullOrEmpty(ExecutablePath) && Directory.Exists(ExecutablePath)) ? Visibility.Visible : Visibility.Collapsed;

        public Thickness ItemMargin => _isIndented ? new Thickness(28, 0, 0, 0) : new Thickness(12, 0, 0, 0);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}