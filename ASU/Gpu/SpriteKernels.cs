using ILGPU;

namespace ASU.Gpu
{
    /// <summary>
    /// ILGPU kernels. Must be static, side-effect free and must not capture
    /// any state (all values are passed as kernel parameters).
    /// </summary>
    public static class SpriteKernels
    {
        /// <summary>
        /// One thread per pixel: mask[i] = 1 when the pixel differs from the
        /// background colour, 0 otherwise.
        /// </summary>
        public static void MaskKernel(Index1D index, ArrayView<int> argb, ArrayView<byte> mask, int bgArgb)
        {
            if (index < argb.Length)
            {
                mask[index] = (byte)(argb[index] == bgArgb ? 0 : 1);
            }
        }

        /// <summary>
        /// One thread per pixel: atomic count into a 16-bit (R5|G6|B5) colour
        /// bucket histogram.
        /// </summary>
        public static void BucketHistogramKernel(Index1D index, ArrayView<int> argb, ArrayView<int> buckets)
        {
            if (index < argb.Length)
            {
                int c = argb[index];
                int key = (((c >> 16) & 0x1F) << 10) | (((c >> 8) & 0x3F) << 5) | (c & 0x1F);
                ILGPU.Atomic.Add(ref buckets[key], 1);
            }
        }
    }
}
