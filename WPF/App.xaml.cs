using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace TabbedExplorer.WPF;


public partial class App
    : Application
{
    public static DirectoryInfo AppDir { get; } = new FileInfo(typeof(App).Assembly.Location).Directory!;

    public static DirectoryInfo SettingsDir { get; } = AppDir.CreateSubdirectory("settings");

    public static AppSettings Settings { get; } = new(new(Path.Combine(SettingsDir.FullName, "settings.json")));


    private volatile bool _isrunning;


    protected override void OnStartup(StartupEventArgs e)
    {
        Interop.PutFilesToClipboard(new string[] { @"C:\Users\david\Desktop\test.txt" }, DragDropEffects.Move);


        _isrunning = true;

        Task.Factory.StartNew(async () =>
        {
            Settings.OnStartup();

            while (_isrunning)
                if (Settings.HaveChangesBeenMade)
                    Settings.Save();
                else
                    await Task.Delay(1000);

            Settings.OnExit();
            Settings.Save();
        });

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isrunning = false;

        base.OnExit(e);
    }
}

public sealed class AppSettings
{
    private readonly ConcurrentDictionary<string, object?> _settings = new();
    private volatile bool _changes_made = false;
    private readonly FileInfo _file;


    public bool HaveChangesBeenMade => _changes_made;

    public string FolderCacheName
    {
        get => Get(nameof(FolderCacheName), "foldercache.bin");
        set => Set(nameof(FolderCacheName), value);
    }

    public TimeSpan FolderCacheInvalidationInterval
    {
        get => Get(nameof(FolderCacheInvalidationInterval), TimeSpan.FromMinutes(15));
        set => Set(nameof(FolderCacheInvalidationInterval), value);
    }


    public AppSettings(FileInfo settings_file)
    {
        _file = settings_file;

        Load();
    }

    private T Set<T>(string name, T value)
    {
        _changes_made = true;
        _settings[name] = value;

        return value;
    }

    private T Get<T>(string name, T @default) => _settings.TryGetValue(name, out object? value) && value is T t ? t : Set(name, @default);

    public void Load()
    {
        try
        {
            Dictionary<string, object?> copy = new();

            using FileStream fs = new(_file.FullName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            using BinaryReader rd = new(fs);
            int count = rd.ReadInt32();

            while (count --> 0)
            {
                string key = rd.ReadString();
                object? value = ReadValue(rd);

                copy[key] = value;
            }

            rd.Close();
            fs.Close();

            _changes_made = false;

            foreach ((string key, object? value) in copy)
                _settings[key] = value;
        }
        catch
        {
        }
    }

    public void Save()
    {
        try
        {
            _changes_made = false;

            Dictionary<string, object?> copy = new(_settings);

            using FileStream fs = new(_file.FullName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            using BinaryWriter wr = new(fs);

            wr.Write(copy.Count);

            foreach ((string key, object? value) in copy)
            {
                wr.Write(key);
                WriteValue(wr, value);
            }

            fs.Flush();
            fs.Close();
        }
        catch
        {
        }
    }

    public void OnStartup()
    {
        FolderCache.StartCaching(FolderCacheInvalidationInterval, new(Path.Combine(App.SettingsDir.FullName, FolderCacheName)));
    }

    public void OnExit()
    {
        FolderCache.StopCaching();
    }

    private static void WriteValue(BinaryWriter wr, object? obj)
    {
        Type? type = obj?.GetType();

        wr.Write(type?.AssemblyQualifiedName ?? "null");

        if (obj is string str)
            wr.Write(str);
        else if (obj is DateTime dt)
            wr.Write(dt.Ticks);
        else if (obj is DateTimeOffset dto)
        {
            wr.Write(dto.Ticks);
            wr.Write(dto.Offset.Ticks);
        }
        else if (obj is TimeSpan ts)
            wr.Write(ts.Ticks);
        else if (obj is bool b)
            wr.Write(b);
        else if (obj is byte u8)
            wr.Write(u8);
        else if (obj is sbyte i8)
            wr.Write(i8);
        else if (obj is short i16)
            wr.Write(i16);
        else if (obj is ushort u16)
            wr.Write(u16);
        else if (obj is int i32)
            wr.Write(i32);
        else if (obj is uint u32)
            wr.Write(u32);
        else if (obj is long i64)
            wr.Write(i64);
        else if (obj is ulong u64)
            wr.Write(u64);
        else if (obj is nint ni64)
            wr.Write(ni64);
        else if (obj is nuint nu64)
            wr.Write(nu64);
        else if (obj is Half f16)
            wr.Write(f16);
        else if (obj is float f32)
            wr.Write(f32);
        else if (obj is double f64)
            wr.Write(f64);
        else if (obj is decimal f128)
            wr.Write(f128);
        else if (obj is Guid guid)
            wr.Write(guid.ToByteArray());
        else if (obj is byte[] arr)
        {
            wr.Write(arr.Length);
            wr.Write(arr);
        }
        else
        {
            throw new NotImplementedException();

            GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Pinned);

            // TODO : copy

            handle.Free();
        }
    }

    private static object? ReadValue(BinaryReader rd)
    {
        Type? type = null;

        try
        {
            string typename = rd.ReadString();

            type = Type.GetType(typename);
        }
        catch
        {
        }

        if (type is null)
            return null;
        else if (type == typeof(string))
            return rd.ReadString();
        else if (type == typeof(DateTime))
            return new DateTime(rd.ReadInt64());
        else if (type == typeof(DateTimeOffset))
        {
            long ticks = rd.ReadInt64();
            long offset = rd.ReadInt64();

            return new DateTimeOffset(ticks, new TimeSpan(offset));
        }
        else if (type == typeof(TimeSpan))
            return new TimeSpan(rd.ReadInt64());
        else if (type == typeof(bool))
            return rd.ReadBoolean();
        else if (type == typeof(byte))
            return rd.ReadByte();
        else if (type == typeof(sbyte))
            return rd.ReadSByte();
        else if (type == typeof(short))
            return rd.ReadInt16();
        else if (type == typeof(ushort))
            return rd.ReadUInt16();
        else if (type == typeof(int))
            return rd.ReadInt32();
        else if (type == typeof(uint))
            return rd.ReadUInt32();
        else if (type == typeof(long))
            return rd.ReadInt64();
        else if (type == typeof(ulong))
            return rd.ReadUInt64();
        else if (type == typeof(nint))
            return (nint)rd.ReadInt64();
        else if (type == typeof(nuint))
            return (nuint)rd.ReadUInt64();
        else if (type == typeof(Half))
            return rd.ReadHalf();
        else if (type == typeof(float))
            return rd.ReadSingle();
        else if (type == typeof(double))
            return rd.ReadDouble();
        else if (type == typeof(decimal))
            return rd.ReadDecimal();
        else if (type == typeof(Guid))
            return new Guid(rd.ReadBytes(16));
        else if (type == typeof(byte[]))
        {
            int length = rd.ReadInt32();

            return rd.ReadBytes(length);
        }
        else
            throw new NotImplementedException();
    }
}

public static class FolderCache
{
    private static readonly ConcurrentDictionary<int, (long size, DateTime last_updated)> _dircache = new();
    private static TimeSpan _timeout = TimeSpan.FromMinutes(15);
    private static volatile bool _updated = false;
    private static volatile bool _running = false;


