using System.Runtime.InteropServices;

namespace __native__;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214e4-0000-0000-c000-000000000046")]
internal interface IContextMenu
{
    [PreserveSig]
    nint QueryContextMenu(nint hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, CMF uFlags);

    void InvokeCommand(ref CMINVOKECOMMANDINFO pici);

    [PreserveSig]
    nint GetCommandString(int idcmd, uint uflags, int reserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder commandstring, int cch);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214f4-0000-0000-c000-000000000046")]
internal interface IContextMenu2
    : IContextMenu
{
    [PreserveSig]
    new nint QueryContextMenu(nint hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, CMF uFlags);

    void InvokeCommand(ref CMINVOKECOMMANDINFO_ByIndex pici);

    [PreserveSig]
    new nint GetCommandString(int idcmd, uint uflags, int reserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder commandstring, int cch);

    [PreserveSig]
    nint HandleMenuMsg(int uMsg, nint wParam, nint lParam);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
internal interface IContextMenu3
    : IContextMenu2
{
    [PreserveSig]
    new nint QueryContextMenu(nint hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, CMF uFlags);

    [PreserveSig]
    new nint InvokeCommand(ref CMINVOKECOMMANDINFO pici);

    [PreserveSig]
    new nint GetCommandString(int idcmd, uint uflags, int reserved, [MarshalAs(UnmanagedType.LPStr)] StringBuilder commandstring, int cch);

    [PreserveSig]
    new nint HandleMenuMsg(int uMsg, nint wParam, nint lParam);

    [PreserveSig]
    nint HandleMenuMsg2(int uMsg, nint wParam, nint lParam, out IntPtr plResult);
}


class lol
{
    public static bool GetIContextMenu(IShellFolder parent, nint[] pidls, out nint icontextMenuPtr, out IContextMenu? iContextMenu)
    {
        if (parent.GetUIObjectOf(default, (uint)pidls.Length, pidls, ref ShellAPI.IID_IContextMenu, default, out icontextMenuPtr) == ShellAPI.S_OK)
            iContextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(icontextMenuPtr, typeof(IContextMenu));
        else
        {
            icontextMenuPtr = default;
            iContextMenu = null;
        }

        return iContextMenu is { };
    }
}