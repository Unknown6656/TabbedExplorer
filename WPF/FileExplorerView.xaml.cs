using System.ComponentModel;
using System.Diagnostics;

using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

using ModernWpf;

namespace TabbedExplorer.WPF;


public interface TabbedExplorerAPI
{
    void SelectionChanged(FileExplorerViewAPI source, List<FilesystemEntry> selection);
    void OpenInNewTab(FileExplorerViewAPI source, FilePath? path, bool background);
    bool RenameOrMove(FileExplorerViewAPI source, FilesystemEntry existing, FilePath newpath, bool force);
}

public interface FileExplorerViewAPI
{
    bool CanRename { get; }
    bool CanDelete { get; }
    bool CanCut { get; }
    bool CanCopy { get; }
    bool CanPaste { get; }
    bool CanCreateFile { get; }
    bool CanCreateDirectory { get; }
    bool CanSelectAll { get; }
    bool CanSelectNone { get; }
    bool CanInvertSelection { get; }
    bool CanShare { get; }
    bool CanPin { get; }
    bool CanUnpin { get; }
    bool CanCompress { get; }
    bool CanGetProperties { get; }
    FilePath? Path { get; }
    FilesystemEntry[] CurrentlySelected { get; }

    bool NavigateUp();
    bool NavigateBack();
    bool NavigateForward();
    void SimulateF2Press();
    void SimulateF6Press();
}

