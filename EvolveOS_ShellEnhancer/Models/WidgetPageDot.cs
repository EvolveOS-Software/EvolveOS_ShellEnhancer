// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.ComponentModel;

namespace EvolveOS_ShellEnhancer.Models
{
    public class WidgetPageDot : INotifyPropertyChanged
    {
        public int PageIndex { get; set; }

        private double _indicatorSize = 6.0;
        public double IndicatorSize { get => _indicatorSize; set { _indicatorSize = value; OnPropertyChanged(nameof(IndicatorSize)); } }

        private double _indicatorOpacity = 0.4;
        public double IndicatorOpacity { get => _indicatorOpacity; set { _indicatorOpacity = value; OnPropertyChanged(nameof(IndicatorOpacity)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}