// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
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

        public string? DisplayToolTip => IsRunning ? null : Name;

        internal IRandomAccessStreamReference? UwpLogoStreamRef { get; set; }

        public Visibility HasIcon => IconSource != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasNoIcon => IconSource == null ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}