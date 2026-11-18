using System;
using System.Collections.Generic;
using System.Windows.Automation;

namespace SnipPrompt
{
    sealed class TabRef
    {
        public string Title;
        public IntPtr Hwnd;
        public AutomationElement Element;
    }

    /// Finds all tabs of all Windows Terminal windows via UI Automation.
    static class TerminalTabs
    {
        const string WindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";

        public static List<TabRef> FindAll()
        {
            var list = new List<TabRef>();
            try
            {
                var winCond = new PropertyCondition(AutomationElement.ClassNameProperty, WindowClass);
                var tabCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                foreach (AutomationElement win in AutomationElement.RootElement.FindAll(TreeScope.Children, winCond))
                {
                    IntPtr hwnd = new IntPtr(win.Current.NativeWindowHandle);
                    foreach (AutomationElement tab in win.FindAll(TreeScope.Descendants, tabCond))
                        list.Add(new TabRef { Title = tab.Current.Name, Hwnd = hwnd, Element = tab });
                }
            }
            catch (Exception) { }
            return list;
        }

        /// Makes the tab the active one in its window. Returns false if not possible.
        public static bool Select(TabRef tab)
        {
            try
            {
                object p;
                if (tab.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p))
                {
                    var sel = (SelectionItemPattern)p;
                    if (!sel.Current.IsSelected) sel.Select();
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        public static bool IsSelected(TabRef tab)
        {
            try
            {
                object p;
                return tab.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p)
                    && ((SelectionItemPattern)p).Current.IsSelected;
            }
            catch (Exception) { return false; }
        }

        /// Reads the live title; false when the tab no longer exists.
        public static bool TryTitle(TabRef tab, out string title)
        {
            try { title = tab.Element.Current.Name; return true; }
            catch (Exception) { title = tab.Title; return false; }
        }

        /// Current title of the tab (it changes while Claude works), falls back to the stored one.
        public static string CurrentTitle(TabRef tab)
        {
            try { return tab.Element.Current.Name; } catch { return tab.Title; }
        }
    }
}
