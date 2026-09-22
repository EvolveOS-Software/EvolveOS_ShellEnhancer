// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

#region System Namespaces
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
#endregion

#region EvolveOS Shell Enhancer Namespaces
global using EvolveOS_ShellEnhancer.Enums;
global using EvolveOS_ShellEnhancer.Interfaces;
global using EvolveOS_ShellEnhancer.Models;
global using EvolveOS_ShellEnhancer.Views;
global using EvolveOS_ShellEnhancer.Utilities.Animations;
global using EvolveOS_ShellEnhancer.Utilities.Helpers;
global using EvolveOS_ShellEnhancer.Utilities.Managers;
global using EvolveOS_ShellEnhancer.Utilities.Services;
#endregion

#region WinUI 3 / Microsoft Namespaces
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Microsoft.UI.Xaml.Media;
global using Microsoft.UI.Xaml.Media.Imaging;
global using Windows.Foundation;
#endregion

#region Global Aliases & Statics
global using Color = global::Windows.UI.Color;
global using Colors = Microsoft.UI.Colors;
global using Registry = Microsoft.Win32.Registry;
global using VirtualKey = global::Windows.System.VirtualKey;

global using static EvolveOS_ShellEnhancer.Utilities.Helpers.Win32Helper;
#endregion