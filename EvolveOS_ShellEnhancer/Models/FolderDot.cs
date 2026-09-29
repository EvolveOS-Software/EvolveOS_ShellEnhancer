// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_ShellEnhancer.Models
{
    public class FolderDot : System.ComponentModel.INotifyPropertyChanged
    {
        public int PageIndex { get; set; }

        private double _opacity = 0.4;
        public double Opacity
        {
            get => _opacity;
            set { _opacity = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Opacity))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}