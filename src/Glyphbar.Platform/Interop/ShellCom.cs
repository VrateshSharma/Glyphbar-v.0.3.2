using System.Runtime.InteropServices;

namespace Glyphbar.Platform.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
    public PROPERTYKEY(string id, uint p) { fmtid = new Guid(id); pid = p; }
}

[StructLayout(LayoutKind.Sequential)]
public struct SIZE
{
    public int cx, cy;
}

[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [PreserveSig] int BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    [PreserveSig] int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
    [PreserveSig] int GetDisplayName(uint sigdn, out nint name);
    [PreserveSig] int GetAttributes(uint mask, out uint attributes);
    [PreserveSig] int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
}

[ComImport, Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem2
{
    // IShellItem, redeclared because classic COM interop does not flatten inherited vtable slots.
    [PreserveSig] int BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    [PreserveSig] int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
    [PreserveSig] int GetDisplayName(uint sigdn, out nint name);
    [PreserveSig] int GetAttributes(uint mask, out uint attributes);
    [PreserveSig] int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
    // IShellItem2
    [PreserveSig] int GetPropertyStore(int flags, ref Guid riid, out nint ppv);
    [PreserveSig] int GetPropertyStoreWithCreateObject(int flags, nint createObject, ref Guid riid, out nint ppv);
    [PreserveSig] int GetPropertyStoreForKeys(nint keys, uint count, int flags, ref Guid riid, out nint ppv);
    [PreserveSig] int GetPropertyDescriptionList(ref PROPERTYKEY key, ref Guid riid, out nint ppv);
    [PreserveSig] int Update(nint pbc);
    [PreserveSig] int GetProperty(ref PROPERTYKEY key, nint propvar);
    [PreserveSig] int GetCLSID(ref PROPERTYKEY key, out Guid clsid);
    [PreserveSig] int GetFileTime(ref PROPERTYKEY key, out long fileTime);
    [PreserveSig] int GetInt32(ref PROPERTYKEY key, out int value);
    [PreserveSig] int GetString(ref PROPERTYKEY key, out nint value);
}

[ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumShellItems
{
    [PreserveSig] int Next(uint count, [MarshalAs(UnmanagedType.Interface)] out IShellItem item, out uint fetched);
    [PreserveSig] int Skip(uint count);
    [PreserveSig] int Reset();
    [PreserveSig] int Clone([MarshalAs(UnmanagedType.Interface)] out IEnumShellItems clone);
}

[ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemImageFactory
{
    [PreserveSig] int GetImage(SIZE size, uint flags, out nint bitmap);
}

[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
    [PreserveSig] int GetValue(ref PROPERTYKEY key, nint propvar);
}

public static class Shell
{
    public const uint SIGDN_NORMALDISPLAY = 0, SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
    public const uint SIIGBF_RESIZETOFIT = 0, SIIGBF_BIGGERSIZEOK = 1, SIIGBF_ICONONLY = 4;

    public static readonly Guid FOLDERID_AppsFolder = new("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
    public static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    public static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    public static readonly Guid IID_IEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");
    public static readonly Guid IID_IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
    public static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");

    public static PROPERTYKEY PKEY_Link_TargetParsingPath = new("B9B4B3FC-2B51-4A42-B5D8-324146AFCF25", 2);
    public static PROPERTYKEY PKEY_AppUserModel_ID = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3", 5);

    [DllImport("shell32", CharSet = CharSet.Unicode, PreserveSig = true)]
    public static extern int SHGetKnownFolderItem(ref Guid folder, int flags, nint token, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [DllImport("shell32", CharSet = CharSet.Unicode, PreserveSig = true)]
    public static extern int SHCreateItemFromParsingName(string path, nint bindCtx, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);

    [DllImport("shell32", PreserveSig = true)]
    public static extern int SHGetPropertyStoreForWindow(nint hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("shell32", CharSet = CharSet.Unicode, PreserveSig = true)]
    public static extern int SHParseDisplayName(string name, nint bindCtx, out nint pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32", PreserveSig = true)]
    public static extern int SHOpenFolderAndSelectItems(nint pidlFolder, uint count, nint[]? items, uint flags);

    [DllImport("ole32")] public static extern void CoTaskMemFree(nint ptr);
    [DllImport("ole32")] public static extern int PropVariantClear(nint propvar);

    /// <summary>Reads and frees a shell-allocated string.</summary>
    public static string? TakeString(nint ptr)
    {
        if (ptr == 0) return null;
        try { return Marshal.PtrToStringUni(ptr); }
        finally { CoTaskMemFree(ptr); }
    }

    /// <summary>Reads a VT_LPWSTR property. PROPVARIANT is 24 bytes on x64; vt at 0, the pointer at 8.</summary>
    public static string? ReadStringProperty(IPropertyStore store, ref PROPERTYKEY key)
    {
        const ushort VT_LPWSTR = 31;
        nint variant = Marshal.AllocCoTaskMem(24);
        try
        {
            for (int i = 0; i < 24; i += 8) Marshal.WriteInt64(variant, i, 0);
            if (store.GetValue(ref key, variant) != 0) return null;
            try
            {
                return (ushort)Marshal.ReadInt16(variant) == VT_LPWSTR
                    ? Marshal.PtrToStringUni(Marshal.ReadIntPtr(variant, 8))
                    : null;
            }
            finally { PropVariantClear(variant); }
        }
        finally { Marshal.FreeCoTaskMem(variant); }
    }
}
