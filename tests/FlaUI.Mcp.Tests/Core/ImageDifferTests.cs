using System.Drawing;
using System.Drawing.Imaging;
using FluentAssertions;
using FlaUI.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class ImageDifferTests
{
    private static byte[] BitmapToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static Bitmap SolidColor(int width, int height, Color color)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }

    [Fact]
    public void Compare_Identical_NoChange()
    {
        using var bmp = SolidColor(10, 10, Color.Blue);
        var png = BitmapToPng(bmp);
        var result = ImageDiffer.Compare(png, png, threshold: 10);
        result.SameSize.Should().BeTrue();
        result.ChangedBounds.Should().BeNull();
        result.ChangedPixels.Should().Be(0);
        result.TotalPixels.Should().Be(100);
    }

    [Fact]
    public void Compare_SinglePixelChange_DetectsBounds()
    {
        using var before = SolidColor(10, 10, Color.White);
        using var after  = SolidColor(10, 10, Color.White);
        after.SetPixel(3, 7, Color.Black);
        var result = ImageDiffer.Compare(BitmapToPng(before), BitmapToPng(after), threshold: 0);
        result.SameSize.Should().BeTrue();
        result.ChangedPixels.Should().Be(1);
        result.ChangedBounds.Should().NotBeNull();
        result.ChangedBounds!.Value.X.Should().Be(3);
        result.ChangedBounds!.Value.Y.Should().Be(7);
        result.ChangedBounds!.Value.Width.Should().Be(1);
        result.ChangedBounds!.Value.Height.Should().Be(1);
    }

    [Fact]
    public void Compare_RectangleArea_BoundsMatchArea()
    {
        using var before = SolidColor(20, 20, Color.White);
        using var after  = SolidColor(20, 20, Color.White);
        using var g = Graphics.FromImage(after);
        g.FillRectangle(Brushes.Red, new Rectangle(2, 3, 5, 4)); // x=2,y=3,w=5,h=4
        var result = ImageDiffer.Compare(BitmapToPng(before), BitmapToPng(after), threshold: 0);
        result.ChangedBounds.Should().NotBeNull();
        result.ChangedBounds!.Value.X.Should().Be(2);
        result.ChangedBounds!.Value.Y.Should().Be(3);
        result.ChangedBounds!.Value.Width.Should().Be(5);
        result.ChangedBounds!.Value.Height.Should().Be(4);
    }

    [Fact]
    public void Compare_DifferentSizes_ReturnsNotSameSize()
    {
        using var before = SolidColor(100, 100, Color.White);
        using var after  = SolidColor(50, 50, Color.White);
        var result = ImageDiffer.Compare(BitmapToPng(before), BitmapToPng(after), threshold: 10);
        result.SameSize.Should().BeFalse();
        result.ChangedBounds.Should().BeNull();
        result.BeforeSize.Should().Be(new Size(100, 100));
        result.AfterSize.Should().Be(new Size(50, 50));
    }

    [Fact]
    public void Compare_ChangeWithinThreshold_NoChange()
    {
        using var before = SolidColor(5, 5, Color.FromArgb(100, 100, 100));
        using var after  = SolidColor(5, 5, Color.FromArgb(105, 105, 105)); // delta=5
        var result = ImageDiffer.Compare(BitmapToPng(before), BitmapToPng(after), threshold: 10);
        result.ChangedPixels.Should().Be(0);
        result.ChangedBounds.Should().BeNull();
    }

    [Fact]
    public void Compare_ChangeExceedsThreshold_AllChanged()
    {
        using var before = SolidColor(4, 4, Color.FromArgb(100, 100, 100));
        using var after  = SolidColor(4, 4, Color.FromArgb(130, 130, 130)); // delta=30
        var result = ImageDiffer.Compare(BitmapToPng(before), BitmapToPng(after), threshold: 10);
        result.ChangedPixels.Should().Be(16);
        result.ChangedBounds.Should().NotBeNull();
        result.ChangedBounds!.Value.Should().Be(new Rectangle(0, 0, 4, 4));
    }
}
