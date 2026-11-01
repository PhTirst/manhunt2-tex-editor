using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace Mh2TexEditor;

internal sealed class SafeTexHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeTexHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.Close(handle);
        return true;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct NativeTextureInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 48)]
    public string Format;

    public uint Width;
    public uint Height;
    public uint MipCount;
    public uint SlotSize;
    public int Changed;
}

internal static class NativeMethods
{
    private const string Library = "mh2core.dll";

    [DllImport(Library, EntryPoint = "mh2_tex_open", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    internal static extern SafeTexHandle Open(string path);

    [DllImport(Library, EntryPoint = "mh2_tex_close", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Close(IntPtr handle);

    [DllImport(Library, EntryPoint = "mh2_tex_texture_count", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int TextureCount(SafeTexHandle handle);

    [DllImport(Library, EntryPoint = "mh2_tex_get_texture_info", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetTextureInfo(SafeTexHandle handle, int index, out NativeTextureInfo info);

    [DllImport(Library, EntryPoint = "mh2_tex_render_preview", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RenderPreview(
        SafeTexHandle handle,
        int index,
        uint mipLevel,
        int useOriginal,
        int channel,
        out IntPtr pixels,
        out uint width,
        out uint height,
        out uint stride);

    [DllImport(Library, EntryPoint = "mh2_tex_free_buffer", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FreeBuffer(IntPtr buffer);

    [DllImport(Library, EntryPoint = "mh2_tex_export_png", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    internal static extern int ExportPng(SafeTexHandle handle, int index, string path, int useOriginal);

    [DllImport(Library, EntryPoint = "mh2_tex_replace_png", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    internal static extern int ReplacePng(SafeTexHandle handle, int index, string path);

    [DllImport(Library, EntryPoint = "mh2_tex_revert_texture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RevertTexture(SafeTexHandle handle, int index);

    [DllImport(Library, EntryPoint = "mh2_tex_can_undo", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CanUndo(SafeTexHandle handle);

    [DllImport(Library, EntryPoint = "mh2_tex_can_redo", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CanRedo(SafeTexHandle handle);

    [DllImport(Library, EntryPoint = "mh2_tex_undo", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Undo(SafeTexHandle handle);

    [DllImport(Library, EntryPoint = "mh2_tex_redo", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Redo(SafeTexHandle handle);

    [DllImport(Library, EntryPoint = "mh2_tex_is_dirty", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int IsDirty(SafeTexHandle handle);

    [DllImport(Library, EntryPoint = "mh2_tex_save_as", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    internal static extern int SaveAs(SafeTexHandle handle, string path);

    [DllImport(Library, EntryPoint = "mh2_get_last_error_utf8", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetLastErrorUtf8(IntPtr buffer, int bufferSize);

    internal static string LastError()
    {
        var required = Math.Max(1, GetLastErrorUtf8(IntPtr.Zero, 0));
        var buffer = Marshal.AllocHGlobal(required);
        try
        {
            GetLastErrorUtf8(buffer, required);
            var bytes = new byte[Math.Max(0, required - 1)];
            if (bytes.Length > 0)
                Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
