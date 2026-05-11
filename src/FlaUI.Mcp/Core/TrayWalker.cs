using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace FlaUI.Mcp.Core;

public readonly record struct TrayIcon(
    AutomationElement Element,
    string Name,
    int? OwnerPid,
    TraySource Source);

public enum TraySource { User, System, Overflow }

/// <summary>
/// Enumerates Windows notification-area (tray) icons via UI Automation.
/// Supports Win10 and Win11 with the classic (Explorer) taskbar.
/// Win11 native taskbar (22H2+) is not supported — Shell_TrayWnd is absent.
/// </summary>
public static class TrayWalker
{
    private const string TrayWndClass = "Shell_TrayWnd";
    private const string OverflowWndClass = "NotifyIconOverflowWindow";

    // English names
    private const string UserAreaEn = "User Promoted Notification Area";
    private const string SystemAreaEn = "System Promoted Notification Area";
    private const string ChevronNameEn = "Show hidden icons";

    // Korean names (Windows 10 KO)
    private const string UserAreaKo = "사용자가 승격한 알림 영역";
    private const string SystemAreaKo = "시스템이 승격한 알림 영역";
    private const string ChevronNameKo = "알림 영역 오버플로 표시";

    /// <summary>
    /// Returns true when Shell_TrayWnd is present on the desktop.
    /// False on Win11 native taskbar (22H2+) or non-Explorer shells.
    /// </summary>
    public static bool HasShellTrayWnd(UIA3Automation automation) =>
        SafeAccess.Get(() =>
            automation.GetDesktop().FindFirstChild(cf => cf.ByClassName(TrayWndClass)) != null, false);

    /// <summary>
    /// Returns tray icons visible on the taskbar.
    /// When includeOverflow=true and the overflow popup is hidden, the chevron is
    /// temporarily expanded (Escape is sent afterward to dismiss it).
    /// </summary>
    public static List<TrayIcon> Enumerate(
        UIA3Automation automation,
        bool includeOverflow = true,
        bool includeSystem = false)
    {
        var desktop = automation.GetDesktop();
        var result = new List<TrayIcon>();

        var trayWnd = SafeAccess.Get(() =>
            desktop.FindFirstChild(cf => cf.ByClassName(TrayWndClass)));
        if (trayWnd == null) return result;

        // User notification area
        var userTb = FindNamedToolbar(trayWnd, UserAreaEn, UserAreaKo);
        if (userTb != null)
            CollectButtons(userTb, TraySource.User, result);

        // System notification area (opt-in)
        if (includeSystem)
        {
            var sysTb = FindNamedToolbar(trayWnd, SystemAreaEn, SystemAreaKo);
            if (sysTb != null)
                CollectButtons(sysTb, TraySource.System, result);
        }

        // Fallback for other locales: enumerate ALL toolbars under Shell_TrayWnd.
        // Skip if the targeted search already found icons to avoid duplicates.
        if (result.Count == 0)
        {
            var allToolbars = SafeAccess.Get(() =>
                trayWnd.FindAllDescendants(cf => cf.ByControlType(ControlType.ToolBar)),
                Array.Empty<AutomationElement>());
            foreach (var tb in allToolbars)
                CollectButtons(tb, TraySource.User, result);
        }

        if (!includeOverflow) return result;

        // Overflow: already-visible popup has no side-effect
        var overflowWnd = SafeAccess.Get(() =>
            desktop.FindFirstChild(cf => cf.ByClassName(OverflowWndClass)));

        var openedOverflow = false;
        if (overflowWnd == null)
        {
            overflowWnd = ExpandOverflow(trayWnd, desktop);
            openedOverflow = overflowWnd != null;
        }

        if (overflowWnd != null)
        {
            var overflowBtns = SafeAccess.Get(() =>
                overflowWnd.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)),
                Array.Empty<AutomationElement>());

            foreach (var btn in overflowBtns)
            {
                var name = SafeAccess.Get(() => btn.Properties.Name.ValueOrDefault ?? string.Empty, string.Empty);
                var pid = SafeAccess.Get(() => (int?)btn.Properties.ProcessId.ValueOrDefault);
                result.Add(new TrayIcon(btn, name, pid, TraySource.Overflow));
            }

            if (openedOverflow)
            {
                try { Keyboard.Press(VirtualKeyShort.ESCAPE); } catch { }
            }
        }

        return result;
    }

    private static AutomationElement? FindNamedToolbar(
        AutomationElement trayWnd, string englishName, string localizedName) =>
        SafeAccess.Get(() => trayWnd.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.ToolBar).And(
                cf.ByName(englishName).Or(cf.ByName(localizedName)))));

    private static void CollectButtons(
        AutomationElement toolbar, TraySource source, List<TrayIcon> result)
    {
        var buttons = SafeAccess.Get(() =>
            toolbar.FindAllChildren(cf => cf.ByControlType(ControlType.Button)),
            Array.Empty<AutomationElement>());

        foreach (var btn in buttons)
        {
            var name = SafeAccess.Get(() => btn.Properties.Name.ValueOrDefault ?? string.Empty, string.Empty);
            var pid = SafeAccess.Get(() => (int?)btn.Properties.ProcessId.ValueOrDefault);
            result.Add(new TrayIcon(btn, name, pid, source));
        }
    }

    private static AutomationElement? ExpandOverflow(
        AutomationElement trayWnd, AutomationElement desktop)
    {
        var chevron = SafeAccess.Get(() => trayWnd.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(
                cf.ByName(ChevronNameEn).Or(cf.ByName(ChevronNameKo)))));

        if (chevron == null) return null;

        try
        {
            if (chevron.Patterns.Invoke.IsSupported)
                chevron.Patterns.Invoke.Pattern.Invoke();
            else
                Mouse.Click(chevron.GetClickablePoint());
        }
        catch { return null; }

        // Poll up to 1 second for the overflow popup to appear
        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(100);
            var wnd = SafeAccess.Get(() =>
                desktop.FindFirstChild(cf => cf.ByClassName(OverflowWndClass)));
            if (wnd != null) return wnd;
        }
        return null;
    }
}
