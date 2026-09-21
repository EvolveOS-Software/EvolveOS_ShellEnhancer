using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace EvolveOS_ShellEnhancer.Models
{
    public class ShortcutItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _targetPath = string.Empty;
        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(); }
        }

        private int _displayModeIndex;
        public int DisplayModeIndex
        {
            get => _displayModeIndex;
            set { _displayModeIndex = value; OnPropertyChanged(); }
        }

        private bool _isSeparator;
        public bool IsSeparator
        {
            get => _isSeparator;
            set
            {
                _isSeparator = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StandardItemVisibility));
                OnPropertyChanged(nameof(SeparatorVisibility));
            }
        }

        private string _iconGlyph = string.Empty;
        public string IconGlyph
        {
            get => _iconGlyph;
            set { _iconGlyph = value; OnPropertyChanged(); OnPropertyChanged(nameof(FontIconVisibility)); }
        }

        private string _iconImagePath = string.Empty;
        public string IconImagePath
        {
            get => _iconImagePath;
            set
            {
                _iconImagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomImageVisibility));
                OnPropertyChanged(nameof(FontIconVisibility));
                UpdateIconImage();
            }
        }

        private ImageSource? _iconImage;
        public ImageSource? IconImage
        {
            get => _iconImage;
            private set { _iconImage = value; OnPropertyChanged(); }
        }

        private void UpdateIconImage()
        {
            if (!string.IsNullOrEmpty(_iconImagePath) && File.Exists(_iconImagePath))
            {
                try
                {
                    IconImage = new BitmapImage(new Uri(_iconImagePath));
                }
                catch
                {
                    IconImage = null;
                }
            }
            else
            {
                IconImage = null;
            }
        }

        public Visibility StandardItemVisibility => IsSeparator ? Visibility.Collapsed : Visibility.Visible;
        public Visibility SeparatorVisibility => IsSeparator ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CustomImageVisibility => !string.IsNullOrEmpty(IconImagePath) && IconImage != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FontIconVisibility => (string.IsNullOrEmpty(IconImagePath) || IconImage == null) && !string.IsNullOrEmpty(IconGlyph) ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}