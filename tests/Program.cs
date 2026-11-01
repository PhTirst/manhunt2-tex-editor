using System.Drawing.Imaging;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Mh2TexEditor;

internal static class Program
{
    static string output = "";
    static int passed, failed;

    [STAThread]
    static int Main(string[] args)
    {
        output = Path.GetFullPath(args.FirstOrDefault() ?? "out/test-results");
        Directory.CreateDirectory(output);
        foreach (bool compressed in new[] { false, true })
        {
            foreach (bool wrapped in new[] { false, true })
            {
                string name = $"{(compressed ? "bc3" : "bgra")}-{(wrapped ? "z2hm" : "raw")}";
                Test(name + " preview, mipmaps, replacement, undo, save, duplicate isolation", () => RoundTrip(name, compressed, wrapped));
            }
        }
        Test("Invalid files produce useful errors", () =>
        {
            string path = Path.Combine(output, "invalid.tex");
            File.WriteAllText(path, "not a TEX file");
            Reject(() => TexDocument.Open(path), "TCDT");
        });
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed. Artifacts: {output}");
        return failed == 0 ? 0 : 1;
    }

    static void RoundTrip(string name, bool compressed, bool wrapped)
    {
        string source = Fixture(name, compressed, wrapped);
        byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
        using var doc = TexDocument.Open(source);
        var before = doc.GetTextures();
        Check(before.Select(t => t.Name).SequenceEqual(["z_last", "duplicate", "duplicate"]), "Record order changed");
        Check(before[1].NameOccurrence == 1 && before[2].NameOccurrence == 2 && before[1].NameCount == 2, "Duplicate numbering");
        Check(before.Select(t => t.ExportStem).Distinct().Count() == before.Count, "Export names collide");
        var selected = before[1];
        Check(selected.MipCount == 2, "Wrong mip count");
        using var mip = doc.RenderPreview(selected.Index, 1, true, PreviewChannel.Rgba);
        Check(mip.Width == 4 && mip.Height == 4, "Mip dimensions");
        using var alpha = doc.RenderPreview(selected.Index, 0, true, PreviewChannel.Alpha);
        Check(alpha.GetPixel(0, 0).R == 255 && alpha.GetPixel(0, 0).A == 255, "Alpha preview");
        using var sibling = doc.RenderPreview(before[2].Index, 0, true, PreviewChannel.Rgba);
        byte[] siblingHash = ImageHash(sibling);
        string exported = Path.Combine(output, name + "-export.png");
        doc.ExportPng(selected.Index, exported, true);
        Check(File.Exists(exported), "Export missing");
        string wrongSize = Png(name + "-wrong.png", 4, 4, Color.White);
        Reject(() => doc.ReplacePng(selected.Index, wrongSize), "dimensions");
        Check(!doc.IsDirty, "Failed edit mutated document");
        string replacement = Png(name + "-new.png", 8, 8, Color.Magenta);
        doc.ReplacePng(selected.Index, replacement);
        Check(doc.IsDirty && doc.CanUndo, "Edit history missing");
        doc.Undo();
        Check(!doc.IsDirty && doc.CanRedo, "Undo failed");
        doc.Redo();
        Check(doc.IsDirty, "Redo failed");
        using var original = doc.RenderPreview(selected.Index, 0, true, PreviewChannel.Rgba);
        using var changed = doc.RenderPreview(selected.Index, 0, false, PreviewChannel.Rgba);
        Check(!ImageHash(original).SequenceEqual(ImageHash(changed)), "Current and original are identical after edit");
        string saved = Path.Combine(output, name + "-edited.tex");
        doc.SaveAs(saved);
        Check(!doc.IsDirty && !doc.CanUndo, "Save did not reset baseline");
        using var reopened = TexDocument.Open(saved);
        Check(reopened.GetTextures().Select(t => t.Name).SequenceEqual(before.Select(t => t.Name)), "Saved record order changed");
        Check(reopened.GetTextures().All(t => t.MipCount == 2 && t.Width == 8 && t.Height == 8), "Layout changed");
        using var after = reopened.RenderPreview(selected.Index, 0, true, PreviewChannel.Rgba);
        Color pixel = after.GetPixel(0, 0);
        Check(pixel.R > 200 && pixel.B > 200 && pixel.G < 50, "Replacement pixels incorrect");
        using var siblingAfter = reopened.RenderPreview(before[2].Index, 0, true, PreviewChannel.Rgba);
        Check(ImageHash(siblingAfter).SequenceEqual(siblingHash), "Other same-name record was overwritten");
        Check(SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(hash), "Original file was modified");
        doc.ReplacePng(selected.Index, replacement);
        doc.RevertTexture(selected.Index);
        Check(!doc.IsDirty, "Revert failed");
    }

