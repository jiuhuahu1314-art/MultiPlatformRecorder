using System;
using System.Reflection;
using Microsoft.Win32;

#nullable enable
namespace BililiveRecorder.WPF
{
    internal static class AutoStartHelper
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "BililiveRecorder";

        internal static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                if (key is null) return false;
                var value = key.GetValue(ValueName) as string;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        internal static void SetEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                if (enable)
                {
                    var exePath = Assembly.GetEntryAssembly()?.Location;
                    if (string.IsNullOrWhiteSpace(exePath))
                        return;
                    key.SetValue(ValueName, $"\"{exePath}\" --hide");
                }
                else
                {
                    if (key.GetValue(ValueName) is not null)
                        key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
            catch
            { }
        }
    }
}
