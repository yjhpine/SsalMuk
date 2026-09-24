using System;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class DisplaySettings
    {
        public const string DarkModeKey = "SsalMuk.Display.DarkMode";
        public bool DarkMode { get; private set; }
        public event Action<bool> Changed;
        public DisplaySettings() { DarkMode = PlayerPrefs.GetInt(DarkModeKey, 1) != 0; }
        public void SetDarkMode(bool enabled)
        {
            if (DarkMode == enabled) return;
            DarkMode = enabled;
            PlayerPrefs.SetInt(DarkModeKey, enabled ? 1 : 0); PlayerPrefs.Save();
            Changed?.Invoke(enabled);
        }
    }
}
