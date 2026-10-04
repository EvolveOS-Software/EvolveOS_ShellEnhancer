// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using Windows.Storage.Streams;

namespace EvolveOS_ShellEnhancer.Models
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public class AppItem : INotifyPropertyChanged
    {
        #region Core Properties

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

        public string DisplayDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExecutablePath)) return string.Empty;
                try
                {
                    string? dir = System.IO.Path.GetDirectoryName(ExecutablePath);
                    return string.IsNullOrEmpty(dir) ? string.Empty : dir + "\\";
                }
                catch { return string.Empty; }
            }
        }

        public string DisplayFileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExecutablePath)) return string.Empty;
                try { return System.IO.Path.GetFileName(ExecutablePath); }
                catch { return ExecutablePath; }
            }
        }

        public string? FallbackGlyph { get; set; }
        public bool IsUwp { get; set; }
        internal IRandomAccessStreamReference? UwpLogoStreamRef { get; set; }

        public DateTime InstallDate { get; set; }
        public bool IsNew { get; set; }

        public ObservableCollection<AppItem> FolderApps { get; } = new ObservableCollection<AppItem>();
        public ObservableCollection<AppItem> PreviewFolderApps { get; } = new ObservableCollection<AppItem>();

        #endregion

        #region Initialization & Synchronization

        public AppItem()
        {
            FolderApps.CollectionChanged += SyncPreviewApps;
        }

        private void SyncPreviewApps(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e?.NewItems != null)
            {
                foreach (AppItem item in e.NewItems)
                {
                    item.ParentFolderSize = this.FolderSize;
                }
            }

            PreviewFolderApps.Clear();
            for (int i = 0; i < Math.Min(4, FolderApps.Count); i++)
            {
                PreviewFolderApps.Add(FolderApps[i]);
            }
        }

        #endregion

        #region UI & Layout Properties

        private string? _customImagePath;
        public string? CustomImagePath
        {
            get => _customImagePath;
            set
            {
                if (_customImagePath != value)
                {
                    _customImagePath = value;
                    OnPropertyChanged(nameof(CustomImagePath));
                }
            }
        }

        private string? _tintColor;
        public string? TintColor
        {
            get => _tintColor;
            set
            {
                if (_tintColor != value)
                {
                    _tintColor = value;

                    if (!string.IsNullOrEmpty(_tintColor) && _tintColor.Contains("_"))
                    {
                        var parts = _tintColor.Split('_');
                        if (parts.Length >= 3)
                        {
                            _isTileTinted = parts[1] == "1";
                            _isIconTinted = parts[2] == "1";
                            OnPropertyChanged(nameof(IsTileTinted));
                            OnPropertyChanged(nameof(IsIconTinted));
                        }
                    }

                    OnPropertyChanged(nameof(TintColor));
                    OnPropertyChanged(nameof(TileBrush));
                    OnPropertyChanged(nameof(TintBrush));
                    OnPropertyChanged(nameof(ActiveIconSource));

                    if (_isIconTinted && !string.IsNullOrEmpty(_tintColor) && _tintColor != "NONE")
                    {
                        _ = Utilities.Helpers.StartMenuHelper.ApplyTintAsync(this);
                    }
                }
            }
        }

        private bool _isTileTinted = true;
        public bool IsTileTinted
        {
            get => _isTileTinted;
            set
            {
                if (_isTileTinted != value)
                {
                    _isTileTinted = value;
                    UpdateTintColorFlags();
                    OnPropertyChanged(nameof(IsTileTinted));
                    OnPropertyChanged(nameof(TileBrush));
                }
            }
        }

        private bool _isIconTinted = false;
        public bool IsIconTinted
        {
            get => _isIconTinted;
            set
            {
                if (_isIconTinted != value)
                {
                    _isIconTinted = value;
                    UpdateTintColorFlags();
                    OnPropertyChanged(nameof(IsIconTinted));
                    OnPropertyChanged(nameof(TintBrush));
                    OnPropertyChanged(nameof(ActiveIconSource));
                }
            }
        }

        private void UpdateTintColorFlags()
        {
            if (string.IsNullOrEmpty(_tintColor) || _tintColor == "NONE") return;

            var baseColor = _tintColor.Split('_')[0];
            string tileFlag = _isTileTinted ? "1" : "0";
            string iconFlag = _isIconTinted ? "1" : "0";

            _tintColor = $"{baseColor}_{tileFlag}_{iconFlag}";
            OnPropertyChanged(nameof(TintColor));
        }

        public SolidColorBrush TileBrush
        {
            get
            {
                if (ExecutablePath == "PINNED_FOLDER" || ExecutablePath == "TAB_DATA")
                    return new SolidColorBrush(Colors.Transparent);

                if (!IsTileTinted || string.IsNullOrEmpty(_tintColor) || _tintColor == "NONE")
                    return new SolidColorBrush(Colors.Transparent);

                return GetBrushFromHex(_tintColor);
            }
        }

        public SolidColorBrush? TintBrush
        {
            get
            {
                if (!IsIconTinted || string.IsNullOrEmpty(_tintColor) || _tintColor == "NONE")
                    return null;

                return GetBrushFromHex(_tintColor);
            }
        }

        private SolidColorBrush GetBrushFromHex(string hexColor)
        {
            try
            {
                string hex = hexColor.Split('_')[0].Replace("#", "");
                byte a = 255, r = 0, g = 0, b = 0;

                if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }

                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
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
                    OnPropertyChanged(nameof(ActiveIconSource));
                    OnPropertyChanged(nameof(HasIcon));
                    OnPropertyChanged(nameof(HasNoIcon));
                    OnPropertyChanged(nameof(StandardIconVisibility));
                    OnPropertyChanged(nameof(StandardNoIconVisibility));
                }
            }
        }

        private ImageSource? _tintedIconSource;
        public ImageSource? TintedIconSource
        {
            get => _tintedIconSource;
            set
            {
                if (_tintedIconSource != value)
                {
                    _tintedIconSource = value;
                    OnPropertyChanged(nameof(TintedIconSource));
                    OnPropertyChanged(nameof(ActiveIconSource));
                }
            }
        }

        public ImageSource? ActiveIconSource => (IsIconTinted && TintedIconSource != null) ? TintedIconSource : IconSource;

        public double IconScale { get; set; } = 1.0;

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

        #endregion

        #region Visibility & Display Configurations

        private Visibility _appNameVisibility = SettingsEngine.Shell_StartMenuShowAppLabels ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AppNameVisibility
        {
            get => ExecutablePath == "PINNED_FOLDER" ? Visibility.Visible : _appNameVisibility;
            set
            {
                if (_appNameVisibility != value)
                {
                    _appNameVisibility = value;
                    OnPropertyChanged(nameof(AppNameVisibility));
                }
            }
        }

        public double TextGridWidth => ExecutablePath == "PINNED_FOLDER" ? FolderBoxSize : double.NaN;
        public HorizontalAlignment TextGridAlignment => ExecutablePath == "PINNED_FOLDER" ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        public Visibility PinnedFolderVisibility => ExecutablePath == "PINNED_FOLDER" ? Visibility.Visible : Visibility.Collapsed;

        public Visibility StandardIconVisibility => ExecutablePath == "PINNED_FOLDER" ? Visibility.Collapsed : HasIcon;
        public Visibility StandardNoIconVisibility => ExecutablePath == "PINNED_FOLDER" ? Visibility.Collapsed : HasNoIcon;

        public Visibility AllAppsChevronVisibility => (FallbackGlyph == "\xE8B7" || (!string.IsNullOrEmpty(ExecutablePath) && Directory.Exists(ExecutablePath))) ? Visibility.Visible : Visibility.Collapsed;

        public string? DisplayToolTip => IsRunning ? null : Name;
        public double ChevronAngle => _isExpanded ? 180.0 : 0.0;
        public string ChevronGlyph => _isExpanded ? "\xE70E" : "\xE70D";

        public Visibility HasIcon => IconSource != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasNoIcon => IconSource == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsFolderItem => (!string.IsNullOrEmpty(ExecutablePath) && Directory.Exists(ExecutablePath)) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NewBadgeVisibility => IsNew ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InverseNewBadgeVisibility => IsNew ? Visibility.Collapsed : Visibility.Visible;

        public Thickness ItemMargin => _isIndented ? new Thickness(28, 0, 0, 0) : new Thickness(12, 0, 0, 0);

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}