    static string Fixture(string name, bool compressed, bool wrapped)
    {
        string[] names = ["z_last", "duplicate", "duplicate"];
        const int recordStart = 64, recordSize = 128;
        int ddsSize = 128 + (compressed ? 80 : (64 + 16) * 4);
        int dataStart = recordStart + names.Length * recordSize;
        byte[] bytes = new byte[dataStart + names.Length * ddsSize];
        void U32(int offset, int value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
        U32(0, 0x54444354); U32(32, names.Length); U32(36, recordStart + 8);
        for (int i = 0; i < names.Length; i++)
        {
            int record = recordStart + i * recordSize;
            int dds = dataStart + i * ddsSize;
            U32(record + 8, recordStart + ((i + 1) % names.Length) * recordSize + 8);
            Encoding.ASCII.GetBytes(names[i]).CopyTo(bytes, record + 16);
            U32(record + 80, 8); U32(record + 84, 8); U32(record + 88, 32);
            U32(record + 92, 32); U32(record + 100, 2);
            U32(record + 104, dds); U32(record + 112, ddsSize);
            U32(dds, 0x20534444); U32(dds + 4, 124);
            U32(dds + 8, compressed ? 0xa1007 : 0x2100f);
            U32(dds + 12, 8); U32(dds + 16, 8); U32(dds + 20, compressed ? 64 : 32);
            U32(dds + 28, 2); U32(dds + 76, 32);
            U32(dds + 108, 0x401008);
            if (compressed)
            {
                U32(dds + 80, 4); U32(dds + 84, 0x35545844);
                for (int p = dds + 128; p < dds + ddsSize; p += 16)
                {
                    bytes[p] = 255; bytes[p + 1] = 255;
                    ushort color = i == 2 ? (ushort)0x001f : (ushort)0xf800;
                    BitConverter.GetBytes(color).CopyTo(bytes, p + 8);
                    BitConverter.GetBytes(color).CopyTo(bytes, p + 10);
                }
            }
            else
            {
                U32(dds + 80, 0x41); U32(dds + 88, 32);
                U32(dds + 92, 0xff0000); U32(dds + 96, 0xff00); U32(dds + 100, 0xff);
                U32(dds + 104, unchecked((int)0xff000000));
                for (int p = dds + 128; p < dds + ddsSize; p += 4)
                {
                    bytes[p] = (byte)(i * 70); bytes[p + 1] = 180; bytes[p + 2] = 40; bytes[p + 3] = 255;
                }
            }
        }
        string path = Path.Combine(output, name + ".tex");
        if (wrapped)
        {
            using var stream = File.Create(path);
            stream.Write("Z2HM"u8); stream.Write(BitConverter.GetBytes(bytes.Length));
            using var zlib = new ZLibStream(stream, CompressionLevel.Fastest);
            zlib.Write(bytes);
        }
        else File.WriteAllBytes(path, bytes);
        return path;
    }

    static string Png(string name, int width, int height, Color color)
    {
        string path = Path.Combine(output, name);
        using var image = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(color); image.Save(path, ImageFormat.Png); return path;
    }
    static byte[] ImageHash(Bitmap image)
    {
        using var stream = new MemoryStream(); image.Save(stream, ImageFormat.Png); return SHA256.HashData(stream.ToArray());
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string contains)
    {
        try { action(); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains(contains, StringComparison.OrdinalIgnoreCase), ex.Message); return; }
        throw new Exception("Expected error: " + contains);
    }
    static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + "\n" + ex); }
    }
}
