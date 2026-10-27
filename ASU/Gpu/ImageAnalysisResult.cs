using System;
using System.Drawing;

namespace ASU.Gpu
{
    /// <summary>
    /// Immutable per-image analysis result: raw 32bpp ARGB pixels, a foreground
    /// mask (1 = pixel belongs to a sprite) and the detected background colour.
    /// Shared read-only by all region threads.
    /// </summary>
    public class ImageAnalysisResult
    {
        public int Width;
        public int Height;
        public int[] Argb;
        public byte[] Mask;

        /// <summary>
        /// For <see cref="BackgroundDetectionMethod.BorderConnectedBackground"/>
        /// this is the cell fill colour (the colour "Make Background Transparent"
        /// removes); for the other methods it is the background colour and matches
        /// <see cref="SeparatorColour"/>.
        /// </summary>
        public Color Background;

        /// <summary>
        /// The colour the mask was built from. For
        /// <see cref="BackgroundDetectionMethod.BorderConnectedBackground"/> it is
        /// the separator grid colour that is flooded from the image edge; for the
        /// other methods it matches <see cref="Background"/>.
        /// </summary>
        public Color SeparatorColour;

        public int ColoursCount;
        public bool UsedGpu;

        public bool IsForeground(int x, int y)
        {
            return Mask[y * Width + x] == 1;
        }
    }
}