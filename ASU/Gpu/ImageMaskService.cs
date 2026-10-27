using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ASU.Gpu
{
    /// <summary>
    /// Controls how the background colour is determined when the user has not
    /// manually fixed one.
    /// </summary>
    public enum BackgroundDetectionMethod
    {
        /// <summary>The colour that appears most often in the whole image.</summary>
        MostVotingColour = 0,
        /// <summary>The colour of the very top-left pixel of the image.</summary>
        TopLeftColour = 1,
        /// <summary>
        /// The background is the separator-coloured area that is reachable from
        /// the image edge (the outer margin and the grid lines of a sheet). Every
        /// pixel the separator encloses - the cell fill and the sprite drawn on
        /// it, including the sprite's own separator-coloured pixels - stays
        /// foreground. Suits sheets drawn as cells filled with a flat colour
        /// (e.g. magenta) and separated by a thin black grid.
        /// </summary>
        BorderConnectedBackground = 2,
    }

    /// <summary>
    /// Facade that extracts 32bpp ARGB pixels from a bitmap once (LockBits,
    /// ~100x faster than GetPixel), determines the background colour and builds
    /// the foreground mask. The mask computation runs on the GPU when a CUDA
    /// device is available (App.config "UseGpu"), otherwise on the CPU.
    /// </summary>
    public static class ImageMaskService
    {
        // Above this pixel count the GPU upload cost outweighs the benefit and
        // we stay on the CPU to avoid huge device allocations.
        private const long MAX_GPU_PIXELS = 64_000_000L;

        public static bool UseGpu
        {
            get
            {
                if (Environment.GetEnvironmentVariable("ASU_FORCE_CPU") == "1")
                {
                    return false;
                }
                try
                {
                    string setting = ConfigurationManager.AppSettings["UseGpu"];
                    return string.IsNullOrEmpty(setting) || Convert.ToBoolean(setting);
                }
                catch
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Analyses the image. When <paramref name="knownBackground"/> is null
        /// the background colour is detected by <paramref name="method"/>:
        /// <see cref="BackgroundDetectionMethod.MostVotingColour"/> uses the exact
        /// legacy ImageUnpacker.SetBackgroundColour histogram (x-outer/y-inner,
        /// last-maximum tie-break), while
        /// <see cref="BackgroundDetectionMethod.TopLeftColour"/> uses the colour
        /// of the top-left pixel.
        /// <see cref="BackgroundDetectionMethod.BorderConnectedBackground"/>
        /// treats the separator colour that is reachable from the image edge as
        /// background (see <see cref="ComputeBorderConnectedMask"/>); the reported
        /// <see cref="ImageAnalysisResult.Background"/> is then the cell fill
        /// colour rather than the separator, and
        /// <see cref="ImageAnalysisResult.SeparatorColour"/> holds the separator.
        /// </summary>
        public static ImageAnalysisResult Analyze(Bitmap image, Color? knownBackground, BackgroundDetectionMethod method, Action<int> progress)
        {
            int width = image.Width;
            int height = image.Height;
            int[] argb = ExtractArgb(image, progress);

            ImageAnalysisResult result = new ImageAnalysisResult();
            result.Width = width;
            result.Height = height;
            result.Argb = argb;

            if (method == BackgroundDetectionMethod.BorderConnectedBackground)
            {
                Color separator;
                if (knownBackground.HasValue)
                {
                    separator = knownBackground.Value;
                }
                else
                {
                    separator = DetectSeparatorColour(argb, width, height);
                }

                result.SeparatorColour = separator;
                result.Mask = ComputeBorderConnectedMask(argb, width, height, separator.ToArgb());

                int fillColoursCount;
                result.Background = DetectFillColour(argb, result.Mask, separator, out fillColoursCount);
                result.ColoursCount = fillColoursCount;
                return result;
            }

            Color background;
            int coloursCount = 0;

            if (knownBackground.HasValue)
            {
                background = knownBackground.Value;
            }
            else
            {
                background = DetectBackgroundColour(argb, width, height, method, out coloursCount);
            }

            result.Background = background;
            result.SeparatorColour = background;
            result.ColoursCount = coloursCount;

            int bgArgb = background.ToArgb();
            byte[] mask = null;

            if (UseGpu && (long)width * height <= MAX_GPU_PIXELS)
            {
                mask = GpuAccelerator.Instance.ComputeMask(argb, bgArgb);
                result.UsedGpu = mask != null;
            }

            if (mask == null)
            {
                mask = new byte[argb.Length];
                for (int i = 0; i < argb.Length; i++)
                {
                    mask[i] = (byte)(argb[i] == bgArgb ? 0 : 1);
                }
            }

            result.Mask = mask;
            return result;
        }

        /// <summary>
        /// Copies all pixels into a row-major int[] as 32bpp ARGB. LockBits
        /// with Format32bppArgb converts indexed / 24bpp / grayscale bitmaps
        /// and returns bit-identical values to GetPixel.
        /// </summary>
        private static int[] ExtractArgb(Bitmap image, Action<int> progress)
        {
            int width = image.Width;
            int height = image.Height;
            int[] argb = new int[width * height];

            Rectangle full = new Rectangle(0, 0, width, height);
            BitmapData data = image.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                bool negativeStride = stride < 0;
                int absStride = Math.Abs(stride);
                IntPtr basePtr = negativeStride ? data.Scan0 + (height - 1) * absStride : data.Scan0;

                int[] row = new int[width];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(basePtr + y * absStride, row, 0, width);
                    Array.Copy(row, 0, argb, y * width, width);

                    if (progress != null && y % 32 == 0)
                    {
                        progress(Convert.ToInt32(10.0 * y / height));
                    }
                }
            }
            finally
            {
                image.UnlockBits(data);
            }

            return argb;
        }

        /// <summary>
        /// Resolves the background colour according to <paramref name="method"/>.
        /// The most-voting colour reproduces the legacy histogram exactly:
        /// iterate x outer / y inner, add to a Dictionary (insertion order), then
        /// pick the colour with the highest count using >= (ties resolved by last
        /// insertion). The top-left method simply takes the first pixel.
        /// </summary>
        private static Color DetectBackgroundColour(int[] argb, int width, int height, BackgroundDetectionMethod method, out int coloursCount)
        {
            if (method == BackgroundDetectionMethod.TopLeftColour)
            {
                coloursCount = 0;
                return Color.FromArgb(argb[0]);
            }

            Dictionary<int, int> colourCountsByArgb = new Dictionary<int, int>();

            for (int x = 0; x <= width - 1; x++)
            {
                for (int y = 0; y <= height - 1; y++)
                {
                    int presentArgb = argb[y * width + x];

                    if (!colourCountsByArgb.ContainsKey(presentArgb))
                    {
                        colourCountsByArgb.Add(presentArgb, 1);
                    }
                    else
                    {
                        colourCountsByArgb[presentArgb] += 1;
                    }
                }
            }

            int maxCount = 0;
            int backgroundArgb = 0;

            foreach (int colourArgb in colourCountsByArgb.Keys)
            {
                if (colourCountsByArgb[colourArgb] >= maxCount)
                {
                    maxCount = colourCountsByArgb[colourArgb];
                    backgroundArgb = colourArgb;
                }
            }

            coloursCount = colourCountsByArgb.Count - 1;
            return Color.FromArgb(backgroundArgb);
        }

        /// <summary>
        /// Picks the separator colour as the most frequent colour along the four
        /// image edges: a sheet's outer margin and its grid lines are the only
        /// things that normally touch the edges. Ties are resolved by the last
        /// insertion, as in the legacy histogram.
        /// </summary>
        private static Color DetectSeparatorColour(int[] argb, int width, int height)
        {
            Dictionary<int, int> colourCountsByArgb = new Dictionary<int, int>();

            for (int x = 0; x < width; x++)
            {
                AddColourCount(colourCountsByArgb, argb[x]);
                AddColourCount(colourCountsByArgb, argb[(height - 1) * width + x]);
            }
            for (int y = 0; y < height; y++)
            {
                AddColourCount(colourCountsByArgb, argb[y * width]);
                AddColourCount(colourCountsByArgb, argb[y * width + width - 1]);
            }

            int maxCount = 0;
            int separatorArgb = argb[0];

            foreach (int colourArgb in colourCountsByArgb.Keys)
            {
                if (colourCountsByArgb[colourArgb] >= maxCount)
                {
                    maxCount = colourCountsByArgb[colourArgb];
                    separatorArgb = colourArgb;
                }
            }

            return Color.FromArgb(separatorArgb);
        }

        /// <summary>
        /// Builds the foreground mask of
        /// <see cref="BackgroundDetectionMethod.BorderConnectedBackground"/>: a
        /// pixel is background when it has the separator colour and is
        /// 4-connected to the image edge. Separator-coloured pixels that the cell
        /// fill seals off are foreground, which is what keeps a sprite's own dark
        /// pixels (e.g. black outlines) intact instead of punching holes in it.
        /// </summary>
        private static byte[] ComputeBorderConnectedMask(int[] argb, int width, int height, int separatorArgb)
        {
            // 0 = not separator coloured, 1 = separator coloured and still to be
            // visited, 2 = separator coloured and reachable from the edge.
            byte[] mask = new byte[argb.Length];
            for (int i = 0; i < argb.Length; i++)
            {
                mask[i] = (byte)(argb[i] == separatorArgb ? 1 : 0);
            }

            List<int> seeds = new List<int>();

            for (int x = 0; x < width; x++)
            {
                QueueSeed(mask, seeds, x, 0, width);
                QueueSeed(mask, seeds, x, height - 1, width);
            }
            for (int y = 0; y < height; y++)
            {
                QueueSeed(mask, seeds, 0, y, width);
                QueueSeed(mask, seeds, width - 1, y, width);
            }

            // Scanline flood fill: only span starts are queued, so the seed list
            // stays small even on a multi-megapixel sheet.
            while (seeds.Count > 0)
            {
                int last = seeds.Count - 1;
                int seed = seeds[last];
                seeds.RemoveAt(last);

                if (mask[seed] != 1)
                {
                    continue;
                }

                int rowStart = seed - (seed % width);
                int left = seed - rowStart;
                int right = left;

                while (left > 0 && mask[rowStart + left - 1] == 1)
                {
                    left -= 1;
                }
                while (right < width - 1 && mask[rowStart + right + 1] == 1)
                {
                    right += 1;
                }

                for (int x = left; x <= right; x++)
                {
                    mask[rowStart + x] = 2;
                }

                int y = rowStart / width;
                QueueSpanSeeds(mask, seeds, y - 1, left, right, width, height);
                QueueSpanSeeds(mask, seeds, y + 1, left, right, width, height);
            }

            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = (byte)(mask[i] == 2 ? 0 : 1);
            }

            return mask;
        }

        private static void QueueSeed(byte[] mask, List<int> seeds, int x, int y, int width)
        {
            int index = y * width + x;
            if (mask[index] == 1)
            {
                seeds.Add(index);
            }
        }

        private static void QueueSpanSeeds(byte[] mask, List<int> seeds, int y, int left, int right, int width, int height)
        {
            if (y < 0 || y >= height)
            {
                return;
            }

            int rowStart = y * width;
            bool insideSpan = false;

            for (int x = left; x <= right; x++)
            {
                if (mask[rowStart + x] == 1)
                {
                    if (!insideSpan)
                    {
                        seeds.Add(rowStart + x);
                        insideSpan = true;
                    }
                }
                else
                {
                    insideSpan = false;
                }
            }
        }

        /// <summary>
        /// Reports the colour that fills the cells (the sheet's transparent
        /// colour, e.g. magenta) as the image background, so that "make background
        /// transparent" removes the fill around a sprite while leaving the
        /// sprite's own separator-coloured pixels alone.
        /// </summary>
        private static Color DetectFillColour(int[] argb, byte[] mask, Color fallback, out int coloursCount)
        {
            Dictionary<int, int> colourCountsByArgb = new Dictionary<int, int>();

            for (int i = 0; i < argb.Length; i++)
            {
                if (mask[i] == 1)
                {
                    AddColourCount(colourCountsByArgb, argb[i]);
                }
            }

            if (colourCountsByArgb.Count == 0)
            {
                coloursCount = 0;
                return fallback;
            }

            int maxCount = 0;
            int fillArgb = fallback.ToArgb();

            foreach (int colourArgb in colourCountsByArgb.Keys)
            {
                if (colourCountsByArgb[colourArgb] >= maxCount)
                {
                    maxCount = colourCountsByArgb[colourArgb];
                    fillArgb = colourArgb;
                }
            }

            coloursCount = colourCountsByArgb.Count;
            return Color.FromArgb(fillArgb);
        }

        private static void AddColourCount(Dictionary<int, int> colourCountsByArgb, int argb)
        {
            if (!colourCountsByArgb.ContainsKey(argb))
            {
                colourCountsByArgb.Add(argb, 1);
            }
            else
            {
                colourCountsByArgb[argb] += 1;
            }
        }
    }
}
