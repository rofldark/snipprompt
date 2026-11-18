using System;
using Microsoft.Win32;

namespace SnipPrompt
{
    /// "Start with Windows" via HKCU\...\Run (no admin rights needed). Starts in the background, without a capture.
    static class Autostart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "SnipPrompt";

        public static void Set(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (on) k.SetValue(Name, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --background");
                    else if (k.GetValue(Name) != null) k.DeleteValue(Name);
                }
            }
            catch (Exception) { }
        }
    }
}
