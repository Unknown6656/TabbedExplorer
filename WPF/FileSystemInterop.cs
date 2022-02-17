// #define USE_FS_ENTRY_CACHE

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows;

namespace TabbedExplorer.WPF;

using FAttr = FileAttributes;



public record HumanReadableSize(string Size, string Unit)
{
    public static HumanReadableSize Unknown { get; } = new("", "");

    public override string ToString() => $"{Size} {Unit}";
}

internal static class Interop
{
    public const string SHELL32 = "shell32.dll";

    public const string KERNEL32 = "kernel32.dll";

    public const string DROP_EFFECT = "Preferred DropEffect";


    [DllImport(SHELL32)]
    public static extern nint ExtractIcon(nint hInst, string lpszExeFileName, int nIconIndex);

    [DllImport(SHELL32)]
    public static extern nint SHGetFileInfo(string pszPath, FAttr attributes, out SHFILEINFO psfi, int cbFileInfo, int flags);

    [DllImport(KERNEL32, SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(nint hFile, FILE_INFO_BY_HANDLE_CLASS infoClass, out FILE_ID_BOTH_DIR_INFO dirInfo, uint dwBufferSize);


    public static BitmapSource? GetIcon(string path, bool large_icon = false)
    {
        SHGetFileInfo(path, 0, out SHFILEINFO shinfo, Marshal.SizeOf<SHFILEINFO>(), large_icon ? 0x100 : 0x101);

        if (shinfo.hIcon != 0)
            return Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        else return File.Exists(path) && System.Drawing.Icon.ExtractAssociatedIcon(path) is { } icon
            ? Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
            : null;
    }

    public static HumanReadableSize ToHumanReadable(this long size)
    {
        string sizes = "kMGTEY";
        double len = size;
        int order = 0;

        while (len >= 1024 && order < sizes.Length)
        {
            ++order;
            len /= 1024;
        }

        return new(order > 0 ? len.ToString("0.00") : len.ToString(), (order == 0 ? "" : sizes[order - 1]) + "B");
    }

    public static string? GetFileTypeDescription(string path_or_extension) =>
        SHGetFileInfo(path_or_extension, FAttr.Normal, out SHFILEINFO shfi, Marshal.SizeOf<SHFILEINFO>(), 0x0410) != 0
            ? shfi.szTypeName : null;

    public static unsafe void PutFilesToClipboard(string[] files, DragDropEffects effect)
    {
        byte[] payload = new byte[sizeof(DragDropEffects)];

        fixed (byte* ptr = payload)
            for (int i = 0; i < payload.Length; ++i)
                ptr[i] = ((byte*)&effect)[i];

        MemoryStream stream = new(payload);
        StringCollection collection = new();
        DataObject obj = new();

        collection.AddRange(files);

        obj.SetData(DROP_EFFECT, stream);
        obj.SetFileDropList(collection);

        Clipboard.SetDataObject(obj);
    }

    public static unsafe (string[] files, DragDropEffects effect)? GetFilesFromClipboard()
    {
        if ((DataObject?)Clipboard.GetDataObject() is DataObject obj)
        {
            string[] files = obj.ContainsFileDropList() ? obj.GetFileDropList().Cast<string>().ToArray() : Array.Empty<string>();
            DragDropEffects effect = DragDropEffects.None;

            if (obj.GetData(DROP_EFFECT) is MemoryStream stream)
            {
                byte[] payload = new byte[sizeof(DragDropEffects)];

                stream.Read(payload, 0, payload.Length);

                fixed (byte* ptr = payload)
                    effect = *(DragDropEffects*)ptr;
            }

            if (files.Length > 0)
                return (files, effect);
        }

        return null;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEINFO
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FILE_ID_BOTH_DIR_INFO
    {
        public uint NextEntryOffset;
        public uint FileIndex;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public long EndOfFile;
        public long AllocationSize;
        public uint FileAttributes;
        public uint FileNameLength;
        public uint EaSize;
        public char ShortNameLength;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
        public string ShortName;
        public long FileId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
        public string FileName;
    }

    public enum FILE_INFO_BY_HANDLE_CLASS
    {
        FileBasicInfo = 0,
        FileStandardInfo = 1,
        FileNameInfo = 2,
        FileRenameInfo = 3,
        FileDispositionInfo = 4,
        FileAllocationInfo = 5,
        FileEndOfFileInfo = 6,
        FileStreamInfo = 7,
        FileCompressionInfo = 8,
        FileAttributeTagInfo = 9,
        FileIdBothDirectoryInfo = 10,// 0x0A
        FileIdBothDirectoryRestartInfo = 11, // 0xB
        FileIoPriorityHintInfo = 12, // 0xC
        FileRemoteProtocolInfo = 13, // 0xD
        FileFullDirectoryInfo = 14, // 0xE
        FileFullDirectoryRestartInfo = 15, // 0xF
        FileStorageInfo = 16, // 0x10
        FileAlignmentInfo = 17, // 0x11
        FileIdInfo = 18, // 0x12
        FileIdExtdDirectoryInfo = 19, // 0x13
        FileIdExtdDirectoryRestartInfo = 20, // 0x14
        MaximumFileInfoByHandlesClass
    }
}

[Flags]
public enum DragDropEffects
    : int
{
    Scroll = int.MinValue,
    All = -0x7FFFFFFD,
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4
}

public class FilesystemEntry
    : DependencyObject
{
#if USE_FS_ENTRY_CACHE
    private static readonly ConcurrentDictionary<FilePath, FilesystemEntry> _path_mapper = new();
#endif

    internal static readonly DependencyProperty SuppressProperty =
        DependencyProperty.Register(nameof(Suppress), typeof(bool), typeof(FilesystemEntry), new PropertyMetadata(false));

    internal static readonly DependencyProperty ShowErrorProperty =
        DependencyProperty.Register(nameof(ShowError), typeof(bool), typeof(FilesystemEntry), new PropertyMetadata(false));

    internal static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(FilesystemEntry), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey HumanReadableSizePropertyKey
        = DependencyProperty.RegisterReadOnly(nameof(HumanReadableSize), typeof(HumanReadableSize), typeof(FilesystemEntry), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HumanReadableSizeProperty = HumanReadableSizePropertyKey.DependencyProperty;

    public static readonly DependencyProperty FileAttributesProperty = register_property<FAttr>(nameof(FileAttributes), (@this, attr) =>
    {
        if (@this.FileSystemInfo is FileSystemInfo info && info.Attributes != attr)
            info.Attributes = attr;
    });

    public static readonly DependencyProperty CreationUTCProperty = register_property<DateTime>(nameof(CreationUTC), (@this, date) =>
    {
        if (@this.FileSystemInfo is FileSystemInfo info && info.CreationTimeUtc != date)
            info.CreationTimeUtc = date;
    });

    public static readonly DependencyProperty AccessUTCProperty = register_property<DateTime>(nameof(AccessUTC), (@this, date) =>
    {
        if (@this.FileSystemInfo is FileSystemInfo info && info.LastAccessTimeUtc != date)
            info.LastAccessTimeUtc = date;
    });

    public static readonly DependencyProperty WriteUTCProperty = register_property<DateTime>(nameof(WriteUTC), (@this, date) =>
    {
        if (@this.FileSystemInfo is FileSystemInfo info && info.LastWriteTimeUtc != date)
            info.LastWriteTimeUtc = date;
    });


    public static FilesystemEntry Unknown { get; } = Create(null, "", "<unknown>", true);

    public static FilesystemEntry Separator { get; } = new FilesystemEntrySeparator();

    public static FilesystemEntry SpecialFolders_ThisPC { get; } = Create("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", attributes: FAttr.System | FAttr.Directory | FAttr.Device | FAttr.ReparsePoint);
    public static FilesystemEntry SpecialFolders_RecycleBin { get; } = Create("shell:::{645ff040-5081-101b-9f08-00aa002f954e}", "Recycle Bin");
    public static FilesystemEntry SpecialFolders_SHGUID_Pictures { get; } = Create("shell:::{008ca0b1-55b4-4c56-b8a8-4de4b299d3be}", "Pictures");
    public static FilesystemEntry SpecialFolders_SHGUID_Documents { get; } = Create("shell:::{450d8fba-ad25-11d0-98a8-0800361b1103}", "Documents");
    public static FilesystemEntry SpecialFolders_Startup { get; } = Create("shell:startup", "Startup");
    public static FilesystemEntry SpecialFolders_NetworkPlaces { get; } = Create("shell:::{208d2c60-3aea-1069-a2d7-08002b30309d}", "Network Places");
    public static FilesystemEntry SpecialFolders_NetworkComputers { get; } = Create("shell:::{1f4de370-d627-11d1-ba4f-00a0c91eedba}", "Network Computers");
    public static FilesystemEntry SpecialFolders_NetworkConnections { get; } = Create("shell:::{7007acc7-3202-11d1-aad2-00805fc1270e}", "Network Connections");
    public static FilesystemEntry SpecialFolders_GodMode { get; } = Create("shell:::{ED7BA470-8E54-465E-825C-99712043E01C}", "God Mode");
    public static FilesystemEntry SpecialFolders_ControlPanel { get; } = Create("shell:::{21EC2020-3AEA-1069-A2DD-08002b30309d}", "Control Panel");
    public static FilesystemEntry SpecialFolders_Connections { get; } = Create("shell:::{241D7C96-F8BF-4F85-B01F-E2B043341A4B}", "Connections");
    public static FilesystemEntry SpecialFolders_History { get; } = Create("shell:::{ff393560-c2a7-11cf-bff4-444553540000}", "History");
    public static FilesystemEntry SpecialFolders_Printers { get; } = Create("shell:::{2227A280-3AEA-1069-A2DE-08002B30309D}", "Printers");
    public static FilesystemEntry SpecialFolders_GetPrograms { get; } = Create("shell:::{de61d971-5ebc-4f02-a3a9-6c82895e5c04}", "Get Programs");
    public static FilesystemEntry SpecialFolders_AdminTools { get; } = Create("shell:::{D20EA4E1-3957-11d2-A40B-0C5020524153}", "Admin Tools");

    public static FilesystemEntry UserFolders_UserDir { get; } = Create("%USERPROFILE%");
    public static FilesystemEntry UserFolders_AppData { get; } = Create("%APPDATA%", "%APPDATA%");
    public static FilesystemEntry UserFolders_LocalAppData { get; } = Create("%LOCALAPPDATA%", "%LOCALAPPDATA%");
    public static FilesystemEntry UserFolders_Desktop { get; } = Create("%USERPROFILE%/Desktop");
    public static FilesystemEntry UserFolders_Contacts { get; } = Create("%USERPROFILE%/Contacts");
    public static FilesystemEntry UserFolders_Documents { get; } = Create("%USERPROFILE%/Documents");
    public static FilesystemEntry UserFolders_Downloads { get; } = Create("%USERPROFILE%/Downloads");
    public static FilesystemEntry UserFolders_Favorites { get; } = Create("%USERPROFILE%/Favorites");
    public static FilesystemEntry UserFolders_Links { get; } = Create("%USERPROFILE%/Links");
    public static FilesystemEntry UserFolders_Music { get; } = Create("%USERPROFILE%/Music");
    public static FilesystemEntry UserFolders_Pictures { get; } = Create("%USERPROFILE%/Pictures");
    public static FilesystemEntry UserFolders_Searches { get; } = Create("%USERPROFILE%/Searches");
    public static FilesystemEntry UserFolders_Videos { get; } = Create("%USERPROFILE%/Videos");
    // public static FilesystemEntry UserFolders_Library_ { get; } = Create("%HOMEPATH%/3D Objects");
    // public static FilesystemEntry UserFolders_Library_ { get; } = Create("%HOMEPATH%/Saved Games");



    public HumanReadableSize? HumanReadableSize
    {
        get => GetValue(HumanReadableSizeProperty) as HumanReadableSize;
        private set => SetValue(HumanReadableSizePropertyKey, value);
    }

    public FileSystemInfo? FileSystemInfo { get; }

    public string DisplayNameOverride { get; set; }

    public FilePath? Path { get; }

    public BitmapSource? Icon { get; }

    public long? SizeInBytes { get; private set; }

    public bool IsDirectory { get; }

    public bool IsReadOnly { get; }

    public string? ExtensionWithoutDot { get; }

    public string? TypeDescription { get; }

    internal bool Suppress
    {
        get => (bool)GetValue(SuppressProperty);
        set => SetValue(SuppressProperty, value);
    }

    internal bool ShowError
    {
        get => (bool)GetValue(ShowErrorProperty);
        set => SetValue(ShowErrorProperty, value);
    }

    internal bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public FAttr? FileAttributes
    {
        get => (FAttr?)GetValue(FileAttributesProperty);
        set => SetValue(FileAttributesProperty, value);
    }

    /*
        D A {RT} C E H S {LSM}
        D {LMS} {RT} S H E C A
        |   |    |   | | | | |
        |   |    |   | | | | '- archive flag
        |   |    |   | | | '--- compressed flag
        |   |    |   | | '----- encrypted flag
        |   |    |   | '------- hidden flag
        |   |    |   '--------- system flag
        |   |    '------------- readonly (R) | temporary (T)
        |   '------------------ symlink (S) | mountpoint/junction (M) | softlink (L)
        '---------------------- directory flag
     */
    public virtual string AttributeString
    {
        get
        {
            const char dash = '-'; // '\x2013';
            FAttr attr = FileAttributes ?? default;
            char linktype = LinkTarget is string ? 'S' : dash;

            if (Path?.Name?.ToLowerInvariant() is { Length: > 4 } name && (name.EndsWith(".url") || name.EndsWith(".lnk")))
                linktype = 'L';

            if (linktype is not 'S' && attr.HasFlag(FAttr.ReparsePoint))
                linktype = 'M'; // junction or mount point

            // darhsl

            char[] flags =
            {
                attr.HasFlag(FAttr.Device) ? 'B' : IsDirectory ? 'D' : Path?.HasExecutableExtension ?? false ? 'X' : dash,
                attr.HasFlag(FAttr.Archive) ? 'A' : dash,
                attr.HasFlag(FAttr.ReadOnly) ? 'R' : attr.HasFlag(FAttr.Temporary) ? 'T' : dash,
                attr.HasFlag(FAttr.Compressed) ? 'C' : dash,
                attr.HasFlag(FAttr.Encrypted) ? 'E' : dash,
                attr.HasFlag(FAttr.Hidden) ? 'H' : dash,
                attr.HasFlag(FAttr.System) ? 'S' : dash,
                linktype,
            };

            return new(flags);
        }
    }

    public DateTime? CreationUTC
    {
        get => (DateTime?)GetValue(CreationUTCProperty);
        set => SetValue(CreationUTCProperty, value);
    }

    public DateTime? AccessUTC
    {
        get => (DateTime?)GetValue(AccessUTCProperty);
        set => SetValue(AccessUTCProperty, value);
    }

    public DateTime? WriteUTC
    {
        get => (DateTime?)GetValue(WriteUTCProperty);
        set => SetValue(WriteUTCProperty, value);
    }

    public string? Creation => CreationUTC?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string? Access => AccessUTC?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string? Write => WriteUTC?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public bool Exists => FileSystemInfo is { };

    public string? OptionalSpecialDisplay { get; }

    public string? LinkTarget => FileSystemInfo?.LinkTarget;

    public bool IsSymLink => LinkTarget is { };


    private protected FilesystemEntry(FilePath? path, string display_name_override, string? optional_special_display, bool is_readonly, FAttr? attributes = null)
    {
        string? fullpath = path?.FullPath;

        if (fullpath is { Length: 2 } && fullpath[1] == ':' && char.IsLetter(fullpath[0]))
        {
            fullpath += '/'; // prevents https://github.com/dotnet/runtime/issues/63892
            path = fullpath;
        }

        Path = path;
        DisplayNameOverride = display_name_override;
        OptionalSpecialDisplay = optional_special_display ?? FileSystemInfo?.LinkTarget;
        FileSystemInfo = File.Exists(fullpath) ? new FileInfo(fullpath) :
                    Directory.Exists(fullpath) ? new DirectoryInfo(fullpath) : null;
        IsDirectory = FileSystemInfo is DirectoryInfo;
        IsReadOnly = display_name_override is ".." or "." or "" || is_readonly;
        FileAttributes = FileSystemInfo?.Attributes;
        ExtensionWithoutDot = FileSystemInfo?.Extension switch {
            "" or null => null,
            string s when s[0] is '.' => s[1..],
            string s => s
        };

        if (attributes is { })
            FileAttributes = attributes;

        if (FileSystemInfo is { FullName: string fpath })
        {
            fullpath = fpath;
            Icon = Interop.GetIcon(fullpath);
            TypeDescription = IsDirectory ? path?.IsRoot ?? false ? "Root Directory" : "File Directory" : Interop.GetFileTypeDescription(fullpath);

            if (FileSystemInfo is FileInfo file)
            {
                SizeInBytes = file.Length;
                HumanReadableSize = file.Length.ToHumanReadable();
            }
            else
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        long size = FolderCache.GetSizeInBytes((DirectoryInfo)FileSystemInfo);

                        SizeInBytes = size;
                        Dispatcher.Invoke(() => HumanReadableSize = size.ToHumanReadable());
                    }
                    catch
                    {
                        SizeInBytes = 0;
                        HumanReadableSize = HumanReadableSize.Unknown;
                    }
                });
        }
        else
        {
            SizeInBytes = 0;
            HumanReadableSize = HumanReadableSize.Unknown;
        }
    }

    public bool Is(FilePath? other) => Equals(Path, other);

    public bool Is(FilesystemEntry? other) => Equals(other) || Is(other?.Path);

    public override string ToString() => Path?.FullPath ?? DisplayNameOverride;


    public static FilesystemEntry MoveTo(FilesystemEntry entry, FilePath target, bool overwrite = false)
    {
        throw new NotImplementedException(); // TODO
    }

    public static FilesystemEntry CopyTo(FilesystemEntry entry, FilePath target, bool overwrite = false)
    {
        throw new NotImplementedException(); // TODO
    }

    public static bool Recycle(FilesystemEntry entry)
    {
        throw new NotImplementedException(); // TODO
    }


    public static FilesystemEntry Create(FilePath? path) => path is null ? Unknown : Create(path, path.Name, null, false);

    public static FilesystemEntry Create(FilePath? path, string display_name_override, string? optional_special_display = null, FAttr? attributes = null) => Create(path, display_name_override, optional_special_display, true, attributes);

    public static FilesystemEntry Create(FilePath? path, string display_name_override, string? optional_special_display, bool is_readonly, FAttr? attributes = null)
    {
#if USE_FS_ENTRY_CACHE
        if (path is null || !_path_mapper.TryGetValue(path, out FilesystemEntry? entry))
        {
#else
        FilesystemEntry
#endif
            entry = new(path, display_name_override, optional_special_display, is_readonly, attributes);
#if USE_FS_ENTRY_CACHE

            if (path is { })
                _path_mapper[path] = entry;
        }
#endif
        return entry;
    }

    private static DependencyProperty register_property<T>(string name, Action<FilesystemEntry, T> setter)
        where T : struct => DependencyProperty.Register(name, typeof(T?), typeof(FilesystemEntry), new PropertyMetadata(null, (s, e) =>
        {
            if (s is FilesystemEntry entry && (T?)e.NewValue is T value)
                setter(entry, value);
        }));

    public static implicit operator FilesystemEntry(FileSystemInfo info) => Create(info);

    public static implicit operator FilesystemEntry(FilePath? path) => Create(path);

    public static implicit operator FilesystemEntry(string path) => (FilePath?)path;


    private sealed class FilesystemEntrySeparator
        : FilesystemEntry
    {
        public override string AttributeString => "";

        public FilesystemEntrySeparator()
            : base(null, "", null, true, null)
        {
        }
    }
}

public sealed record FilePath(string Name, FilePath? RealParent = null)
{
    private static readonly string[] _executable_extensions = Environment.GetEnvironmentVariable("PATHEXT")?.Split(";") ?? Array.Empty<string>();
    private static readonly Regex CLSID_REGEX = new(@"((shell:)?::)?\{(?<clsid>[0-9a-f]{8}(\-[0-9a-f]{4}){3}\-[0-9a-f]{12})\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);


    public bool IsCLSID => CLSID_REGEX.IsMatch(Name) || CLSID_REGEX.IsMatch(FullPath);

    public bool IsRoot { get; } = RealParent is null;

    public string FullPath => ToString();

    public string ExtensionWithDot { get; } = Name.LastIndexOf('.') is int i and >= 0 ? Name[i..] : "";

    public bool HasExecutableExtension => _executable_extensions.Contains(ExtensionWithDot, StringComparer.InvariantCultureIgnoreCase);

    public FilePath Parent => RealParent ?? this;

    public FilePath Root => Parent is null ? this : Parent;

    public FilePath[] FullPathChain => (RealParent?.FullPathChain ?? Enumerable.Empty<FilePath>()).Append(this).ToArray();


    public override string ToString() => RealParent is null ? Name : $"{RealParent}\\{Name}";

    public FilePath Append(FilePath second) => second.FullPathChain.Aggregate(this, (c, p) => new(p.Name, this));

    public static FilePath? Concatenate(params FilePath?[] paths)
    {
        FilePath? path = null;

        foreach (var p in paths)
            if (p is not null)
                path = path?.Append(p) ?? p;

        return path;
    }

    public static FilePath? FromPath(string? path)
    {
        if (path is null)
            return null;

        path = path.Trim().Replace('/', '\\');

        if (path[0] == '"' && path[^1] == '"')
            path = path[1..^1];

        if (path[^1] == '\\')
            path = path[..^1];

        path = Environment.ExpandEnvironmentVariables(path.Trim());

        return FromPath(path.Split('\\'));
    }

    public static FilePath? FromPath(IEnumerable<string>? path) => path?.Aggregate(null as FilePath, (current, token) => token.Trim() switch
    {
        "" => current?.Root,
        "." => current,
        ".." => current?.Parent,
        string name => new FilePath(name, current)
    });

    public static FilePath operator +(FilePath first, FilePath second) => first.Append(second);

    public static implicit operator FilePath?(FileSystemInfo info) => FromPath(info.FullName);

    public static implicit operator FilePath?(string path) => FromPath(path);

    [return: NotNullIfNotNull("path")]
    public static implicit operator string?(FilePath? path) => path?.FullPath;
}









public class NativeProgressDialog
{
    private Win32IProgressDialog? _diag = null;
    private nint _hparent;

