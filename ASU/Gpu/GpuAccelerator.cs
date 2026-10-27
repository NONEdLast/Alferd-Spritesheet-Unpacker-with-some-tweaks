using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace ASU.Gpu
{
    /// <summary>
    /// Lazily-initialised singleton owning the ILGPU CUDA context. If no CUDA
    /// device is available (or initialisation fails) <see cref="IsAvailable"/>
    /// is false and all callers fall back to the CPU path.
    /// </summary>
    public class GpuAccelerator : IDisposable
    {
        private static readonly object InitLock = new object();
        private static GpuAccelerator instance;

        private Context context;
        private CudaAccelerator accelerator;
        private readonly object acceleratorLock = new object();

        public bool IsAvailable { get; private set; }
        public string DeviceName { get; private set; }

        public static GpuAccelerator Instance
        {
            get
            {
                lock (InitLock)
                {
                    if (instance == null)
                    {
                        instance = new GpuAccelerator();
                    }
                    return instance;
                }
            }
        }

        private GpuAccelerator()
        {
            try
            {
                this.context = Context.Create(builder => builder.Cuda());
                CudaDevice device = this.context.Devices
                    .OfType<CudaDevice>()
                    .OrderByDescending(d => d.MemorySize)
                    .FirstOrDefault();
                if (device != null)
                {
                    this.accelerator = device.CreateAccelerator(this.context) as CudaAccelerator;
                    if (this.accelerator != null)
                    {
                        this.DeviceName = device.Name;
                        this.IsAvailable = true;
                        Console.WriteLine("[ASU] GPU accelerator enabled: " + device.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ASU] GPU unavailable, falling back to CPU: " + ex.Message);
                this.IsAvailable = false;
            }
        }

        /// <summary>
        /// Computes the foreground mask on the GPU. Returns the mask array, or
        /// null when the GPU path failed (caller should fall back to CPU).
        /// </summary>
        public byte[] ComputeMask(int[] argb, int bgArgb)
        {
            if (!this.IsAvailable)
            {
                return null;
            }

            lock (this.acceleratorLock)
            {
                try
                {
                    int length = argb.Length;
                    byte[] mask;

                    var kernel = this.accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<byte>, int>(SpriteKernels.MaskKernel);
                    using (var argbBuffer = this.accelerator.Allocate1D(argb))
                    using (var maskBuffer = this.accelerator.Allocate1D<byte>(length))
                    {
                        kernel(new Index1D(length), argbBuffer.View, maskBuffer.View, bgArgb);
                        this.accelerator.Synchronize();
                        mask = maskBuffer.GetAsArray1D();
                    }
                    return mask;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ASU] GPU kernel failed, falling back to CPU: " + ex.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// Computes a 16-bit (R5|G6|B5) colour-bucket histogram on the GPU.
        /// Returns 65536 bucket counts, or null when the GPU path failed.
        /// </summary>
        public int[] ComputeBucketHistogram(int[] argb)
        {
            if (!this.IsAvailable)
            {
                return null;
            }

            lock (this.acceleratorLock)
            {
                try
                {
                    int length = argb.Length;
                    int[] buckets = new int[65536];

                    var kernel = this.accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(SpriteKernels.BucketHistogramKernel);
                    using (var argbBuffer = this.accelerator.Allocate1D(argb))
                    using (var bucketBuffer = this.accelerator.Allocate1D(buckets))
                    {
                        kernel(new Index1D(length), argbBuffer.View, bucketBuffer.View);
                        this.accelerator.Synchronize();
                        buckets = bucketBuffer.GetAsArray1D();
                    }
                    return buckets;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ASU] GPU histogram failed, falling back to CPU: " + ex.Message);
                    return null;
                }
            }
        }

        public void Dispose()
        {
            lock (this.acceleratorLock)
            {
                if (this.accelerator != null)
                {
                    this.accelerator.Dispose();
                    this.accelerator = null;
                }
                if (this.context != null)
                {
                    this.context.Dispose();
                    this.context = null;
                }
                this.IsAvailable = false;
            }
        }
    }
}
