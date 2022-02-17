using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

using Microsoft.VisualBasic.FileIO;

using ModernWpf;


namespace TabbedExplorer.WPF;

public sealed partial class MainWindow
    : Window
    , TabbedExplorerAPI
{
    public const string KEY_NEWTAB = "<tag:newtab>";
    public const string KEY_SETTINGS = "<tag:settings>";

    public static MainWindow? Instance { get; private set; }

    private static readonly DependencyPropertyKey CurrentFileExplorerViewProperty =
        DependencyProperty.RegisterReadOnly(nameof(CurrentFileExplorerView), typeof(FileExplorerView), typeof(MainWindow), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey IsModalActiveProperty =
        DependencyProperty.RegisterReadOnly(nameof(IsModalActive), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public StackPointer<FilesystemAction> ActionHistory { get; } = new();



    public bool IsModalActive
    {
        get => (bool)GetValue(IsModalActiveProperty.DependencyProperty);
        private set => SetValue(IsModalActiveProperty, value);
    }

    public FileExplorerView? CurrentFileExplorerView
    {
        get => GetValue(CurrentFileExplorerViewProperty.DependencyProperty) as FileExplorerView;
        private set => SetValue(CurrentFileExplorerViewProperty, value);
    }


    public MainWindow()
    {
        Instance = this;

        InitializeComponent();

        tab_control.SelectionChanged += TabSelectionChanged;
        KeyDown += (s, e) => MainWindow_KeyDown(s, e);
    }

    private void UpdateUI()
    {
        CurrentFileExplorerView = tab_control.SelectedItem is TabItem { Tag: TabInfo info } ? info.Viewer : null;
        Title = CurrentFileExplorerView?.Path ?? "Tabbed File Explorer";

        // btn_undo.IsEnabled = ;
        // btn_redo.IsEnabled = ;
        btn_new_file.IsEnabled = CurrentFileExplorerView?.CanCreateFile ?? false;
        btn_new_folder.IsEnabled = CurrentFileExplorerView?.CanCreateDirectory ?? false;
        btn_file_rename.IsEnabled = CurrentFileExplorerView?.CanRename ?? false;
        btn_file_paste.IsEnabled = CurrentFileExplorerView?.CanPaste ?? false;
        btn_file_copy.IsEnabled = CurrentFileExplorerView?.CanCopy ?? false;
        btn_file_cut.IsEnabled = CurrentFileExplorerView?.CanCut ?? false;
        btn_file_delete.IsEnabled = CurrentFileExplorerView?.CanDelete ?? false;
        btn_select_all.IsEnabled = CurrentFileExplorerView?.CanSelectAll ?? false;
        btn_select_none.IsEnabled = CurrentFileExplorerView?.CanSelectNone ?? false;
        btn_select_invert.IsEnabled = CurrentFileExplorerView?.CanInvertSelection ?? false;
        btn_file_copy_path.IsEnabled =
        btn_file_share.IsEnabled = CurrentFileExplorerView?.CanShare ?? false;
        btn_pin.IsEnabled = CurrentFileExplorerView?.CanPin ?? false;
        btn_unpin.IsEnabled = CurrentFileExplorerView?.CanUnpin ?? false;
        btn_file_compress.IsEnabled = CurrentFileExplorerView?.CanCompress ?? false;
        btn_file_properties.IsEnabled = CurrentFileExplorerView?.CanGetProperties ?? false;
    }

    private void TabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (tab_control.SelectedItem is TabItem tab)
        {
            if (tab.Tag is KEY_NEWTAB)
                CreateTab(FilesystemEntry.SpecialFolders_ThisPC);
            else if (tab.Tag is KEY_SETTINGS)
            {
                // TODO
            }
            else if (tab.Tag is TabInfo tab_info)
            {
                // TODO
            }
            else
            {
                // TODO
            }
        }

        UpdateUI();
    }

    private TabInfo CreateTab(FilesystemEntry path, int? after_index = null, bool switch_to_tab = true) => CreateTab(path.Path, after_index, switch_to_tab);

    private TabInfo CreateTab(FilePath? path, int? after_index = null, bool switch_to_tab = true)
    {
        FileExplorerView viewer = new()
        {
            TabbedExplorerAPI = this,
        };
        Grid header_panel = new()
        {
            Margin = new Thickness(-10, -7, -10, -7),
            Width = 200,
        };
        TextBlock title = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4),
            Width = header_panel.Width - 30,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = path?.Name,
        };
        Button close = new ThemedSVGButton()
        {
            ImageName = "windows.searchclosetab",
            HorizontalAlignment = HorizontalAlignment.Center,
            BorderThickness = new(0),
            Padding = new(2),
            Margin = new(0),
            Width = 16,
            Height = 16,
            ToolTip = "Close Tab",
        };
        title.SetValue(Grid.ColumnProperty, 0);
        close.SetValue(Grid.ColumnProperty, 1);

        header_panel.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header_panel.ColumnDefinitions.Add(new() { Width = new(header_panel.Width - title.Width) });
        header_panel.Children.Add(title);
        header_panel.Children.Add(close);

        TabItem tab = new()
        {
            Header = header_panel,
            Content = viewer,
        };
        TabInfo info = new(tab_control, tab, viewer, title, path);
        int index = Math.Max(0, Math.Min(after_index ?? int.MaxValue, tab_control.Items.Count - 2));

        if (tab_control.SelectedIndex == tab_control.Items.Count - 1)
            --tab_control.SelectedIndex;

        tab.Tag = info;
        close.Click += (_, _) => CloseTab(info);
        viewer.OnNavigated += (_, entry) =>
        {
            title.Text = entry?.Path?.Name;

            Viewer_OnNavigated(info, entry);
            UpdateUI();
        };
        viewer.NavigateTo(path);

        tab_control.Items.Insert(index + 1, tab);

        if (switch_to_tab)
            SwitchTab(info);

        return info;
    }

    private TabInfo DuplicateTab(TabInfo existing) => CreateTab(existing.Viewer?.Path, existing.Index, true);

    private void SwitchTab(TabInfo target)
    {
        if (tab_control.SelectedItem != target.TabItem)
            tab_control.SelectedItem = target.TabItem;
    }

    private void CloseTab(TabInfo tab)
    {
        if (tab.Index is int index)
            tab_control.SelectedIndex = index - 1;

        tab_control.Items.Remove(tab.TabItem);
    }

    void TabbedExplorerAPI.OpenInNewTab(FileExplorerViewAPI source, FilePath? path, bool background) => CreateTab(path, null, !background);

    bool TabbedExplorerAPI.RenameOrMove(FileExplorerViewAPI source, FilesystemEntry existing, FilePath newpath, bool force)
    {
        return false; throw new NotImplementedException(); // TODO
    }

    void TabbedExplorerAPI.SelectionChanged(FileExplorerViewAPI source, List<FilesystemEntry> selection)
    {
        if (source == CurrentFileExplorerView)
            UpdateUI();
    }

    private bool MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        bool handle(Action act)
        {
            e.Handled = true;

            act();

            return true;
        }
        ModifierKeys mod = e.KeyboardDevice.Modifiers;
        Key key = e.Key;

        if (key is Key.System)
            key = e.SystemKey;
        else if (key is Key.ImeProcessed)
            key = e.ImeProcessedKey;
        else if (key is Key.DeadCharProcessed)
            key = e.DeadCharProcessedKey;

        switch (key, mod)
        {
            case (Key.W, ModifierKeys.Control | ModifierKeys.Shift):
                return handle(() => throw new NotImplementedException());
            case (Key.T, ModifierKeys.Control | ModifierKeys.Shift):
                return handle(() => throw new NotImplementedException());
            case (Key.T, ModifierKeys.Control):
                return handle(() => CreateTab(FilesystemEntry.SpecialFolders_ThisPC));
            default:
                if (tab_control.SelectedItem is TabItem { Tag: TabInfo { Viewer: FileExplorerView view } tab })
                    switch (key, mod)
                    {
                        case (Key.Up, ModifierKeys.Alt):
                            return handle(() => view.NavigateUp());
                        case (Key.BrowserBack, _):
                        case (Key.Left, ModifierKeys.Alt):
                            return handle(() => view.NavigateBack());
                        case (Key.BrowserForward, _):
                        case (Key.Right, ModifierKeys.Alt):
                            return handle(() => view.NavigateForward());
                        case (Key.BrowserRefresh, _):
                        case (Key.F5, _):
                            return handle(() => view.NavigateTo(view.Path));
                        case (Key.F6, _):
                            return handle(view.SimulateF6Press);
                        case (Key.F2, _):
                            return handle(view.SimulateF2Press);
                        case (Key.BrowserHome, _):
                        case (Key.H, ModifierKeys.Control):
                            return handle(() => CreateTab(FilesystemEntry.SpecialFolders_ThisPC));
                        case (Key.D, ModifierKeys.Control):
                        case (Key.K, ModifierKeys.Control | ModifierKeys.Shift):
                            return handle(() => DuplicateTab(tab));
                        case (Key.W, ModifierKeys.Control):
                            return handle(() => CloseTab(tab));
                        case (Key.N, ModifierKeys.Control | ModifierKeys.Shift):
                            return handle(() => btn_new_folder_Click(this, e));
                        case (Key.N, ModifierKeys.Control):
                            return handle(() => btn_new_file_Click(this, e));
                    }

                break;
        }

        return false;
    }

    private void Viewer_OnNavigated(TabInfo info, FilesystemEntry entry)
    {
    }

    private void btn_undo_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_redo_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_new_file_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_new_folder_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_file_rename_Click(object sender, RoutedEventArgs e) => CurrentFileExplorerView?.SimulateF2Press();

    private void btn_file_paste_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFileExplorerView?.Path is { } folder && Interop.GetFilesFromClipboard() is (string[] files, DragDropEffects effect))
        {
            FileSystem.


            

            if (effect.HasFlag(DragDropEffects.Move))
                ;
            else if (effect.HasFlag(DragDropEffects.Copy))
                ;
            else
                ; // TODO
        }
    }

    private void btn_file_copy_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFileExplorerView?.CurrentlySelected is { } selected)
            Interop.PutFilesToClipboard((from entry in selected
                                         let p = entry.Path?.FullPath
                                         where p is { }
                                         select p).ToArray(), DragDropEffects.Copy | DragDropEffects.Link);
    }

    private void btn_file_cut_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFileExplorerView?.CurrentlySelected is { } selected)
            Interop.PutFilesToClipboard((from entry in selected
                                         let p = entry.Path?.FullPath
                                         where p is { }
                                         select p).ToArray(), DragDropEffects.Move);
    }

    private void btn_file_delete_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_select_all_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_select_none_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_select_invert_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_file_copy_path_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_file_share_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_pin_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_unpin_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_file_compress_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btn_file_properties_Click(object sender, RoutedEventArgs e)
    {

    }

    public static MessageBoxResult? MessageBox(string text, string title, MessageBoxButton buttons, SymbolGlyph glyph, MessageBoxResult? default_result = null)
    {
        MessageBoxResult? result = null;

        if (Instance is MainWindow window)
            window.Dispatcher.Invoke(() =>
            {
                window.IsModalActive = true;
                result = ModernWpf.MessageBox.Show(Instance, text, title, buttons, glyph, default_result);
                window.IsModalActive = false;
            });

        return result;
    }

    public static MessageBoxResult? MessageBox(string text, string title, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult? default_result = null)
    {
        MessageBoxResult? result = null;

        if (Instance is MainWindow window)
            window.Dispatcher.Invoke(() =>
            {
                window.IsModalActive = true;
                result = ModernWpf.MessageBox.Show(Instance, text, title, buttons, icon, default_result);
                window.IsModalActive = false;
            });

        return result;
    }
}

