using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace VinXiangQi
{
    class ImageHelper
    {
        public static Random Rand = new Random();
        [DllImport("msvcrt.dll")]
        private static extern int memcmp(IntPtr b1, IntPtr b2, IntPtr count);

        /// <summary>
        /// 统计两张同尺寸图片在 region 内不同的像素数，并给出差异的外接矩形。
        /// 用于判断“棋盘区域是否真的变了”（棋盘外的计时器、头像动画等不算）。
        /// </summary>
        public static int CountDiffInRegion(Bitmap b1, Bitmap b2, Rectangle region, out Rectangle diffBox)
        {
            diffBox = Rectangle.Empty;
            if (b1 == null || b2 == null || b1.Size != b2.Size) return int.MaxValue;
            region.Intersect(new Rectangle(Point.Empty, b1.Size));
            if (region.Width <= 0 || region.Height <= 0) return int.MaxValue;
            var full = new Rectangle(Point.Empty, b1.Size);
            var bd1 = b1.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var bd2 = b2.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] r1 = new int[region.Width], r2 = new int[region.Width];
                int count = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    long off = (long)y * bd1.Stride + region.Left * 4L;
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(bd1.Scan0.ToInt64() + off), r1, 0, region.Width);
                    off = (long)y * bd2.Stride + region.Left * 4L;
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(bd2.Scan0.ToInt64() + off), r2, 0, region.Width);
                    for (int i = 0; i < r1.Length; i++)
                    {
                        if (r1[i] != r2[i])
                        {
                            count++;
                            int x = region.Left + i;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
                if (count > 0) diffBox = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                return count;
            }
            finally
            {
                b1.UnlockBits(bd1);
                b2.UnlockBits(bd2);
            }
        }

        public static bool CompareMemCmp(Bitmap b1, Bitmap b2)
        {
            if ((b1 == null) != (b2 == null)) return false;
            if (b1.Size != b2.Size) return false;

            var bd1 = b1.LockBits(new Rectangle(new Point(0, 0), b1.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var bd2 = b2.LockBits(new Rectangle(new Point(0, 0), b2.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                IntPtr bd1scan0 = bd1.Scan0;
                IntPtr bd2scan0 = bd2.Scan0;
                int stride = bd1.Stride;
                int len = stride * b1.Height;
                return memcmp(bd1scan0, bd2scan0, (IntPtr)len) == 0;
            }
            finally
            {
                b1.UnlockBits(bd1);
                b2.UnlockBits(bd2);
            }
        }

        public static Bitmap RandomObstacle(Bitmap bitmap)
        {
            Bitmap nbmp = new Bitmap(bitmap);
            Graphics g = Graphics.FromImage(nbmp);
            Rectangle BoardRect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            Rectangle rect = new Rectangle(BoardRect.X + Rand.Next(0, BoardRect.Width * 2 / 3), BoardRect.Y + Rand.Next(0, BoardRect.Height * 2 / 3),
                  Rand.Next(BoardRect.Width / 5, BoardRect.Width / 2), Rand.Next(BoardRect.Height / 5, BoardRect.Height / 2));
            if (rect.X + rect.Width > BoardRect.Width) rect.Width = BoardRect.Width - rect.X;
            if (rect.Y + rect.Height > BoardRect.Height) rect.Height = BoardRect.Height - rect.Y;
            if (Rand.Next(2) == 0)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(Rand.Next(50, 130), Rand.Next(0, 255), Rand.Next(0, 255), Rand.Next(0, 255))), rect);
            }
            else
            {
                g.FillEllipse(new SolidBrush(Color.FromArgb(Rand.Next(50, 130), Rand.Next(0, 255), Rand.Next(0, 255), Rand.Next(0, 255))), rect);
            }
            return nbmp;
        }

        public static Bitmap AdjustImage(Bitmap bitmap, float brightness = 1.0f, float contrast = 1.0f, float gamma = 1.0f)
        {
            if (bitmap == null) return null;
            Bitmap adjustedImage = new Bitmap(bitmap.Width, bitmap.Height);
            float adjustedBrightness = brightness - 1.0f;
            // create matrix that will brighten and contrast the image
            float[][] ptsArray ={
        new float[] {contrast, 0, 0, 0, 0}, // scale red
        new float[] {0, contrast, 0, 0, 0}, // scale green
        new float[] {0, 0, contrast, 0, 0}, // scale blue
        new float[] {0, 0, 0, 1.0f, 0}, // don't scale alpha
        new float[] {adjustedBrightness, adjustedBrightness, adjustedBrightness, 0, 1}};

            ImageAttributes imageAttributes = new ImageAttributes();
            imageAttributes.ClearColorMatrix();
            imageAttributes.SetColorMatrix(new ColorMatrix(ptsArray), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            imageAttributes.SetGamma(gamma, ColorAdjustType.Bitmap);
            Graphics g = Graphics.FromImage(adjustedImage);
            g.DrawImage(bitmap, new Rectangle(0, 0, adjustedImage.Width, adjustedImage.Height)
                , 0, 0, bitmap.Width, bitmap.Height,
                GraphicsUnit.Pixel, imageAttributes);
            g.Dispose();
            return adjustedImage;
        }

        public static byte[] ShaHash(Image image)
        {
            var bytes = new byte[1];
            bytes = (byte[])(new ImageConverter()).ConvertTo(image, bytes.GetType());

            return (new SHA256Managed()).ComputeHash(bytes);
        }

        public static bool AreEqual(Image imageA, Image imageB)
        {
            if (imageA.Width != imageB.Width) return false;
            if (imageA.Height != imageB.Height) return false;

            // 直接逐字节比较内存，比原先的 SHA256 全图哈希快几个数量级
            return CompareMemCmp((Bitmap)imageA, (Bitmap)imageB);
        }

        public static Image CropImage(Image img, Rectangle cropArea)
        {
            try
            {
                return ((Bitmap)img).Clone(cropArea, img.PixelFormat);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "ImageHelper.CropImage");
                return new Bitmap(10, 10);
            }
        }

        public static bool DetectImage(Bitmap sample, Bitmap model)
        {

            int sWidth = sample.Width - model.Width, sHeight = sample.Height - model.Height;
            for (int y = 0; y < sHeight; y++)
            {
                for (int x = 0; x < sWidth; x++)
                {
                    if (CompareImage(sample, model, x, y, 30)) return true;
                }
            }
            return false;
        }

        public static Point FindImage(Bitmap sample, Bitmap model, int threshold = 30)
        {

            int sWidth = sample.Width - model.Width, sHeight = sample.Height - model.Height;
            for (int y = 0; y < sHeight; y++)
            {
                for (int x = 0; x < sWidth; x++)
                {
                    if (CompareImageFaster(sample, model, x, y, threshold)) return new Point(x, y);
                }
            }
            return new Point(-1, -1);
        }

        unsafe public static Point FindImageFromLeft(Bitmap sample, Bitmap model, int threshold = 30, int startFindX = 0, int startFindY = 0, int deltaX = int.MaxValue, int deltaY = int.MaxValue)
        {
            bool jumpOut = false;
            int endX = sample.Width - model.Width, endY = sample.Height - model.Height;
            endX = endX - startFindX <= deltaX ? endX : startFindX + deltaX;
            endY = endY - startFindY <= deltaY ? endY : startFindY + deltaY;
            int mWidth = model.Width;
            int mHeight = model.Height;
            int sWidth = sample.Width;
            int sHeight = sample.Height;
            BitmapData modelBMD = model.LockBits(new Rectangle(0, 0, model.Width, model.Height), ImageLockMode.ReadWrite, model.PixelFormat);
            BitmapData sampleBMD = sample.LockBits(new Rectangle(0, 0, sample.Width, sample.Height), ImageLockMode.ReadWrite, sample.PixelFormat);
            byte* sampleScan0 = (byte*)sampleBMD.Scan0;
            byte* modelScan0 = (byte*)modelBMD.Scan0;
            byte c1r, c1g, c1b, c2a, c2r, c2g, c2b;

            for (int startX = startFindX; startX < endX; startX++)
            {
                for (int startY = startFindY; startY < endY; startY++)
                {
                    for (int y = 0; y < mHeight; y++)
                    {
                        jumpOut = false;
                        if (y + startY >= sHeight) break;
                        for (int x = 0; x < mWidth; x++)
                        {
                            c2a = modelScan0[y * modelBMD.Stride + x * 4 + 3];
                            if (c2a != 0)
                            {
                                c1b = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4];
                                c1g = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 1];
                                c1r = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 2];
                                // c1a = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 3];
                                c2b = modelScan0[y * modelBMD.Stride + x * 4];
                                c2g = modelScan0[y * modelBMD.Stride + x * 4 + 1];
                                c2r = modelScan0[y * modelBMD.Stride + x * 4 + 2];
                                if (Math.Abs(c1r - c2r) + Math.Abs(c1g - c2g) + Math.Abs(c1b - c2b) > threshold) { jumpOut = true; break; }
                            }
                        }
                        if (jumpOut) break;
                    }
                    if (jumpOut) continue;
                    sample.UnlockBits(sampleBMD);
                    model.UnlockBits(modelBMD);
                    return new Point(startX, startY);
                }
            }
            sample.UnlockBits(sampleBMD);
            model.UnlockBits(modelBMD);
            return new Point(-1, -1);
        }

        unsafe public static Point FindImageFromTop(Bitmap sample, Bitmap model, int threshold = 50, int startFindX = 0, int startFindY = 0, int deltaX = int.MaxValue, int deltaY = int.MaxValue)
        {
            bool jumpOut = false;
            int endX = sample.Width - model.Width, endY = sample.Height - model.Height;
            endX = endX - startFindX <= deltaX ? endX : startFindX + deltaX;
            endY = endY - startFindY <= deltaY ? endY : startFindY + deltaY;
            int mWidth = model.Width;
            int mHeight = model.Height;
            int sWidth = sample.Width;
            int sHeight = sample.Height;
            BitmapData modelBMD = model.LockBits(new Rectangle(0, 0, model.Width, model.Height), ImageLockMode.ReadWrite, model.PixelFormat);
            BitmapData sampleBMD = sample.LockBits(new Rectangle(0, 0, sample.Width, sample.Height), ImageLockMode.ReadWrite, sample.PixelFormat);
            byte* sampleScan0 = (byte*)sampleBMD.Scan0;
            byte* modelScan0 = (byte*)modelBMD.Scan0;
            byte c1r, c1g, c1b, c2a, c2r, c2g, c2b;

            for (int startY = startFindY; startY < endY; startY++)
            {
                for (int startX = startFindX; startX < endX; startX++)
                {
                    for (int y = 0; y < mHeight; y++)
                    {
                        jumpOut = false;
                        if (y + startY >= sHeight) break;
                        for (int x = 0; x < mWidth; x++)
                        {
                            c2a = modelScan0[y * modelBMD.Stride + x * 4 + 3];
                            if (c2a != 0)
                            {
                                c1b = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4];
                                c1g = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 1];
                                c1r = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 2];
                                // c1a = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 3];
                                c2b = modelScan0[y * modelBMD.Stride + x * 4];
                                c2g = modelScan0[y * modelBMD.Stride + x * 4 + 1];
                                c2r = modelScan0[y * modelBMD.Stride + x * 4 + 2];
                                if (Math.Abs(c1r - c2r) + Math.Abs(c1g - c2g) + Math.Abs(c1b - c2b) > threshold) { jumpOut = true; break; }
                            }
                        }
                        if (jumpOut) break;
                    }
                    if (jumpOut) continue;
                    sample.UnlockBits(sampleBMD);
                    model.UnlockBits(modelBMD);
                    return new Point(startX, startY);
                }
            }
            sample.UnlockBits(sampleBMD);
            model.UnlockBits(modelBMD);
            return new Point(-1, -1);
        }

        unsafe public static Point FindImageFromRight(Bitmap sample, Bitmap model, int threshold = 30, int startFindX = 0, int startFindY = 0, int deltaX = int.MaxValue, int deltaY = int.MaxValue)
        {
            bool jumpOut = false;
            int endX = sample.Width - model.Width, endY = sample.Height - model.Height;
            endX = endX - startFindX <= deltaX ? endX : startFindX + deltaX;
            endY = endY - startFindY <= deltaY ? endY : startFindY + deltaY;
            int mWidth = model.Width;
            int mHeight = model.Height;
            int sWidth = sample.Width;
            int sHeight = sample.Height;
            BitmapData modelBMD = model.LockBits(new Rectangle(0, 0, model.Width, model.Height), ImageLockMode.ReadWrite, model.PixelFormat);
            BitmapData sampleBMD = sample.LockBits(new Rectangle(0, 0, sample.Width, sample.Height), ImageLockMode.ReadWrite, sample.PixelFormat);
            byte* sampleScan0 = (byte*)sampleBMD.Scan0;
            byte* modelScan0 = (byte*)modelBMD.Scan0;
            byte c1r, c1g, c1b, c2a, c2r, c2g, c2b;

            for (int startX = endX; startX > startFindX; startX--)
            {
                for (int startY = startFindY; startY < endY; startY++)
                {
                    for (int y = 0; y < mHeight; y++)
                    {
                        jumpOut = false;
                        if (y + startY >= sHeight) break;
                        for (int x = 0; x < mWidth; x++)
                        {
                            c2a = modelScan0[y * modelBMD.Stride + x * 4 + 3];
                            if (c2a != 0)
                            {
                                c1b = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4];
                                c1g = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 1];
                                c1r = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 2];
                                // c1a = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 3];
                                c2b = modelScan0[y * modelBMD.Stride + x * 4];
                                c2g = modelScan0[y * modelBMD.Stride + x * 4 + 1];
                                c2r = modelScan0[y * modelBMD.Stride + x * 4 + 2];
                                if (Math.Abs(c1r - c2r) + Math.Abs(c1g - c2g) + Math.Abs(c1b - c2b) > threshold) { jumpOut = true; break; }
                            }
                        }
                        if (jumpOut) break;
                    }
                    if (jumpOut) continue;
                    sample.UnlockBits(sampleBMD);
                    model.UnlockBits(modelBMD);
                    return new Point(startX, startY);
                }
            }
            sample.UnlockBits(sampleBMD);
            model.UnlockBits(modelBMD);
            return new Point(-1, -1);
        }

        unsafe public static Point FindImageFromBottomRight(Bitmap sample, Bitmap model, int threshold = 30, int startFindX = 0, int startFindY = 0, int deltaX = int.MaxValue, int deltaY = int.MaxValue)
        {
            bool jumpOut = false;
            int endX = sample.Width - model.Width, endY = sample.Height - model.Height;
            endX = endX - startFindX <= deltaX ? endX : startFindX + deltaX;
            endY = endY - startFindY <= deltaY ? endY : startFindY + deltaY;
            int mWidth = model.Width;
            int mHeight = model.Height;
            int sWidth = sample.Width;
            int sHeight = sample.Height;
            BitmapData modelBMD = model.LockBits(new Rectangle(0, 0, model.Width, model.Height), ImageLockMode.ReadWrite, model.PixelFormat);
            BitmapData sampleBMD = sample.LockBits(new Rectangle(0, 0, sample.Width, sample.Height), ImageLockMode.ReadWrite, sample.PixelFormat);
            byte* sampleScan0 = (byte*)sampleBMD.Scan0;
            byte* modelScan0 = (byte*)modelBMD.Scan0;
            byte c1r, c1g, c1b, c2a, c2r, c2g, c2b;

            for (int startX = endX; startX > startFindX; startX--)
            {
                for (int startY = endY; startY > startFindY; startY--)
                {
                    for (int y = 0; y < mHeight; y++)
                    {
                        jumpOut = false;
                        if (y + startY >= sHeight) break;
                        for (int x = 0; x < mWidth; x++)
                        {
                            c2a = modelScan0[y * modelBMD.Stride + x * 4 + 3];
                            if (c2a != 0)
                            {
                                c1b = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4];
                                c1g = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 1];
                                c1r = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 2];
                                // c1a = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 3];
                                c2b = modelScan0[y * modelBMD.Stride + x * 4];
                                c2g = modelScan0[y * modelBMD.Stride + x * 4 + 1];
                                c2r = modelScan0[y * modelBMD.Stride + x * 4 + 2];
                                if (Math.Abs(c1r - c2r) + Math.Abs(c1g - c2g) + Math.Abs(c1b - c2b) > threshold) { jumpOut = true; break; }
                            }
                        }
                        if (jumpOut) break;
                    }
                    if (jumpOut) continue;
                    sample.UnlockBits(sampleBMD);
                    model.UnlockBits(modelBMD);
                    return new Point(startX, startY);
                }
            }
            sample.UnlockBits(sampleBMD);
            model.UnlockBits(modelBMD);
            return new Point(-1, -1);
        }

        unsafe public static bool CompareImageFaster(Bitmap sample, Bitmap model, int startX, int startY, int threshold = 20)
        {
            
            int mWidth = model.Width;
            int mHeight = model.Height;
            int sWidth = sample.Width;
            int sHeight = sample.Height;
            
            BitmapData modelBMD = model.LockBits(new Rectangle(0, 0, model.Width, model.Height), ImageLockMode.ReadWrite, model.PixelFormat);
            BitmapData sampleBMD = sample.LockBits(new Rectangle(0, 0, sample.Width, sample.Height), ImageLockMode.ReadWrite, sample.PixelFormat);
            byte* sampleScan0 = (byte*)sampleBMD.Scan0;
            byte* modelScan0 = (byte*)modelBMD.Scan0;
            byte c1r, c1g, c1b, c2a, c2r, c2g, c2b;
            for (int y = 0; y < mHeight; y++)
            {
                if (y + startY >= sHeight) break;
                for (int x = 0; x < mWidth; x++)
                {
                    c2a = modelScan0[y * modelBMD.Stride + x * 4 + 3];
                    if (c2a != 0)
                    {
                        c1b = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4];
                        c1g = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 1];
                        c1r = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 2];
                        // c1a = sampleScan0[(y + startY) * sampleBMD.Stride + (x + startX) * 4 + 3];
                        c2b = modelScan0[y * modelBMD.Stride + x * 4];
                        c2g = modelScan0[y * modelBMD.Stride + x * 4 + 1];
                        c2r = modelScan0[y * modelBMD.Stride + x * 4 + 2];
                        if (Math.Abs(c1r - c2r) + Math.Abs(c1g - c2g) + Math.Abs(c1b - c2b) > threshold) { sample.UnlockBits(sampleBMD); model.UnlockBits(modelBMD); return false; }
                    }
                }
            }
            sample.UnlockBits(sampleBMD);
            model.UnlockBits(modelBMD);
            return true;
        }

        public static bool CompareImage(Bitmap sample, Bitmap model, int startX, int startY, int threshold = 20)
        {
            int mWidth = model.Width;
            int mHeight = model.Height;
            int sWidth = sample.Width;
            int sHeight = sample.Height;
            Color c1, c2;
            for (int y = 0; y < mHeight; y++)
            {
                if (y + startY >= sHeight) break;
                for (int x = 0; x < mWidth; x++)
                {
                    if (x + startX >= sWidth) break;
                    c2 = model.GetPixel(x, y);
                    if (c2.A != 0)
                    {
                        c1 = sample.GetPixel(startX + x, startY + y);
                        if (GetColorDiff(c1, c2) > threshold) return false;
                    }
                }
            }
            return true;
        }

        unsafe public static byte[,,] Image2ByteArray(Bitmap img)
        {
            byte[,,] imgData = new byte[img.Width, img.Height, 4];
            int mWidth = img.Width;
            int mHeight = img.Height;
            BitmapData imgBMD = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadWrite, img.PixelFormat);
            byte* imgScan0 = (byte*)imgBMD.Scan0;

            if (img.PixelFormat == PixelFormat.Format32bppArgb)
            {
                for (int y = 0; y < mHeight; y++)
                {
                    for (int x = 0; x < mWidth; x++)
                    {
                        imgData[x, y, 0] = imgScan0[(y) * imgBMD.Stride + (x) * 4 + 3];
                        imgData[x, y, 1] = imgScan0[(y) * imgBMD.Stride + (x) * 4 + 2];
                        imgData[x, y, 2] = imgScan0[(y) * imgBMD.Stride + (x) * 4 + 1];
                        imgData[x, y, 3] = imgScan0[(y) * imgBMD.Stride + (x) * 4];
                    }
                }
            }
            else if (img.PixelFormat == PixelFormat.Format24bppRgb)
            {
                for (int y = 0; y < mHeight; y++)
                {
                    for (int x = 0; x < mWidth; x++)
                    {
                        imgData[x, y, 0] = 255;
                        imgData[x, y, 1] = imgScan0[(y) * imgBMD.Stride + (x) * 3 + 2];
                        imgData[x, y, 2] = imgScan0[(y) * imgBMD.Stride + (x) * 3 + 1];
                        imgData[x, y, 3] = imgScan0[(y) * imgBMD.Stride + (x) * 3];
                    }
                }
            }
            else
            {
                for (int y = 0; y < mHeight; y++)
                {
                    for (int x = 0; x < mWidth; x++)
                    {
                        imgData[x, y, 0] = 0;
                        imgData[x, y, 1] = 0;
                        imgData[x, y, 2] = 0;
                        imgData[x, y, 3] = 0;
                    }
                }
            }
            img.UnlockBits(imgBMD);
            return imgData;
        }

        public static int GetRGBAvg(Color c)
        {
            return (c.R + c.G + c.B) / 3;
        }

        public static int GetColorDiff(Color c1, Color c2)
        {
            return Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
        }

        public static Bitmap CutImage(Bitmap bm, Rectangle rect)
        {
            Bitmap temp = new Bitmap(rect.Width, rect.Height);
            Graphics gdi = Graphics.FromImage(temp);
            gdi.DrawImage(bm, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            return temp;
        }

        public static Bitmap GetWhiteTextFromImage(Bitmap bm, int threshold = 120)
        {
            Bitmap rbm = new Bitmap(bm.Width, bm.Height);
            for (int y = 0; y < bm.Height; y++)
            {
                for (int x = 0; x < bm.Width; x++)
                {
                    Color c = bm.GetPixel(x, y);
                    if (c.R * 0.299 + c.G * 0.587 + c.B * 0.114 >= threshold)
                    {
                        rbm.SetPixel(x, y, Color.Black);
                    }
                }
            }
            return rbm;
        }

        /// <summary>
        /// 自动抠背景：若四个角颜色接近（实心背景截图），把与边缘相连、且与背景色接近的像素
        /// 设为全透明，返回新的 32bppArgb 位图（FindImageFromTop 会跳过 alpha=0 的像素）。
        /// 四角颜色不一致，或抠完剩余主体过少时，原样返回一份 32bppArgb 拷贝。
        /// 调用方负责释放传入的 src。
        /// </summary>
        public static Bitmap MakeTransparentFromCorners(Bitmap src, int tolerance = 30)
        {
            int w = src.Width, h = src.Height;
            Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            if (w < 3 || h < 3) return dst;

            BitmapData bd = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = bd.Stride;
                byte[] px = new byte[stride * h];
                Marshal.Copy(bd.Scan0, px, 0, px.Length);

                int bgB = px[0], bgG = px[1], bgR = px[2];
                Func<int, int, bool> nearBg = (x, y) =>
                {
                    int o = y * stride + x * 4;
                    return Math.Abs(px[o] - bgB) + Math.Abs(px[o + 1] - bgG) + Math.Abs(px[o + 2] - bgR) <= tolerance;
                };
                // 四角都接近左上角颜色才认为是实心背景
                if (!nearBg(w - 1, 0) || !nearBg(0, h - 1) || !nearBg(w - 1, h - 1)) return dst;

                bool[] visited = new bool[w * h];
                var queue = new Queue<int>();
                Action<int, int> push = (x, y) =>
                {
                    int idx = y * w + x;
                    if (visited[idx]) return;
                    visited[idx] = true;
                    if (nearBg(x, y)) queue.Enqueue(idx);
                };
                for (int x = 0; x < w; x++) { push(x, 0); push(x, h - 1); }
                for (int y = 0; y < h; y++) { push(0, y); push(w - 1, y); }

                var cleared = new List<int>();
                while (queue.Count > 0)
                {
                    int idx = queue.Dequeue();
                    cleared.Add(idx);
                    int cx = idx % w, cy = idx / w;
                    if (cx > 0) push(cx - 1, cy);
                    if (cx < w - 1) push(cx + 1, cy);
                    if (cy > 0) push(cx, cy - 1);
                    if (cy < h - 1) push(cx, cy + 1);
                }

                // 剩余主体不足 10% 说明模板几乎全是背景，抠掉反而会到处误匹配，放弃
                if (w * h - cleared.Count < w * h / 10) return dst;
                foreach (int idx in cleared)
                {
                    px[(idx / w) * stride + (idx % w) * 4 + 3] = 0;
                }
                Marshal.Copy(px, 0, bd.Scan0, px.Length);
                return dst;
            }
            finally
            {
                dst.UnlockBits(bd);
            }
        }
    }
}
