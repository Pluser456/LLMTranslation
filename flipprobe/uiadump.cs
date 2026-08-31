// 转储: 1) Shell_TrayWnd 下所有 Button 元素  2) 全桌面中 Name 含"划词翻译"的所有元素
using System;
using System.Windows.Automation;

class UiaDump
{
    static void Main()
    {
        var tray = AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        Console.WriteLine("Shell_TrayWnd = " + (tray == null ? "NULL" : "found"));
        if (tray != null)
        {
            var btns = tray.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            Console.WriteLine("buttons=" + btns.Count);
            foreach (AutomationElement b in btns)
            {
                string pat = "-";
                try { pat = b.GetCurrentPattern(InvokePattern.Pattern) != null ? "invoke" : "-"; } catch { }
                var r = b.Current.BoundingRectangle;
                Console.WriteLine("  [" + b.Current.Name + "] cls=" + b.Current.ClassName + " @(" + (int)r.X + "," + (int)r.Y + "," + (int)r.Width + "x" + (int)r.Height + ") " + pat);
            }
        }
        // 全桌面找 Name 精确等于窗口标题的元素
        var cond = new PropertyCondition(AutomationElement.NameProperty, "LLM 划词翻译");
        var hits = AutomationElement.RootElement.FindAll(TreeScope.Descendants, cond);
        Console.WriteLine("desktop hits name~LLM划词翻译 = " + hits.Count);
        foreach (AutomationElement h in hits)
            Console.WriteLine("  [" + h.Current.Name + "] ctrl=" + h.Current.ControlType.ProgrammaticName + " cls=" + h.Current.ClassName);
    }
}
