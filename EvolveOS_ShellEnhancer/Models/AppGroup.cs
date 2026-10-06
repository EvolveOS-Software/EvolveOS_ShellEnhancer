// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_ShellEnhancer.Models
{
    public class AppGroup : List<AppItem>
    {
        public string Key { get; }
        public bool HasItems => Count > 0;

        public double Opacity => HasItems ? 1.0 : 0.3;

        public AppGroup(string key)
        {
            Key = key;
        }
    }
}