    private static int GetKey(DirectoryInfo entry) => entry.FullName.ToLowerInvariant().Trim().Replace('\\', '/').GetHashCode();

    public static long GetSizeInBytes(DirectoryInfo entry)
    {
        int key = GetKey(entry);

        if (_dircache.TryGetValue(key, out (long size, DateTime last_updated) value) && value.last_updated + _timeout > DateTime.UtcNow)
            return value.size;
        else
        {
            long size = 0;
            FileInfo[] files = Array.Empty<FileInfo>();
            DirectoryInfo[] dirs = Array.Empty<DirectoryInfo>();

            try
            {
                files = entry.EnumerateFiles().ToArray();
            }
            catch
            {
            }

            try
            {
                dirs = entry.EnumerateDirectories().ToArray();
            }
            catch
            {
            }

            // iterate only after fetching in to keep the lock times to a minimum.

            foreach (FileInfo file in files)
                size += file.Length;

            foreach (DirectoryInfo subdir in dirs.AsParallel())
                Interlocked.Add(ref size, subdir.LinkTarget is string ? 0 : GetSizeInBytes(subdir));

            _dircache[key] = (size, DateTime.UtcNow);
            _updated = true;

            return size;
        }
    }

    public static void Save(FileInfo path)
    {
        try
        {
            using FileStream fs = new(path.FullName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            using BinaryWriter wr = new(fs);

            _updated = false;

            wr.Write(_dircache.Count);

            foreach ((int hash, (long size, DateTime updated) entry) in _dircache)
                if (entry.updated + _timeout > DateTime.UtcNow)
                {
                    wr.Write(hash);
                    wr.Write(entry.size);
                    wr.Write(entry.updated.Ticks);
                }

            fs.Flush();
            fs.Close();
        }
        catch
        {
        }
    }

    public static void Load(FileInfo path)
    {
        try
        {
            using FileStream fs = new(path.FullName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            using BinaryReader rd = new(fs);
            int count = rd.ReadInt32();

            _updated = false;

            while (count-- > 0)
            {
                int hash = rd.ReadInt32();
                long size = rd.ReadInt64();
                DateTime updated = new(rd.ReadInt64());

                if (updated + _timeout > DateTime.UtcNow)
                    _dircache[hash] = (size, updated);
            }
        }
        catch
        {
        }
    }

    public static void StartCaching(TimeSpan timeout, FileInfo path)
    {
        _timeout = timeout;
        _running = true;

        Task.Factory.StartNew(async delegate
        {
            Load(path);

            while (_running)
                if (!_updated)
                    await Task.Delay(1_000);
                else
                    Save(path);

            Save(path);
        });
    }

    public static void StopCaching() => _running = false;
}
