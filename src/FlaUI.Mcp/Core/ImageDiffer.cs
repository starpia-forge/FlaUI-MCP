using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FlaUI.Mcp.Core;

public readonly record struct ImageDiffResult(
    bool SameSize,
    Rectangle? ChangedBounds,
    int ChangedPixels,
    int TotalPixels,
    Size BeforeSize,
    Size AfterSize);

public static class ImageDiffer
{
    public static ImageDiffResult Compare(byte[] beforePng, byte[] afterPng, int threshold)
    {
        using var beforeStream = new MemoryStream(beforePng);
        using var before = (Bitmap)Image.FromStream(beforeStream);
        using var afterStream = new MemoryStream(afterPng);
        using var after = (Bitmap)Image.FromStream(afterStream);

        if (before.Size != after.Size)
            return new ImageDiffResult(false, null, 0, 0, before.Size, after.Size);

        return CompareSameSize(before, after, threshold);
    }

    private static ImageDiffResult CompareSameSize(Bitmap before, Bitmap after, int threshold)
    {
        var rect = new Rectangle(0, 0, before.Width, before.Height);
        int stride = ((before.Width * 4) + 3) & ~3;

        var beforeData = before.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] bPixels = new byte[Math.Abs(beforeData.Stride) * before.Height];
        Marshal.Copy(beforeData.Scan0, bPixels, 0, bPixels.Length);
        before.UnlockBits(beforeData);

        var afterData = after.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] aPixels = new byte[Math.Abs(afterData.Stride) * after.Height];
        Marshal.Copy(afterData.Scan0, aPixels, 0, aPixels.Length);
        after.UnlockBits(afterData);

        int actualStride = Math.Abs(beforeData.Stride);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        int changed = 0;

        for (int y = 0; y < before.Height; y++)
        {
            int rowBase = y * actualStride;
            for (int x = 0; x < before.Width; x++)
            {
                int off = rowBase + x * 4; // BGRA
                int db = Math.Abs(bPixels[off]     - aPixels[off]);
                int dg = Math.Abs(bPixels[off + 1] - aPixels[off + 1]);
                int dr = Math.Abs(bPixels[off + 2] - aPixels[off + 2]);
                if (db > threshold || dg > threshold || dr > threshold)
                {
                    changed++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        Rectangle? bounds = changed == 0
            ? null
            : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);

        return new ImageDiffResult(true, bounds, changed,
            before.Width * before.Height, before.Size, after.Size);
    }
}
