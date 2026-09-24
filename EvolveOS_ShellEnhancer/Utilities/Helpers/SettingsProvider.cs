// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_ShellEnhancer.Helpers
{
    public static class SettingsProvider
    {
        public static readonly List<AppItem> KnownSettings = new List<AppItem>
        {
            // System
            new AppItem { Name = "Display", ExecutablePath = "ms-settings:display", FallbackGlyph = "\xE7F4", IsUwp = true },
            new AppItem { Name = "Sound", ExecutablePath = "ms-settings:sound", FallbackGlyph = "\xE767", IsUwp = true },
            new AppItem { Name = "Notifications", ExecutablePath = "ms-settings:notifications", FallbackGlyph = "\xEA8F", IsUwp = true },
            new AppItem { Name = "Focus assist", ExecutablePath = "ms-settings:quietmoments", FallbackGlyph = "\xE708", IsUwp = true },
            new AppItem { Name = "Power & sleep", ExecutablePath = "ms-settings:powersleep", FallbackGlyph = "\xE708", IsUwp = true },
            new AppItem { Name = "Storage", ExecutablePath = "ms-settings:storagesense", FallbackGlyph = "\xE712", IsUwp = true },
            new AppItem { Name = "Multitasking", ExecutablePath = "ms-settings:multitasking", FallbackGlyph = "\xE7C4", IsUwp = true },
            new AppItem { Name = "Clipboard", ExecutablePath = "ms-settings:clipboard", FallbackGlyph = "\xE77F", IsUwp = true },
            new AppItem { Name = "About", ExecutablePath = "ms-settings:about", FallbackGlyph = "\xE946", IsUwp = true },

            // Bluetooth & Devices
            new AppItem { Name = "Bluetooth & devices", ExecutablePath = "ms-settings:bluetooth", FallbackGlyph = "\xE702", IsUwp = true },
            new AppItem { Name = "Printers & scanners", ExecutablePath = "ms-settings:printers", FallbackGlyph = "\xE749", IsUwp = true },
            new AppItem { Name = "Mouse", ExecutablePath = "ms-settings:mouse", FallbackGlyph = "\xE962", IsUwp = true },
            new AppItem { Name = "Touchpad", ExecutablePath = "ms-settings:devices-touchpad", FallbackGlyph = "\xE927", IsUwp = true },
            new AppItem { Name = "Typing", ExecutablePath = "ms-settings:typing", FallbackGlyph = "\xE765", IsUwp = true },
            new AppItem { Name = "AutoPlay", ExecutablePath = "ms-settings:autoplay", FallbackGlyph = "\xE768", IsUwp = true },
            new AppItem { Name = "USB", ExecutablePath = "ms-settings:usb", FallbackGlyph = "\xE88E", IsUwp = true },
            new AppItem { Name = "Mobile devices", ExecutablePath = "ms-settings:mobile-devices", FallbackGlyph = "\xE8EA", IsUwp = true },

            // Network & Internet
            new AppItem { Name = "Network & internet", ExecutablePath = "ms-settings:network-status", FallbackGlyph = "\xE774", IsUwp = true },
            new AppItem { Name = "Wi-Fi", ExecutablePath = "ms-settings:network-wifi", FallbackGlyph = "\xE701", IsUwp = true },
            new AppItem { Name = "Ethernet", ExecutablePath = "ms-settings:network-ethernet", FallbackGlyph = "\xE839", IsUwp = true },
            new AppItem { Name = "VPN", ExecutablePath = "ms-settings:network-vpn", FallbackGlyph = "\xE705", IsUwp = true },
            new AppItem { Name = "Mobile hotspot", ExecutablePath = "ms-settings:network-mobilehotspot", FallbackGlyph = "\xE88A", IsUwp = true },
            new AppItem { Name = "Airplane mode", ExecutablePath = "ms-settings:network-airplanemode", FallbackGlyph = "\xE709", IsUwp = true },
            new AppItem { Name = "Proxy", ExecutablePath = "ms-settings:network-proxy", FallbackGlyph = "\xE774", IsUwp = true },

            // Personalization
            new AppItem { Name = "Personalization", ExecutablePath = "ms-settings:personalization", FallbackGlyph = "\xE771", IsUwp = true },
            new AppItem { Name = "Background", ExecutablePath = "ms-settings:personalization-background", FallbackGlyph = "\xE3B1", IsUwp = true },
            new AppItem { Name = "Colors / Transparency effects", ExecutablePath = "ms-settings:colors", FallbackGlyph = "\xE790", IsUwp = true },
            new AppItem { Name = "Lock screen", ExecutablePath = "ms-settings:lockscreen", FallbackGlyph = "\xE72E", IsUwp = true },
            new AppItem { Name = "Themes", ExecutablePath = "ms-settings:themes", FallbackGlyph = "\xE771", IsUwp = true },
            new AppItem { Name = "Fonts", ExecutablePath = "ms-settings:fonts", FallbackGlyph = "\xE8D2", IsUwp = true },
            new AppItem { Name = "Start", ExecutablePath = "ms-settings:personalization-start", FallbackGlyph = "\xE713", IsUwp = true },
            new AppItem { Name = "Taskbar settings", ExecutablePath = "ms-settings:taskbar", FallbackGlyph = "\xE713", IsUwp = true },

            // Apps
            new AppItem { Name = "Installed apps", ExecutablePath = "ms-settings:appsfeatures", FallbackGlyph = "\xE71D", IsUwp = true },
            new AppItem { Name = "Default apps", ExecutablePath = "ms-settings:defaultapps", FallbackGlyph = "\xE71D", IsUwp = true },
            new AppItem { Name = "Optional features", ExecutablePath = "ms-settings:optionalfeatures", FallbackGlyph = "\xE71D", IsUwp = true },
            new AppItem { Name = "Startup", ExecutablePath = "ms-settings:startupapps", FallbackGlyph = "\xE7B5", IsUwp = true },

            // Accounts
            new AppItem { Name = "Accounts", ExecutablePath = "ms-settings:emailandaccounts", FallbackGlyph = "\xE77B", IsUwp = true },
            new AppItem { Name = "Sign-in options", ExecutablePath = "ms-settings:signinoptions", FallbackGlyph = "\xE8D7", IsUwp = true },
            new AppItem { Name = "Family", ExecutablePath = "ms-settings:family", FallbackGlyph = "\xE77B", IsUwp = true },
            new AppItem { Name = "Windows backup", ExecutablePath = "ms-settings:backup", FallbackGlyph = "\xE753", IsUwp = true },

            // Time & Language
            new AppItem { Name = "Date & time / Change time zone", ExecutablePath = "ms-settings:dateandtime", FallbackGlyph = "\xE823", IsUwp = true },
            new AppItem { Name = "Language settings", ExecutablePath = "ms-settings:regionlanguage", FallbackGlyph = "\xE774", IsUwp = true },
            new AppItem { Name = "Speech", ExecutablePath = "ms-settings:speech", FallbackGlyph = "\xE720", IsUwp = true },

            // Gaming
            new AppItem { Name = "Gaming", ExecutablePath = "ms-settings:gaming-gamemode", FallbackGlyph = "\xE7FC", IsUwp = true },
            new AppItem { Name = "Xbox Game Bar", ExecutablePath = "ms-settings:gaming-gamebar", FallbackGlyph = "\xE7FC", IsUwp = true },
            new AppItem { Name = "Captures", ExecutablePath = "ms-settings:gaming-captures", FallbackGlyph = "\xE714", IsUwp = true },

            // Accessibility
            new AppItem { Name = "Accessibility", ExecutablePath = "ms-settings:easeofaccess-display", FallbackGlyph = "\xE776", IsUwp = true },
            new AppItem { Name = "Text size", ExecutablePath = "ms-settings:easeofaccess-display", FallbackGlyph = "\xE8D2", IsUwp = true },
            new AppItem { Name = "Visual effects", ExecutablePath = "ms-settings:easeofaccess-visualeffects", FallbackGlyph = "\xE790", IsUwp = true },
            new AppItem { Name = "Mouse pointer and touch", ExecutablePath = "ms-settings:easeofaccess-mousepointer", FallbackGlyph = "\xE962", IsUwp = true },
            new AppItem { Name = "Text cursor", ExecutablePath = "ms-settings:easeofaccess-textcursor", FallbackGlyph = "\xE8D2", IsUwp = true },
            new AppItem { Name = "Magnifier", ExecutablePath = "ms-settings:easeofaccess-magnifier", FallbackGlyph = "\xE71E", IsUwp = true },
            new AppItem { Name = "Color filters", ExecutablePath = "ms-settings:easeofaccess-colorfilter", FallbackGlyph = "\xE790", IsUwp = true },
            new AppItem { Name = "Contrast themes", ExecutablePath = "ms-settings:easeofaccess-highcontrast", FallbackGlyph = "\xE790", IsUwp = true },
            new AppItem { Name = "Narrator", ExecutablePath = "ms-settings:easeofaccess-narrator", FallbackGlyph = "\xE8BD", IsUwp = true },
            new AppItem { Name = "Audio", ExecutablePath = "ms-settings:easeofaccess-audio", FallbackGlyph = "\xE767", IsUwp = true },
            new AppItem { Name = "Captions", ExecutablePath = "ms-settings:easeofaccess-closedcaptioning", FallbackGlyph = "\xE8EC", IsUwp = true },
            new AppItem { Name = "Keyboard", ExecutablePath = "ms-settings:easeofaccess-keyboard", FallbackGlyph = "\xE765", IsUwp = true },
            new AppItem { Name = "Speech recognition / Voice typing", ExecutablePath = "ms-settings:easeofaccess-speechrecognition", FallbackGlyph = "\xE720", IsUwp = true },

            // Privacy & Security
            new AppItem { Name = "Privacy & security", ExecutablePath = "ms-settings:privacy", FallbackGlyph = "\xE72E", IsUwp = true },
            new AppItem { Name = "Windows Security", ExecutablePath = "ms-settings:windowsdefender", FallbackGlyph = "\xE773", IsUwp = true },
            new AppItem { Name = "Location", ExecutablePath = "ms-settings:privacy-location", FallbackGlyph = "\xE81D", IsUwp = true },
            new AppItem { Name = "Camera", ExecutablePath = "ms-settings:privacy-camera", FallbackGlyph = "\xE722", IsUwp = true },
            new AppItem { Name = "Microphone", ExecutablePath = "ms-settings:privacy-microphone", FallbackGlyph = "\xE720", IsUwp = true },
            new AppItem { Name = "Diagnostics & feedback", ExecutablePath = "ms-settings:privacy-feedback", FallbackGlyph = "\xE9CE", IsUwp = true },

            // Windows Update
            new AppItem { Name = "Windows Update", ExecutablePath = "ms-settings:windowsupdate", FallbackGlyph = "\xE895", IsUwp = true },
            new AppItem { Name = "Windows Insider Program", ExecutablePath = "ms-settings:windowsinsider", FallbackGlyph = "\xE895", IsUwp = true }
        };
    }
}