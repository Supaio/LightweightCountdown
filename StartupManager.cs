using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LightweightCountdown
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LightweightCountdown";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public static bool TrySetEnabled(bool enabled, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        errorMessage = "无法访问当前用户的启动项设置。";
                        return false;
                    }

                    if (enabled)
                    {
                        string command = "\"" + Application.ExecutablePath + "\" --startup";
                        key.SetValue(ValueName, command, RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                errorMessage = "无法更新开机自启：" + exception.Message;
                return false;
            }
        }
    }
}