    private readonly string[] _lines = { "", "", "" };
    private string _cancel = string.Empty;
    private string _title = string.Empty;

    private uint value = 0;
    private uint maximum = 100;


    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            _diag?.SetTitle(_title);
        }
    }

    public string CancelMessage
    {
        get => _cancel;
        set
        {
            _cancel = value;
            _diag?.SetCancelMsg(_cancel, null);
        }
    }

    public string[] Lines => _lines;

    public string Line1
    {
        get => _line1;
        set
        {
            _line1 = value;
            if (_diag != null)
            {
                _diag.SetLine(1, _line1, false, IntPtr.Zero);
            }
        }
    }

    public string Line2
    {
        get => line2;
        set
        {
            line2 = value;
            if (_diag != null)
            {
                _diag.SetLine(2, line2, false, IntPtr.Zero);
            }
        }
    }

    public string Line3
    {
        get => line3;
        set
        {
            line3 = value;
            if (_diag != null)
            {
                _diag.SetLine(3, line3, false, IntPtr.Zero);
            }
        }
    }

    public uint Value
    {
        get => value;
        set
        {
            this.value = value;
            if (_diag != null)
            {
                _diag.SetProgress(this.value, maximum);
            }
        }
    }

    public uint Maximum
    {
        get => maximum;
        set
        {
            maximum = value;
            if (_diag != null)
            {
                _diag.SetProgress(this.value, maximum);
            }
        }
    }
    public bool HasUserCancelled
    {
        get
        {
            if (_diag != null)
            {
                return _diag.HasUserCancelled();
            }
            else
                return false;
        }
    }