public sealed class TabInfo
    : IEquatable<TabInfo>
{
    public Guid UUID { get; }
    public FileExplorerView Viewer { get; }
    public FilePath? StartupFolder { get; }
    public TabControl TabHost { get; }
    public TabItem TabItem { get; }
    public TextBlock Title { get; }


    public int? Index => TabHost.Items.IndexOf(TabItem) switch { <0 => null, int i => i };


    internal TabInfo(TabControl tabHost, TabItem tabItem, FileExplorerView viewer, TextBlock title, FilePath? folder)
    {
        UUID = Guid.NewGuid();
        StartupFolder = folder;
        TabHost = tabHost;
        TabItem = tabItem;
        Viewer = viewer;
        Title = title;
    }

    public bool Equals(TabInfo? other) => other?.UUID == UUID;

    public override bool Equals(object? obj) => Equals(obj as TabInfo);

    public override int GetHashCode() => UUID.GetHashCode();
}

public abstract record FilesystemAction
{
    public sealed record Copy(FilePath? From, FilePath? To, bool Overwrite = false) : FilesystemAction;

    public sealed record Move(FilePath? From, FilePath? To, bool Overwrite = false) : FilesystemAction;

    public sealed record Recycle(FilePath? File) : FilesystemAction;

    public sealed record Compress(FilePath? From, FilePath? To, bool Overwrite = false) : FilesystemAction;

    public sealed record CompoundAction(params FilesystemAction[] Actions) : FilesystemAction;
}
