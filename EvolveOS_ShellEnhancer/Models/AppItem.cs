// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;
using System.IO;
using Windows.Storage.Streams;

namespace EvolveOS_ShellEnhancer.Models
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public class AppItem : INotifyPropertyChanged
    {
        public string? Name { get; set; }
        public string? ExecutablePath { get; set; }
        public string? FallbackGlyph { get; set; }
        public bool IsUwp { get; set; }

        public double IconScale { get; set; } = 1.0;

        private ImageSource? _iconSource;
        public ImageSource? IconSource
        {
            get => _iconSource;
            set
            {
                if (_iconSource != value)
                {
                    _iconSource = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSource)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasIcon)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoIcon)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayToolTip)));
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