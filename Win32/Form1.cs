using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Data;
using System.IO;
using System;

using MetroFramework.Forms;
using MetroFramework.Controls;
using MetroFramework.Drawing;
using MetroFramework;
using System.Reflection;
using System.Text;

namespace Win32;


public sealed partial class Form1
    : MetroForm
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_CHILD = 0x40000000u;
    private const int SW_MAXIMIZE = 3;


    private delegate bool enum_delegate(nint hwnd, int lParam);

    private delegate void WinEventDelegate(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll")]
    private static extern nint FindWindowEx(nint parentWindow, nint previousChildWindow, string? windowClass, string? windowTitle);

    [DllImport("user32.dll")]
    private static extern nint GetWindowThreadProcessId(nint hWnd, out int process);

    [DllImport("user32.dll")]
    private static extern nint FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern int EnumWindows(enum_delegate callPtr, int lParam);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, nuint dwNewLong);

    [DllImport("user32.dll")]
    private static extern nuint GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern nint GetWindowText(nint hWnd, StringBuilder title, nint size);

    [DllImport("user32.dll")]
    private static extern int ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, nint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref tagMARGIN margins);


    private static readonly FieldInfo _tabPageCount = typeof(TabControl).GetField(nameof(_tabPageCount), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly MetroTabControl tab_control;
    private readonly MetroButton btn_new, btn_clone, btn_close;
    private readonly MetroPanel panel;

    private volatile bool _running = true;


    public TabInfo[] Tabs => tab_control.TabPages.Cast<TabPage>().Select(t => t.Tag).Cast<TabInfo>().ToArray();


    public Form1()
    {
        SuspendLayout();

        panel = new()
        {
            MinimumSize = new(102, 25),
            MaximumSize = new(306, 25),
            // Dock = DockStyle.Top,
            Location = new(0, 5),
        };
        btn_new = new()
        {
            AutoSize = true,
            Size = new(100, 14),
            Dock = DockStyle.Left,
            Text = "New Tab",
        };
        btn_clone = new()
        {
            AutoSize = true,
            Visible = false,
            Size = new(100, 14),
            Dock = DockStyle.Left,
            Text = "Clone Tab",
        };
        btn_close = new()
        {
            AutoSize = true,
            Visible = false,
            Size = new(100, 14),
            Dock = DockStyle.Left,
            Text = "Close Tab",
        };
        tab_control = new()
        {
            Dock = DockStyle.Fill,
        };
        tab_control.TabIndexChanged += Tab_control_TabIndexChanged;
        tab_control.SizeChanged += Tab_control_SizeChanged;

        panel.Controls.Add(btn_close);
        panel.Controls.Add(btn_clone);
        panel.Controls.Add(btn_new);
        Controls.Add(panel);

        btn_new.Click += async (_, _) =>
        {
            if (await CreateTab(new(Environment.CurrentDirectory)) is TabPage page)
                SelectTab(page);
        };
        btn_clone.Click += async (_, _) =>
        {
            if (tab_control.SelectedTab is TabPage page)
                await DuplicateTab(page);
        };
        btn_close.Click += async (_, _) => await CloseTab(tab_control.SelectedTab);

        // TODO : save / recall

        Controls.Add(panel);
        Controls.Add(tab_control);

        Style = MetroColorStyle.Teal;
        BorderStyle = MetroBorderStyle.None;
        //ShadowType = MetroFormShadowType.AeroShadow;
        ShadowType = MetroFormShadowType.DropShadow;
        Resizable = true;
        MinimumSize = new(500, 350);
        DisplayHeader = false;
        Size = new(900, 700);
        Font = new("Bahnschrift", 12);

        panel.Margin = Margin = new(0);
        panel.Padding = Padding = new(0);
        panel.Font = btn_clone.Font = btn_close.Font = btn_new.Font = Font;
        panel.BringToFront();

        ResumeLayout();

        Load += Form1_Load;
        FormClosing += Form1_FormClosing;
        HandleDestroyed += Form1_HandleDestroyed;
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        nint hwnd = Handle;
        //tagMARGIN margins = new() { top = -6 };

        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) | 0x00040000u);
        SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0227);
        UpdateWindow(hwnd);
        //DwmExtendFrameIntoClientArea(hwnd, ref margins);

        _running = true;

        Task.Factory.StartNew(async () =>
        {
            int last = 0;

            while (_running)
                if (_tabPageCount.GetValue(tab_control) is int count)
                {
                    if (count != last)
                    {
                        Invoke(() => Tab_control_TabCountChanged(tab_control, last, count));

                        last = count;
                    }

                    await Task.Delay(50);
                }
                else
                    break;
        });
    }

    private void Form1_HandleDestroyed(object? sender, EventArgs e) => _running = false;

    private async void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        foreach (TabPage page in tab_control.TabPages)
            await CloseTab(page);
    }

    protected unsafe override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m is { Msg: 0x0083, WParam: not (nint)0 }) // WM_NCCALCSIZE
            ((tagNCCALCSIZE_PARAMS*)m.LParam)->rgrc_0.top -= 6;
    }

    private void Tab_control_SizeChanged(object? sender, EventArgs e)
    {
        foreach (TabPage page in tab_control.TabPages)
            if (page.Tag is TabInfo info)
                UpdateEmbeddedWindow(info.hWnd);
    }

    private void Tab_control_TabIndexChanged(object? sender, EventArgs e)
    {
        tab_control.SelectedTab.Focus();
    }

    private void Tab_control_TabCountChanged(TabControl control, int old, int @new)
    {
        btn_clone.Visible = @new > 0;
        btn_close.Visible = @new > 0;
        panel.Size = @new > 0 ? panel.MaximumSize : panel.MinimumSize;
    }

    private void SelectTab(TabPage page) => Invoke(() => tab_control.SelectedTab = page); // TODO : ????

    private void UpdateTab(TabPage page)
    {
        string title = "Tab " + tab_control.TabPages.IndexOf(page);

        if (page.Tag is TabInfo info)
        {
            StringBuilder sb = new(256);

            GetWindowText(info.hWnd, sb, sb.Capacity);

            string path = sb.ToString();

            try
            {
                info.Path = new(path);
                title = info.Path.Name;
            }
            catch
            {
                info.Path = null;
                title = path;
            }
        }

        Invoke(() => page.Text = title);
    }

    private async Task<TabPage?> CreateTab(DirectoryInfo? path)
    {
        TabPage page = new();
        Process proc = new()
        {
            StartInfo = new()
            {
                FileName = "explorer",
                Arguments = path is null ? "" : $"\"{path.FullName}\"",
                UseShellExecute = true,
            },
            EnableRaisingEvents = true,
        };

        if (tab_control.SelectedIndex < 0 || tab_control.SelectedIndex > tab_control.TabPages.Count - 1)
            tab_control.TabPages.Add(page);
        else
            tab_control.TabPages.Insert(tab_control.SelectedIndex + 1, page);

        tab_control.Update();

        proc.Start();
        proc.WaitForInputIdle();

        nint hwnd = 0;

        for (int i = 0; hwnd == 0 && i < 10; ++i)
        {
            await Task.Delay(300);

            proc.Refresh();
            hwnd = GetProcessWindows(proc.Id, new[] { "explorer.exe", proc.StartInfo.FileName }, new[] { path.FullName, "file explorer", "explorer" }).FirstOrDefault();
        }

        if (hwnd != 0)
        {
            UpdateEmbeddedWindow(hwnd);
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) | WS_CHILD);
            SetParent(hwnd, page.Handle);
            ShowWindow(hwnd, SW_MAXIMIZE);
            UpdateEmbeddedWindow(hwnd);
            // exstyle : 0x00000200L
            SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0227);
            GetWindowThreadProcessId(hwnd, out int pid);

            proc = Process.GetProcessById(pid);

            TabInfo info = new(proc, hwnd)
            {
                Path = path
            };

            page.Tag = info;

            SelectTab(page);
            UpdateTab(page);

            await Task.Factory.StartNew(async delegate
            {
                while (!proc.HasExited && !info.Killed && IsWindow(hwnd))
                {
                    await Task.Delay(50);

                    proc.Refresh();
                    UpdateTab(page);
                }

                await CloseTab(page);
            });

            return page;
        }
        else
        {
            await CloseTab(page);

            return null;
        }
    }

    private async Task<TabPage?> DuplicateTab(TabPage page)
    {
        SelectTab(page);
        UpdateTab(page);

        if (page.Tag is TabInfo info)
            return await CreateTab(info.Path);
        else
            return null;
    }

    private async Task CloseTab(TabPage page)
    {
        if (page.Tag is TabInfo { Process: Process proc } info)
            if (info.Killed)
                return;
            else
                using (proc)
                {
                    proc.Kill();

                    await proc.WaitForExitAsync();

                    info.Killed = true;
                }

        Invoke(delegate
        {
            tab_control.TabPages.Remove(page);
            page.Dispose();
        });
    }

    private static void UpdateEmbeddedWindow(nint hwnd)
    {
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~(WS_CHILD | 0x80C70000u));
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) & ~0x00020201u);
        SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0227);
        UpdateWindow(hwnd);
    }

    private static List<nint> GetProcessWindows(int process, string[] proc_name, string[] title_hints)
    {
        List<nint> windows = new();

        _ = EnumWindows((hwnd, lparam) =>
        {
            GetWindowThreadProcessId(hwnd, out int pid);

            if (pid == process)
                windows.Add(hwnd);

            return true;
        }, 0);

        if (windows.Count == 0)
            foreach (string hint in title_hints)
            {
                nint hwnd = FindWindow(null, hint);

                if (hwnd != 0)
                    windows.Add(hwnd);
            }

        if (windows.Count == 0)
            foreach (Process proc in proc_name.SelectMany(Process.GetProcessesByName))
                try
                {
                    proc.Refresh();

                    if (title_hints.Any(hint => proc.MainWindowTitle.Contains(hint, StringComparison.InvariantCultureIgnoreCase)))
                        windows.Add(proc.MainWindowHandle);
                }
                catch (Exception)
                {
                }

        return windows;
    }
}

public record TabInfo(Process Process, nint hWnd)
{
    public volatile bool Killed = false;

    public DirectoryInfo? Path { get; set; }
}

internal struct tagNCCALCSIZE_PARAMS
{
    public tagRECT rgrc_0;
    public tagRECT rgrc_1;
    public tagRECT rgrc_2;
    public tagWINDOWPOS lppos;
}

internal struct tagRECT
{
    public long left;
    public long top;
    public long right;
    public long bottom;
}

internal struct tagMARGIN
{
    public int left;
    public int right;
    public int top;
    public int bottom;
}

internal struct tagWINDOWPOS
{
    public nint hwnd;
    public nint hwndInsertAfter;
    public int x;
    public int y;
    public int cx;
    public int cy;
    public uint flags;
}