    public NativeProgressDialog(IntPtr parentHandle)
    {
        this._hparent = parentHandle;
    }

    public void ShowDialog(params PROGDLG[] flags)
    {
        if (_diag == null)
        {
            _diag = (Win32IProgressDialog)new Win32ProgressDialog();

            _diag.SetTitle(_title);
            _diag.SetCancelMsg(_cancel, null);
            _diag.SetLine(1, _line1, false, IntPtr.Zero);
            _diag.SetLine(2, line2, false, IntPtr.Zero);
            _diag.SetLine(3, line3, false, IntPtr.Zero);

            PROGDLG dialogFlags = PROGDLG.Normal;
            if (flags.Length != 0)
            {
                dialogFlags = flags[0];
                for (var i = 1; i < flags.Length; i++)
                {
                    dialogFlags = dialogFlags | flags[i];
                }
            }

            _diag.StartProgressDialog(_hparent, null, dialogFlags, IntPtr.Zero);
        }
    }

    public void CloseDialog()
    {
        if (_diag != null)
        {
            _diag.StopProgressDialog();
            //Marshal.ReleaseComObject(pd);
            _diag = null;
        }
    }


    #region "Win32 Stuff"
    // The below was copied from: http://pinvoke.net/default.aspx/Interfaces/IProgressDialog.html

