using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MouseHighlighter;

/// <summary>
/// Windows 기본 화살표 커서로 트레이 아이콘을 만듭니다.
/// 커서 그림은 위쪽 왼편에 작게 들어 있으므로, 화살표 부분만 잘라 아이콘을 꽉 채웁니다.
/// </summary>
internal static class TrayIcon
{
    private const int IconSize = 64;   // Windows가 트레이 크기에 맞게 줄여서 표시합니다
    private const int Padding = 1;     // 가장자리 여백 (IconSize 기준 픽셀)

    public static Icon CreateFromArrowCursor()
    {
        // 커서 파일에서 큰 크기로 다시 불러와야 줄였을 때 깔끔합니다
        IntPtr arrow = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW);
        IntPtr hCursor = NativeMethods.CopyImage(arrow, NativeMethods.IMAGE_CURSOR, 128, 128,
            NativeMethods.LR_COPYFROMRESOURCE);
        if (hCursor == IntPtr.Zero)
            return Icon.FromHandle(System.Windows.Forms.Cursors.Arrow.Handle);

        try
        {
            using Bitmap source = Icon.FromHandle(hCursor).ToBitmap();
            Rectangle bounds = FindOpaqueBounds(source);

            using var result = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // 비율을 유지하면서 가운데에 최대한 크게
                float area = IconSize - Padding * 2;
                float scale = Math.Min(area / bounds.Width, area / bounds.Height);
                float w = bounds.Width * scale, h = bounds.Height * scale;
                var dest = new RectangleF((IconSize - w) / 2, (IconSize - h) / 2, w, h);
                g.DrawImage(source, dest, bounds, GraphicsUnit.Pixel);
            }
            return Icon.FromHandle(result.GetHicon());
        }
        finally
        {
            NativeMethods.DestroyCursor(hCursor);
        }
    }

    /// <summary>투명하지 않은 픽셀이 있는 영역을 찾습니다.</summary>
    private static Rectangle FindOpaqueBounds(Bitmap bmp)
    {
        int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            if (bmp.GetPixel(x, y).A < 16) continue;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        return maxX < 0
            ? new Rectangle(0, 0, bmp.Width, bmp.Height)
            : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }
}
