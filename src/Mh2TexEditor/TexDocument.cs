using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Mh2TexEditor;

internal enum PreviewChannel
{
    Rgba = 0,
    Alpha = 1,
    Red = 2,
    Green = 3,
    Blue = 4,
}

internal sealed record TextureInfo(
    int Index,
    string Name,
    string Format,
    uint Width,
    uint Height,
    uint MipCount,
    uint SlotSize,
    bool Changed,
    int NameOccurrence = 1,
    int NameCount = 1)
{
    public string DisplayName => NameCount > 1 ? $"{Name}  [{NameOccurrence}/{NameCount}]" : Name;
    public string ExportStem => NameCount > 1 ? $"{Name}__{NameOccurrence}" : Name;
}

internal sealed class TexDocument : IDisposable
{
    private readonly SafeTexHandle handle;

    private TexDocument(string path, SafeTexHandle handle)
    {
        Path = path;
        this.handle = handle;
    }

    public string Path { get; private set; }

    public static TexDocument Open(string path)
    {
        var handle = NativeMethods.Open(path);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw NativeFailure("无法打开 TEX");
        }
        return new TexDocument(System.IO.Path.GetFullPath(path), handle);
    }

    public IReadOnlyList<TextureInfo> GetTextures()
    {
        var count = NativeMethods.TextureCount(handle);
        if (count < 0)
            throw NativeFailure("无法读取贴图数量");
        var result = new List<TextureInfo>(count);
        for (var index = 0; index < count; index++)
        {
            Check(NativeMethods.GetTextureInfo(handle, index, out var info), "无法读取贴图信息");
            result.Add(new TextureInfo(
                index,
                info.Name,
                info.Format,
                info.Width,
                info.Height,
                info.MipCount,
                info.SlotSize,
                info.Changed != 0));
        }
        var nameCounts = result
            .GroupBy(texture => texture.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < result.Count; index++)
        {
            var texture = result[index];
            occurrences.TryGetValue(texture.Name, out var occurrence);
            occurrence++;
            occurrences[texture.Name] = occurrence;
            result[index] = texture with
            {
                NameOccurrence = occurrence,
                NameCount = nameCounts[texture.Name],
            };
        }
        return result;
    }

    public Bitmap RenderPreview(int index, uint mipLevel, bool original, PreviewChannel channel)
    {
        Check(
            NativeMethods.RenderPreview(
                handle,
                index,
                mipLevel,
                original ? 1 : 0,
                (int)channel,
                out var source,
                out var width,
                out var height,
                out var sourceStride),
            "无法解码贴图预览");

        try
        {
            var bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var rowBytes = checked((int)width * 4);
                var row = new byte[rowBytes];
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(
                        IntPtr.Add(source, checked((int)(y * sourceStride))),
                        row,
                        0,
                        rowBytes);
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, checked((int)y * data.Stride)), rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
        finally
        {
            NativeMethods.FreeBuffer(source);
        }
    }

    public void ExportPng(int index, string path, bool original) =>
        Check(NativeMethods.ExportPng(handle, index, path, original ? 1 : 0), "导出 PNG 失败");

    public void ReplacePng(int index, string path) =>
        Check(NativeMethods.ReplacePng(handle, index, path), "替换贴图失败");

    public void RevertTexture(int index) =>
        Check(NativeMethods.RevertTexture(handle, index), "还原贴图失败");

    public bool CanUndo => NativeMethods.CanUndo(handle) != 0;
    public bool CanRedo => NativeMethods.CanRedo(handle) != 0;
    public bool IsDirty => NativeMethods.IsDirty(handle) != 0;

    public void Undo() => Check(NativeMethods.Undo(handle), "撤销失败");
    public void Redo() => Check(NativeMethods.Redo(handle), "重做失败");

    public void SaveAs(string path)
    {
        Check(NativeMethods.SaveAs(handle, path), "保存 TEX 失败");
        Path = System.IO.Path.GetFullPath(path);
    }

    public void Dispose() => handle.Dispose();

    private static void Check(int result, string context)
    {
        if (result == 0)
            throw NativeFailure(context);
    }

    private static InvalidOperationException NativeFailure(string context)
    {
        var detail = NativeMethods.LastError();
        return new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? context : $"{context}\n\n{detail}");
    }
}
