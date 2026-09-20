// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EvolveOS_ShellEnhancer.Models
{
    public class AppCategory : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _name = "New Category";
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        public ObservableCollection<AppItem> Apps { get; set; } = new();
    }
}