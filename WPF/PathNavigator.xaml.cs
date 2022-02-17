using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace TabbedExplorer.WPF
{
    public interface PathNavigatorAPI
    {
        event EventHandler<FilePath> NavigationRequested;

        void LaunchProcess(string command, FilesystemEntry? current_dir);
        bool IsExistingPath(FilePath? target);
        void OnRefreshComplete(FilesystemEntry? target);
        void OnNavigationComplete(FilesystemEntry? target);
        IEnumerable<FilePath> GetDirectSubFolders(FilePath path);
    }

    public sealed partial class PathNavigator
        : UserControl
    {
        public static readonly RoutedEvent RefreshedEvent =
            EventManager.RegisterRoutedEvent(nameof(Refreshed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PathNavigator));

        public static readonly RoutedEvent NavigatedEvent =
            EventManager.RegisterRoutedEvent(nameof(Navigated), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PathNavigator));

        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(nameof(Path), typeof(FilePath), typeof(PathNavigator), new PropertyMetadata(null, (d, e) =>
            {
                if (d is PathNavigator @this && e is { OldValue: var source, NewValue: var target })
                    if (source != target)
                        @this?.NavigateTo(target);
            }));

        public static readonly DependencyProperty APIConnectorProperty =
            DependencyProperty.Register(nameof(APIConnector), typeof(PathNavigatorAPI), typeof(PathNavigator), new PropertyMetadata(null, (d, e) =>
            {
                if (d is PathNavigator @this)
                {
                    if (e.OldValue is PathNavigatorAPI old)
                        old.NavigationRequested -= @this.api_NavigationRequested;

                    if (e.NewValue is PathNavigatorAPI @new)
                        @new.NavigationRequested += @this.api_NavigationRequested;

                    @this._api_connector = e.NewValue as PathNavigatorAPI;
                }
            }));

        private PathNavigatorAPI? _api_connector = null;
        private readonly List<string> _history = new();
        private int _history_index = -1;


        public FilePath? Path
        {
            get => GetValue(PathProperty) as FilePath;
            set => SetValue(PathProperty, value);
        }

        public PathNavigatorAPI? APIConnector
        {
            get => GetValue(APIConnectorProperty) as PathNavigatorAPI;
            set => SetValue(APIConnectorProperty, value);
        }

        public StringComparer PathComparer { get; set; } = StringComparer.InvariantCultureIgnoreCase;

        public event RoutedEventHandler Refreshed
        {
            add => AddHandler(RefreshedEvent, value);
            remove => RemoveHandler(RefreshedEvent, value);
        }

        public event RoutedEventHandler Navigated
        {
            add => AddHandler(NavigatedEvent, value);
            remove => RemoveHandler(NavigatedEvent, value);
        }



        public PathNavigator()
        {
            InitializeComponent();

            Refreshed += OnRefreshed;
            Navigated += OnNavigated;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => NavigateTo(Path);

        public bool NavigateTo(object? target)
        {
            FilesystemEntry? entry = target switch
            {
                null => null,
                FilesystemEntry e => e,
                _ => FilesystemEntry.Create(target switch
                {
                    FilePath p => p,
                    IEnumerable<string> ie => FilePath.FromPath(ie),
                    string s => FilePath.FromPath(s),
                    object o => FilePath.FromPath(o.ToString()!),
                }),
            };
            FilePath? path = entry?.Path;
            string? rawpath = path?.ToString();
            bool requires_navigation = false;

            if (Path != path)
                (Path, requires_navigation) = (path, true);

            if (!string.IsNullOrEmpty(rawpath) && !_history.Contains(rawpath, PathComparer))
                _history.Add(rawpath);

            _history_index = -1;

            for (int i = 0; _history_index < 0 && i < _history.Count; i++)
                if (PathComparer.Equals(_history[i], rawpath))
                    _history_index = i;

            ctx_history.Items.Clear();

            for (int i = 0; i < _history.Count; i++)
            {
                MenuItem item = new()
                {
                    Tag = _history[i],
                    Header = _history[i],
                    IsEnabled = i != _history_index,
                };
                item.Click += (s, _) => NavigateTo((s as MenuItem)?.Tag);

                ctx_history.Items.Add(item);
            }

            tb_raw_path.Text = rawpath;
            ico_current.Source = entry?.Icon;
            ic_path.ItemsSource = path?.FullPathChain;
            btn_up.IsEnabled = !(path?.IsRoot ?? true);
            btn_history.IsEnabled = ctx_history.Items.Count > 1;
            btn_back.IsEnabled = _history_index > 0 && _history_index < _history.Count;
            btn_fwd.IsEnabled = _history_index >= 0 && _history_index < _history.Count - 1;

            VerifyPathDisplay();
            SetRawVisibleState(false);
            RaiseEvent(new RoutedEventArgs(requires_navigation ? NavigatedEvent : RefreshedEvent, this));

            return requires_navigation;
        }

        public bool NavigateUp() => NavigateTo(Path?.Parent);

        public bool NavigateBack() => _history_index > 0
                                   && _history_index < _history.Count
                                   && NavigateTo(_history[_history_index - 1]);

        public bool NavigateForward() => _history_index >= 0
                                      && _history_index < _history.Count - 1
                                      && NavigateTo(_history[_history_index + 1]);

        private void OnNavigated(object sender, RoutedEventArgs e) => _api_connector?.OnNavigationComplete(Path);

        private void OnRefreshed(object sender, RoutedEventArgs e) => _api_connector?.OnRefreshComplete(Path);

        private void api_NavigationRequested(object? sender, FilePath e) => NavigateTo(e);

        private void btn_back_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void btn_fwd_Click(object sender, RoutedEventArgs e) => NavigateForward();

        private void btn_up_Click(object sender, RoutedEventArgs e) => NavigateUp();

        private void btn_refresh_Click(object sender, RoutedEventArgs e) => NavigateTo(Path);

        private void btn_goto_Click(object sender, RoutedEventArgs e)
        {
            FilePath? path = tb_raw_path.Text;
            bool exists = _api_connector?.IsExistingPath(path) ?? false;

            if (exists)
                NavigateTo(path);
            else if (_api_connector is { } connector)
                connector.LaunchProcess(tb_raw_path.Text, Path);

            SetRawVisibleState(false, e);
        }

        private void btn_path_segment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: object tag })
                NavigateTo(tag);
        }

        private void btn_path_dropdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: FilePath path, ContextMenu: ContextMenu ctxmenu })
            {
                e.Handled = true;

                ctxmenu.Items.Clear();

                if (_api_connector is { } connector)
                    foreach (var subdir in connector.GetDirectSubFolders(path))
                    {
                        MenuItem item = new()
                        {
                            Header = subdir.Name,
                            ToolTip = subdir.FullPath,
                            Icon = null, // TODO
                        };
                        item.Click += (_, _) => NavigateTo(subdir);

                        ctxmenu.Items.Add(item);
                    }

                if (ctxmenu.Items.Count is 0)
                    ctxmenu.Items.Add(new MenuItem()
                    {
                        Header = "<no subfolders>",
                        IsEnabled = false,
                    });

                ctxmenu.IsOpen = true;
            }
        }

        private void tb_raw_path_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Escape)
                SetRawVisibleState(false, e);
            else if (e.Key is Key.Enter)
                btn_goto_Click(sender, e);
            else
                return;

            e.Handled = true;
        }

        private void tb_raw_path_GotFocus(object sender, RoutedEventArgs e) => tb_raw_path.SelectAll();

        private void tb_raw_path_LostFocus(object sender, RoutedEventArgs e) => SetRawVisibleState(false, e);

        private void ico_current_MouseDown(object sender, MouseButtonEventArgs e) => SetRawVisibleState(true, e);

        private void ic_path_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement { IsMouseCaptured: false })
                SetRawVisibleState(true, e);
        }

        private void SetRawVisibleState(bool visible, RoutedEventArgs? args = null)
        {
            grid_raw.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            grid_path.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;

            if (visible)
                tb_raw_path.Focus();
            else
                VerifyPathDisplay();

            tb_raw_path.SelectAll();

            if (args is not null)
                args.Handled = true;
        }

        private void ic_path_SizeChanged(object sender, SizeChangedEventArgs e) => VerifyPathDisplay();

        private void VerifyPathDisplay()
        {
            var lol = Enumerable.Range(0, ic_path.Items.Count).Select(ic_path.ItemContainerGenerator.ContainerFromIndex);

            // TODO
        }
    }
}