public partial class FileExplorerView
    : UserControl
    , PathNavigatorAPI
    , FileExplorerViewAPI
{
    private static readonly List<FilesystemEntry> InternallyHandled = new()
    {
        FilesystemEntry.SpecialFolders_ThisPC,
        // TODO
    };

    private static readonly DependencyPropertyKey CanGetPropertiesProperty = RegisterReadOnlyProperty<bool>(nameof(CanGetProperties));

    private static readonly DependencyPropertyKey CanCompressProperty = RegisterReadOnlyProperty<bool>(nameof(CanCompress));

    private static readonly DependencyPropertyKey CanUnpinProperty = RegisterReadOnlyProperty<bool>(nameof(CanUnpin));

    private static readonly DependencyPropertyKey CanPinProperty = RegisterReadOnlyProperty<bool>(nameof(CanPin));

    private static readonly DependencyPropertyKey CanShareProperty = RegisterReadOnlyProperty<bool>(nameof(CanShare));

    private static readonly DependencyPropertyKey CanInvertSelectionProperty = RegisterReadOnlyProperty<bool>(nameof(CanInvertSelection));

    private static readonly DependencyPropertyKey CanSelectNoneProperty = RegisterReadOnlyProperty<bool>(nameof(CanSelectNone));

    private static readonly DependencyPropertyKey CanSelectAllProperty = RegisterReadOnlyProperty<bool>(nameof(CanSelectAll));

    private static readonly DependencyPropertyKey CanCreateDirectoryProperty = RegisterReadOnlyProperty<bool>(nameof(CanCreateDirectory));

    private static readonly DependencyPropertyKey CanCreateFileProperty = RegisterReadOnlyProperty<bool>(nameof(CanCreateFile));

    private static readonly DependencyPropertyKey CanPasteProperty = RegisterReadOnlyProperty<bool>(nameof(CanPaste));

    private static readonly DependencyPropertyKey CanCopyProperty = RegisterReadOnlyProperty<bool>(nameof(CanCopy));

    private static readonly DependencyPropertyKey CanCutProperty = RegisterReadOnlyProperty<bool>(nameof(CanCut));

    private static readonly DependencyPropertyKey CanDeleteProperty = RegisterReadOnlyProperty<bool>(nameof(CanDelete));

    private static readonly DependencyPropertyKey CanRenameProperty = RegisterReadOnlyProperty<bool>(nameof(CanRename));

    public static readonly DependencyProperty PathProperty =
        DependencyProperty.Register(nameof(Path), typeof(FilePath), typeof(FileExplorerView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


    [EditorBrowsable(EditorBrowsableState.Never)]
    public event EventHandler<FilePath>? NavigationRequested;

    public event EventHandler<FilesystemEntry?>? OnNavigated;

    public TabbedExplorerAPI? TabbedExplorerAPI { get; set; }

    public FilesystemEntry[] CurrentlySelected { get; private set; } = Array.Empty<FilesystemEntry>();

    public FilePath? Path
    {
        get => GetValue(PathProperty) as FilePath;
        private set => SetValue(PathProperty, value);
    }

    public bool CanGetProperties
    {
        get => (bool)GetValue(CanGetPropertiesProperty.DependencyProperty);
        private set => SetValue(CanGetPropertiesProperty, value);
    }

    public bool CanCompress
    {
        get => (bool)GetValue(CanCompressProperty.DependencyProperty);
        private set => SetValue(CanCompressProperty, value);
    }

    public bool CanUnpin
    {
        get => (bool)GetValue(CanUnpinProperty.DependencyProperty);
        private set => SetValue(CanUnpinProperty, value);
    }

    public bool CanPin
    {
        get => (bool)GetValue(CanPinProperty.DependencyProperty);
        private set => SetValue(CanPinProperty, value);
    }

    public bool CanShare
    {
        get => (bool)GetValue(CanShareProperty.DependencyProperty);
        private set => SetValue(CanShareProperty, value);
    }

    public bool CanInvertSelection
    {
        get => (bool)GetValue(CanInvertSelectionProperty.DependencyProperty);
        private set => SetValue(CanInvertSelectionProperty, value);
    }

    public bool CanSelectNone
    {
        get => (bool)GetValue(CanSelectNoneProperty.DependencyProperty);
        private set => SetValue(CanSelectNoneProperty, value);
    }

    public bool CanSelectAll
    {
        get => (bool)GetValue(CanSelectAllProperty.DependencyProperty);
        private set => SetValue(CanSelectAllProperty, value);
    }

    public bool CanCreateDirectory
    {
        get => (bool)GetValue(CanCreateDirectoryProperty.DependencyProperty);
        private set => SetValue(CanCreateDirectoryProperty, value);
    }

    public bool CanCreateFile
    {
        get => (bool)GetValue(CanCreateFileProperty.DependencyProperty);
        private set => SetValue(CanCreateFileProperty, value);
    }

    public bool CanPaste
    {
        get => (bool)GetValue(CanPasteProperty.DependencyProperty);
        private set => SetValue(CanPasteProperty, value);
    }

    public bool CanCopy
    {
        get => (bool)GetValue(CanCopyProperty.DependencyProperty);
        private set => SetValue(CanCopyProperty, value);
    }

    public bool CanCut
    {
        get => (bool)GetValue(CanCutProperty.DependencyProperty);
        private set => SetValue(CanCutProperty, value);
    }

    public bool CanDelete
    {
        get => (bool)GetValue(CanDeleteProperty.DependencyProperty);
        private set => SetValue(CanDeleteProperty, value);
    }

    public bool CanRename
    {
        get => (bool)GetValue(CanRenameProperty.DependencyProperty);
        private set => SetValue(CanRenameProperty, value);
    }


    public FileExplorerView()
    {
        InitializeComponent();

        _navigator.APIConnector = this;

        // todo: testing only
        _navigator.Path = @"D:\DEV\TabbedExplorer\fluentui-icons";
    }

    public void Refresh() => _navigator.NavigateTo(_navigator.Path);

    public bool NavigateTo(object? path) => _navigator.NavigateTo(path);

    public bool NavigateUp() => NavigateTo(_navigator.Path switch
    {
        { IsRoot: false, Parent: FilePath parent } => parent,
        _ => FilesystemEntry.SpecialFolders_ThisPC,
    });

    public bool NavigateBack() => _navigator.NavigateBack();

    public bool NavigateForward() => _navigator.NavigateForward();

    public void SimulateF2Press()
    {

    }

    public void SimulateF6Press()
    {

    }

    void PathNavigatorAPI.LaunchProcess(string command, FilesystemEntry? current_dir)
    {
        IEnumerable<string> cmdline = SplitCommandLine(command);
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = cmdline.First(),
            Arguments = string.Join(" ", cmdline.Skip(1).Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg)),
            UseShellExecute = true,
            WorkingDirectory = current_dir?.Path,
        });
    }

    bool PathNavigatorAPI.IsExistingPath(FilePath? target)
    {
        if (InternallyHandled.Any(entry => entry.Is(target)))
            return true;
        else if (target?.FullPath is string path)
            try
            {
                DirectoryInfo dir = new(path);

                return dir.Exists;
            }
            catch
            {
            }

        return false;
    }

    void PathNavigatorAPI.OnRefreshComplete(FilesystemEntry? target) => (this as PathNavigatorAPI).OnNavigationComplete(target);

    void PathNavigatorAPI.OnNavigationComplete(FilesystemEntry? target)
    {
        (fileview.ItemsSource, fileview_msg.Text) = GetFilesystemEntries(target, false);

        Path = target?.Path;
        OnNavigated?.Invoke(this, target);
    }

    IEnumerable<FilePath> PathNavigatorAPI.GetDirectSubFolders(FilePath path) => from entry in GetFilesystemEntries(path, true).entries
                                                                                 let p = entry?.Path
                                                                                 where p is { }
                                                                                 select p;

    private (List<FilesystemEntry> entries, string message) GetFilesystemEntries(FilesystemEntry? entry, bool list_only_direct_subfolders)
    {
        List<FilesystemEntry> entries = new();
        string message = "";

        if (entry is null)
            message = "INTERNAL ERROR: The filesystem entry is a nullptr.";
        else if (FilesystemEntry.SpecialFolders_ThisPC.Is(entry))
        {
            entries.Add(FilesystemEntry.UserFolders_UserDir);
            entries.Add(FilesystemEntry.UserFolders_AppData);
            entries.Add(FilesystemEntry.UserFolders_LocalAppData);
            entries.Add(FilesystemEntry.SpecialFolders_RecycleBin);
            entries.Add(FilesystemEntry.Separator);
            entries.Add(FilesystemEntry.UserFolders_Desktop);
            entries.Add(FilesystemEntry.UserFolders_Documents);
            entries.Add(FilesystemEntry.UserFolders_Downloads);
            entries.Add(FilesystemEntry.UserFolders_Pictures);
            entries.Add(FilesystemEntry.UserFolders_Music);
            entries.Add(FilesystemEntry.UserFolders_Videos);
            entries.Add(FilesystemEntry.UserFolders_Contacts);
            entries.Add(FilesystemEntry.UserFolders_Favorites);
            entries.Add(FilesystemEntry.UserFolders_Links);
            entries.Add(FilesystemEntry.UserFolders_Searches);
            entries.Add(FilesystemEntry.Separator);

            foreach (DriveInfo? drive in DriveInfo.GetDrives())
                entries.Add(FilesystemEntry.Create(drive.RootDirectory, string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel, $"{drive.Name} ({drive.DriveFormat}, {drive.DriveType})"));

            entries.Add(FilesystemEntry.Separator);

            // TODO : list drives, libraries, recycle bin, and special folders


        }
        else if (entry?.FileSystemInfo is DirectoryInfo dir)
        {
            if (!list_only_direct_subfolders)
                if (entry.Path is { IsRoot: false, Parent: FilePath parent })
                    entries.Add(FilesystemEntry.Create(parent, "..", $"({parent.Name}/)"));
                else
                    entries.Add(FilesystemEntry.SpecialFolders_ThisPC);

            try
            {
                int count = entries.Count;

                foreach (DirectoryInfo subdir in dir.EnumerateDirectories())
                    entries.Add(subdir);

                if (!list_only_direct_subfolders)
                {
                    // TODO : check sorting cache

                    entries.AddRange(from file in dir.EnumerateFiles()
                                     let fileentry = (FilesystemEntry)file
                                     group fileentry by fileentry.ExtensionWithoutDot into g
                                     orderby g.Key ascending
                                     from fileentry in g
                                     select fileentry);
                }

                if (entries.Count == count)
                    message = "The directory seems to be empty.";
            }
            catch (Exception? ex)
            {
                message = "An error occurred";

                while (ex != null)
                {
                    message += $"\n-----------------\n{ex}";
                    ex = ex.InnerException;
                }
            }
        }

        return (entries, message);
    }

    private void fileview_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // List<FilesystemEntry> newlyselected = e.AddedItems.Cast<FilesystemEntry>().ToList();
        // List<FilesystemEntry> deselected = e.RemovedItems.Cast<FilesystemEntry>().ToList();
        List<FilesystemEntry> selected = fileview.SelectedItems.Cast<FilesystemEntry>().ToList();

        CurrentlySelected = selected.ToArray();
        TabbedExplorerAPI?.SelectionChanged(this, selected);

        foreach (object? item in fileview.Items)
            if (item is FilesystemEntry entry)
                entry.IsSelected = false;

        foreach (var entry in selected)
            entry.IsSelected = true;
    }

    private void tb_search_KeyDown(object sender, KeyEventArgs e)
    {

    }

    private void tb_file_rename_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: FilesystemEntry { Path: FilePath path } } textbox)
        {
            textbox.SelectAll();

            foreach (var item in fileview.ItemsSource)
                if (item is FilesystemEntry entry && entry.Path != path)
                    entry.Suppress = true;
        }
    }

    private void tb_file_rename_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: FilesystemEntry { Path: FilePath { Name: string oldname } path } selected, Text: string newname } textbox)
        {
            foreach (var item in fileview.ItemsSource)
                if (item is FilesystemEntry entry && entry.Path != path)
                    entry.Suppress = false;

            newname = newname.Trim();

            if (IsValidPath(path.Parent, newname) && oldname != newname && (FilePath?)newname is FilePath newpath)
            {
                bool success = TabbedExplorerAPI?.RenameOrMove(this, selected, path.Parent + newpath, false) ?? false;

                if (!success)
                    MainWindow.MessageBox("This is a test text!", "Some title", MessageBoxButton.YesNoCancel, SymbolGlyph.Color);



                ; // TODO : error message
            }
        }
    }

    private void tb_file_rename_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox { Tag: FilesystemEntry { Path: FilePath path } } textbox)
            if (e.Key is Key.Enter && IsValidPath(path.Parent, textbox.Text))
            {
                e.Handled = true;

                fileview.Focus();
            }
            else if (e.Key is Key.Escape)
            {
                e.Handled = true;
                textbox.Text = path.Name;

                fileview.Focus();
            }
    }

    private void tb_file_rename_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox { Tag: FilesystemEntry { Path: FilePath path }, Text: string text, Parent: DependencyObject parent })
            if (parent.GetChildOfType<Popup>() is Popup popup)
                popup.IsOpen = !IsValidPath(path.Parent, text) && !text.Equals(path.Name, StringComparison.InvariantCultureIgnoreCase);
    }

    private void file_MouseDoubleClick(object sender, EventArgs e)
    {
        List<FilesystemEntry> entries = fileview.SelectedItems.Cast<FilesystemEntry>().ToList();

        if (entries.Count == 0)
            return;

        List<bool> types = entries.Select(e => e.IsDirectory || InternallyHandled.Any(e.Is)).Distinct().ToList();

        if (types.Count != 1)
            return;
        else if (types[0])
        {
            NavigateTo(entries[0]);

            foreach (FilesystemEntry entry in entries.Skip(1))
                if (entry.Path is FilePath path)
                    TabbedExplorerAPI?.OpenInNewTab(this, path, false);
        }
        else
            foreach (FilesystemEntry entry in entries)
                if (entry.Path is FilePath path)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path.FullPath,
                        UseShellExecute = true,
                        WorkingDirectory = path.Parent.FullPath,
                    });
    }

    private void file_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // ctrl click
        //TabbedExplorerAPI?.OpenInNewTab(path, true);
    }

    private void file_KeyDown(object sender, KeyEventArgs e)
    {
        bool any_selected = fileview.SelectedItems.Count > 0;

        switch (e.Key)
        {
            case Key.Escape when any_selected:
                fileview.UnselectAll();
                e.Handled = true;

                return;
            case Key.Enter when any_selected:
                e.Handled = true;
                file_MouseDoubleClick(sender, e);

                return;
        }
    }



    public static IEnumerable<string> SplitCommandLine(string cmd)
    {
        IEnumerable<string> split(Func<char, bool> controller)
        {
            int next = 0;

            for (int c = 0; c < cmd.Length; ++c)
                if (controller(cmd[c]))
                {
                    yield return cmd[next..c];

                    next = c + 1;
                }

            yield return cmd[next..];
        }

        bool inQuotes = false;

        return from arg in split(c =>
        {
            inQuotes ^= c is '\"';

            return !inQuotes && c is ' ';
        })
               let trimmed = arg.Length >= 2 && arg[0] is '\"' && arg[^1] is '\"' ? arg[1..^1] : arg
               where !string.IsNullOrWhiteSpace(trimmed)
               select trimmed;
    }

    private static bool IsValidPath(FilePath folder, string name, bool permit_existing = false)
    {
        name = name.Trim();

        string combined = System.IO.Path.Combine(folder, name);

        return name is not ("." or ".." or "")
            && name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0
            && !(permit_existing && File.Exists(combined))
            && !(permit_existing && Directory.Exists(combined));
    }

    private static DependencyPropertyKey RegisterReadOnlyProperty<T>(string name, T value = default(T)!) =>
        DependencyProperty.RegisterReadOnly(name, typeof(T), typeof(FileExplorerView), new FrameworkPropertyMetadata(value));
}