    public static class shlwapi
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
        static extern bool PathCompactPath(IntPtr hDC, [In, Out] StringBuilder pszPath, int dx);
    }

    [ComImport]
    [Guid("EBBC7C04-315E-11d2-B62F-006097DF5BD4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface Win32IProgressDialog
    {
        /// <summary>
        /// Starts the progress dialog box.
        /// </summary>
        /// <param name="hwndParent">A handle to the dialog box's parent window.</param>
        /// <param name="punkEnableModless">Reserved. Set to null.</param>
        /// <param name="dwFlags">Flags that control the operation of the progress dialog box. </param>
        /// <param name="pvResevered">Reserved. Set to IntPtr.Zero</param>
        void StartProgressDialog(
            IntPtr hwndParent, //HWND
            [MarshalAs(UnmanagedType.IUnknown)] object punkEnableModless, //IUnknown
            PROGDLG dwFlags,  //DWORD
            IntPtr pvResevered //LPCVOID
            );

        /// <summary>
        /// Stops the progress dialog box and removes it from the screen.
        /// </summary>
        void StopProgressDialog();

        /// <summary>
        /// Sets the title of the progress dialog box.
        /// </summary>
        /// <param name="pwzTitle">A pointer to a null-terminated Unicode string that contains the dialog box title.</param>
        void SetTitle(
            [MarshalAs(UnmanagedType.LPWStr)] string pwzTitle //LPCWSTR
            );

        /// <summary>
        /// Specifies an Audio-Video Interleaved (AVI) clip that runs in the dialog box. Note: Note  This method is not supported in Windows Vista or later versions.
        /// </summary>
        /// <param name="hInstAnimation">An instance handle to the module from which the AVI resource should be loaded.</param>
        /// <param name="idAnimation">An AVI resource identifier. To create this value, use the MAKEINTRESOURCE macro. The control loads the AVI resource from the module specified by hInstAnimation.</param>
        void SetAnimation(
            IntPtr hInstAnimation, //HINSTANCE
            ushort idAnimation //UINT
            );

        /// <summary>
        /// Checks whether the user has canceled the operation.
        /// </summary>
        /// <returns>TRUE if the user has cancelled the operation; otherwise, FALSE.</returns>
        /// <remarks>
        /// The system does not send a message to the application when the user clicks the Cancel button.
        /// You must periodically use this function to poll the progress dialog box object to determine
        /// whether the operation has been canceled.
        /// </remarks>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool HasUserCancelled();

        /// <summary>
        /// Updates the progress dialog box with the current state of the operation.
        /// </summary>
        /// <param name="dwCompleted">An application-defined value that indicates what proportion of the operation has been completed at the time the method was called.</param>
        /// <param name="dwTotal">An application-defined value that specifies what value dwCompleted will have when the operation is complete.</param>
        void SetProgress(
            uint dwCompleted, //DWORD
            uint dwTotal //DWORD
            );

        /// <summary>
        /// Updates the progress dialog box with the current state of the operation.
        /// </summary>
        /// <param name="ullCompleted">An application-defined value that indicates what proportion of the operation has been completed at the time the method was called.</param>
        /// <param name="ullTotal">An application-defined value that specifies what value ullCompleted will have when the operation is complete.</param>
        void SetProgress64(
            ulong ullCompleted, //ULONGLONG
            ulong ullTotal //ULONGLONG
            );

        /// <summary>
        /// Displays a message in the progress dialog.
        /// </summary>
        /// <param name="dwLineNum">The line number on which the text is to be displayed. Currently there are three lines—1, 2, and 3. If the PROGDLG_AUTOTIME flag was included in the dwFlags parameter when IProgressDialog::StartProgressDialog was called, only lines 1 and 2 can be used. The estimated time will be displayed on line 3.</param>
        /// <param name="pwzString">A null-terminated Unicode string that contains the text.</param>
        /// <param name="fCompactPath">TRUE to have path strings compacted if they are too large to fit on a line. The paths are compacted with PathCompactPath.</param>
        /// <param name="pvResevered"> Reserved. Set to IntPtr.Zero.</param>
        /// <remarks>This function is typically used to display a message such as "Item XXX is now being processed." typically, messages are displayed on lines 1 and 2, with line 3 reserved for the estimated time.</remarks>
        void SetLine(
            uint dwLineNum, //DWORD
            [MarshalAs(UnmanagedType.LPWStr)] string pwzString, //LPCWSTR
            [MarshalAs(UnmanagedType.VariantBool)] bool fCompactPath, //BOOL
            IntPtr pvResevered //LPCVOID
            );

        /// <summary>
        /// Sets a message to be displayed if the user cancels the operation.
        /// </summary>
        /// <param name="pwzCancelMsg">A pointer to a null-terminated Unicode string that contains the message to be displayed.</param>
        /// <param name="pvResevered">Reserved. Set to NULL.</param>
        /// <remarks>Even though the user clicks Cancel, the application cannot immediately call
        /// IProgressDialog::StopProgressDialog to close the dialog box. The application must wait until the
        /// next time it calls IProgressDialog::HasUserCancelled to discover that the user has canceled the
        /// operation. Since this delay might be significant, the progress dialog box provides the user with
        /// immediate feedback by clearing text lines 1 and 2 and displaying the cancel message on line 3.
        /// The message is intended to let the user know that the delay is normal and that the progress dialog
        /// box will be closed shortly.
        /// It is typically is set to something like "Please wait while ...". </remarks>
        void SetCancelMsg(
            [MarshalAs(UnmanagedType.LPWStr)] string pwzCancelMsg, //LPCWSTR
            object pvResevered //LPCVOID
            );

        /// <summary>
        /// Resets the progress dialog box timer to zero.
        /// </summary>
        /// <param name="dwTimerAction">Flags that indicate the action to be taken by the timer.</param>
        /// <param name="pvResevered">Reserved. Set to NULL.</param>
        /// <remarks>
        /// The timer is used to estimate the remaining time. It is started when your application
        /// calls IProgressDialog::StartProgressDialog. Unless your application will start immediately,
        /// it should call Timer just before starting the operation.
        /// This practice ensures that the time estimates will be as accurate as possible. This method
        /// should not be called after the first call to IProgressDialog::SetProgress.</remarks>
        void Timer(
            PDTIMER dwTimerAction, //DWORD
            object pvResevered //LPCVOID
            );

    }

    [ComImport]
    [Guid("F8383852-FCD3-11d1-A6B9-006097DF5BD4")]
    public class Win32ProgressDialog
    {
    }

    /// <summary>
    /// Flags that indicate the action to be taken by the ProgressDialog.SetTime() method.
    /// </summary>
    public enum PDTIMER : uint //DWORD
    {
        /// <summary>Resets the timer to zero. Progress will be calculated from the time this method is called.</summary>
        Reset = (0x01),
        /// <summary>Progress has been suspended.</summary>
        Pause = (0x02),
        /// <summary>Progress has been resumed.</summary>
        Resume = (0x03)
    }

    [Flags]
    public enum PROGDLG : uint //DWORD
    {
        /// <summary>Normal progress dialog box behavior.</summary>
        Normal = 0x00000000,
        /// <summary>The progress dialog box will be modal to the window specified by hwndParent. By default, a progress dialog box is modeless.</summary>
        Modal = 0x00000001,
        /// <summary>Automatically estimate the remaining time and display the estimate on line 3. </summary>
        /// <remarks>If this flag is set, IProgressDialog::SetLine can be used only to display text on lines 1 and 2.</remarks>
        AutoTime = 0x00000002,
        /// <summary>Do not show the "time remaining" text.</summary>
        NoTime = 0x00000004,
        /// <summary>Do not display a minimize button on the dialog box's caption bar.</summary>
        NoMinimize = 0x00000008,
        /// <summary>Do not display a progress bar.</summary>
        /// <remarks>Typically, an application can quantitatively determine how much of the operation remains and periodically pass that value to IProgressDialog::SetProgress. The progress dialog box uses this information to update its progress bar. This flag is typically set when the calling application must wait for an operation to finish, but does not have any quantitative information it can use to update the dialog box.</remarks>
        NoProgressBar = 0x00000010
    }
    #endregion
}


