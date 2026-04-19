using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace WinNotch.Helpers;

public static class BackdropBlurHelper
{
    // Reusable GDI+ bitmap — avoids allocation each frame
    private static Bitmap? _captureBuf;
    private static int _bufW, _bufH;

    /// <summary>
    /// Captures a screen region with subtle padding for refraction directly into a
    /// WriteableBitmap (zero-alloc per frame). GPU BlurEffect handles all blurring.
    /// </summary>
    public static void CaptureInto(WriteableBitmap wb, int x, int y, int width, int height, int padding = 4)
    {
        if (width < 4 || height < 4) return;

        // Padded capture region (slight magnification = refraction)
        int cx = Math.Max(0, x - padding);
        int cy = Math.Max(0, y - padding);
        int cw = width + padding * 2;
        int ch = height + padding * 2;

        try
        {
            // Reuse or recreate the GDI+ capture buffer
            if (_captureBuf == null || _bufW != cw || _bufH != ch)
            {
                _captureBuf?.Dispose();
                _captureBuf = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
                _bufW = cw;
                _bufH = ch;
            }

            using (var g = Graphics.FromImage(_captureBuf))
            {
                g.CopyFromScreen(cx, cy, 0, 0, new System.Drawing.Size(cw, ch), CopyPixelOperation.SourceCopy);
            }

            // Lock source pixels
            var srcData = _captureBuf.LockBits(
                new Rectangle(0, 0, cw, ch),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            // Write directly into the WriteableBitmap (target size)
            wb.Lock();
            try
            {
                // Fast nearest-neighbor stretch from padded capture → target size
                unsafe
                {
                    byte* src = (byte*)srcData.Scan0;
                    byte* dst = (byte*)wb.BackBuffer;
                    int srcStride = srcData.Stride;
                    int dstStride = wb.BackBufferStride;

                    for (int dy = 0; dy < height; dy++)
                    {
                        int sy = dy * ch / height;
                        byte* srcRow = src + sy * srcStride;
                        byte* dstRow = dst + dy * dstStride;

                        for (int dx = 0; dx < width; dx++)
                        {
                            int sx = dx * cw / width;
                            int si = sx * 4;
                            int di = dx * 4;
                            *(int*)(dstRow + di) = *(int*)(srcRow + si);
                        }
                    }
                }

                wb.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                wb.Unlock();
            }

            _captureBuf.UnlockBits(srcData);
        }
        catch
        {
            // Silently ignore capture failures
        }
    }
}
