using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using ASU.Gpu;

namespace ASU.BO
{
    public class ImageUnpacker
    {
        public bool IsLarge { get; set; }

        private Bitmap original;
        private object originalLock = new object();
        private int pcComplete = 0;
        private Color? backgroundColour = null;
        private object boxesLock = new object();
        private List<Rectangle> boxes;
        private int areaUnpacked;
        private object areaUnpackedLock = new object();
        private int areaToUnpack;
        private int threadCounter = 0;
        private object threadCompleteCounterLock = new object();
        private int threadCompleteCounter = 0;
        private bool areAllThreadsCreated;
        private bool isUnpackingComplete = false;
        private Size originalSize;
        private System.Drawing.Imaging.ColorPalette pallette = null;
        private bool _isBackgroundColourSet = false;
        private bool _isUnpacking = false;
        private ImageAnalysisResult analysisResult;
        private const int INT_MAX_REGION_WIDTH = 400;

        public string FileName { get; set; }
        public int ColoursCount = 0;

        /// <summary>
        /// Determines how the background colour is detected when no explicit
        /// colour has been fixed. <see cref="BackgroundDetectionMethod.MostVotingColour"/>
        /// is the default (legacy) behaviour.
        /// </summary>
        public BackgroundDetectionMethod BackgroundMethod { get; set; } = BackgroundDetectionMethod.MostVotingColour;

        public event UnpackingCompleteEventHandler UnpackingComplete;        
        public event PcCompleteChangedEventHandler PcCompleteChanged;

        public delegate void PcCompleteChangedEventHandler(int pcComplete);
        public delegate void UnpackingCompleteEventHandler();

        public ImageUnpacker(Bitmap image, string fileName, bool removeTransparency)
        {
            if (image.Palette.Entries.Length > 0)
            {
                this.pallette = image.Palette;
            }
            this.original = new Bitmap((Bitmap)image.Clone());
            this.originalSize = image.Size;
            if (removeTransparency)
            {
                this.original = this.RemoveTransparencyFromImage(this.original);
            }
            this.boxes = new List<Rectangle>();
            this.FileName = fileName;
            this.IsLarge = (this.original.Width * this.original.Height) > (800 * 800);
        }

        private Bitmap RemoveTransparencyFromImage(Bitmap image)
        {
            Dictionary<int, Color> coloursByArgb = new Dictionary<int, Color>();
            Dictionary<uint, Color> transparentPixelsByCoord = new Dictionary<uint, Color>();
            Dictionary<Color, Color> opaquedByTransparent = new Dictionary<Color, Color>();
            Color pixel;
            bool containsTransparency = false;
            Color transparentPixel;
            Color opaquedPixel;

            for(int x = 0; x < image.Width; x++)
            {
                for(int y = 0; y < image.Height; y++)
                {
                    pixel = image.GetPixel(x, y);

                    if (!coloursByArgb.ContainsKey(pixel.ToArgb()))
                    {
                        coloursByArgb.Add(pixel.ToArgb(), pixel);     
                    }
                    if (pixel.A < 255)
                    {
                        containsTransparency = true;
                        transparentPixelsByCoord.Add(GetHashFromCoord(new Point(x, y)), pixel);
                    }
                }
            }

            if (containsTransparency)
            {
                foreach (uint coordHash in transparentPixelsByCoord.Keys)
                {
                    Point coord = GetCoordFromHash(coordHash);

                    transparentPixel = transparentPixelsByCoord[coordHash];
                    if (opaquedByTransparent.ContainsKey(transparentPixel))
                    {
                        opaquedPixel = opaquedByTransparent[transparentPixel];
                    }
                    else
                    {
                        opaquedPixel = Color.FromArgb(255, transparentPixel);

                        do
                        {
                            if (transparentPixel.R > (255 / 2))
                            {
                                opaquedPixel = Color.FromArgb(opaquedPixel.R - 1, opaquedPixel.G, opaquedPixel.B);
                            }
                            else
                            {
                                opaquedPixel = Color.FromArgb(opaquedPixel.R + 1, opaquedPixel.G, opaquedPixel.B);
                            }
                        } while (coloursByArgb.ContainsKey(opaquedPixel.ToArgb()));

                        coloursByArgb.Remove(transparentPixel.ToArgb());
                        coloursByArgb.Add(opaquedPixel.ToArgb(), opaquedPixel);
                        opaquedByTransparent.Add(transparentPixel, opaquedPixel);
                    }
                    image.SetPixel(coord.X, coord.Y, opaquedPixel);
                }
            }

            return image;
        }

        public System.Drawing.Imaging.ColorPalette GetPallette()
        {
            return this.pallette;
        }

        public Size GetSize()
        {
            return this.originalSize;
        }

        public bool IsUnpacking()
        {
            return this._isUnpacking;
        }

        public bool IsBackgroundColourSet()
        {
            return this._isBackgroundColourSet;
        }

        public Color CreateOuterBackgroundColour()
        {
            Color backgroundColour;

            backgroundColour = this.GetBackgroundColour();

            if (backgroundColour.GetBrightness() > .2f)
            {
                return Color.FromArgb(Math.Max(0, backgroundColour.R - 25), Math.Max(0, backgroundColour.G - 25), Math.Max(0, backgroundColour.B - 25));
            }
            else
            {
                return Color.FromArgb(Math.Min(255, backgroundColour.R + 25), Math.Min(255, backgroundColour.G + 25), Math.Min(255, backgroundColour.B + 25));
            }
        }

        public Color GetBackgroundColour()
        {
            if (this.backgroundColour.HasValue)
            {
                return this.backgroundColour.Value;
            }
            else
            {
                return Color.Black;
            }
        }

        public List<Rectangle> GetBoxes()
        {
            return new List<Rectangle>(this.boxes);
        }

        /// <summary>
        /// Overrides the automatically-detected background colour and forces the
        /// next StartUnpacking to re-analyse using this colour. Used by the
        /// "Set Background" picker when the most-common colour is not the real
        /// separator (e.g. magenta sprite backgrounds separated by a black grid).
        /// </summary>
        public void SetBackgroundColour(Color colour)
        {
            this.backgroundColour = colour;
            this._isBackgroundColourSet = true;
            this.analysisResult = null;
        }

        /// <summary>
        /// Switches the background-detection method and drops the cached analysis
        /// (and any manually fixed colour) so the next StartUnpacking re-analyses
        /// the sheet with the new method. Without this the previously computed
        /// mask would be reused and the algorithm change would have no effect.
        /// </summary>
        public void SetBackgroundMethod(BackgroundDetectionMethod method)
        {
            if (this.BackgroundMethod == method)
            {
                return;
            }
            this.BackgroundMethod = method;
            this.backgroundColour = null;
            this._isBackgroundColourSet = false;
            this.analysisResult = null;
        }

        /// <summary>
        /// Refines a box that was wrongly merged from several sprites: re-runs
        /// sprite-boundary detection restricted to <paramref name="region"/> using
        /// the global foreground mask. Returns the constituent sub-boxes, or null
        /// when the image has not been analysed yet.
        /// </summary>
        public List<Rectangle> GetRefinedBoxes(Rectangle region)
        {
            if (this.analysisResult == null)
            {
                return null;
            }
            return RegionUnpacker.ExtractSubBoxes(this.analysisResult, region);
        }

        public bool IsUnpacked()
        {
            return this.isUnpackingComplete;
        }

        public int GetPcComplete()
        {
            return this.pcComplete;
        }

        public Bitmap GetOriginalClone()
        {
            Bitmap clone = default(Bitmap);

            lock ((this.originalLock))
            {
                clone = new Bitmap((Bitmap)this.original.Clone());
            }

            return clone;
        }

        private void SetPcComplete(int pcComplete)
        {
            if(pcComplete > 100)
            {   // TODO: Fix this bug.
                pcComplete = 100;
            }
            if (pcComplete > this.pcComplete)
            {
                this.pcComplete = pcComplete;
                if (PcCompleteChanged != null)
                {
                    PcCompleteChanged(this.pcComplete);
                }
            }
        }

        public void StartUnpacking()
        {
            System.Threading.Thread newThread = new System.Threading.Thread(this.Unpack);
            this.isUnpackingComplete = false;
            this.pcComplete = 0;
            this.boxes.Clear();
            this._isUnpacking = true;
            this.threadCounter = 0;
            this.threadCompleteCounter = 0;
            newThread.IsBackground = true;
            newThread.Start();
        }

        public static List<Rectangle> OrderBoxes(List<Rectangle> boxes, Enums.SelectAllOrder selectAllOrder, Size spriteSheetSize)
        {
            SortedDictionary<int, List<Rectangle>> orderedBoxes = new SortedDictionary<int, List<Rectangle>>();
            int location = 0;
            List<Rectangle> returnedOrder = new List<Rectangle>();


            foreach (Rectangle box in boxes)
            {
                switch (selectAllOrder)
                {
                    case Enums.SelectAllOrder.TopLeft:
                        location = box.X + (box.Y * spriteSheetSize.Width);
                        break;
                    case Enums.SelectAllOrder.BottomLeft:
                        location = box.X + ((box.Y + box.Height) * spriteSheetSize.Width);
                        break;
                    case Enums.SelectAllOrder.Centre:
                        location = Convert.ToInt32((box.X + (box.Width / 2)) + ((box.Y + (box.Height / 2)) * spriteSheetSize.Width));
                        break;
                }

                if (!orderedBoxes.ContainsKey(location))
                {
                    orderedBoxes.Add(location, new List<Rectangle>());
                }
                orderedBoxes[location].Add(box);
            }

            foreach (int locationKey in orderedBoxes.Keys)
            {
                foreach (Rectangle box in orderedBoxes[locationKey])
                {
                    returnedOrder.Add(box);
                }
            }

            return returnedOrder;
        }

        private void Unpack(object state)
        {
            try
            {
                int subRegionCount;
                int xSize = 0;
                int ySize = 0;
                decimal totalSize;
                Rectangle region = default(Rectangle);
                List<System.Threading.Thread> regionThreads = new List<System.Threading.Thread>();
                List<Rectangle> regions = new List<Rectangle>();
                System.Threading.Thread regionThread = null;

                this.areAllThreadsCreated = false;
                if (!this.backgroundColour.HasValue || this.analysisResult == null)
                {
                    using (Bitmap clone = this.GetOriginalClone())
                    {
                        this.analysisResult = ImageMaskService.Analyze(clone, this.backgroundColour, this.BackgroundMethod, this.SetPcComplete);
                    }
                    this.backgroundColour = this.analysisResult.Background;
                    this._isBackgroundColourSet = true;
                    if (this.analysisResult.ColoursCount > 0)
                    {
                        this.ColoursCount = this.analysisResult.ColoursCount;
                    }
                }
                this.SetPcComplete(10);

                if (this.BackgroundMethod == BackgroundDetectionMethod.BorderConnectedBackground)
                {
                    this.UnpackConnectedComponents();
                    return;
                }

                if(Environment.ProcessorCount > 1)
                {
                    subRegionCount = 4;
                }
                else
                {
                    subRegionCount = 2;
                }

                ySize = Convert.ToInt32(Math.Ceiling((double)this.originalSize.Height / (double)subRegionCount));
                xSize = Convert.ToInt32(Math.Ceiling((double)this.originalSize.Width / (double)subRegionCount));

                this.areaToUnpack = this.originalSize.Width * this.originalSize.Height;

                totalSize = this.originalSize.Height * this.originalSize.Width;
                
                for (int y = 0; y < this.originalSize.Height; y += ySize)
                {
                    for (int x = 0; x < this.originalSize.Width; x += xSize)
                    {
                        region = new Rectangle(x, y, Math.Min(xSize+1, (this.originalSize.Width - x) - 1), Math.Min(ySize+1, (this.originalSize.Height - y) - 1));
                        regionThread = new System.Threading.Thread(this.HandleDividedAreaThread);
                        regionThread.Name = "Region thread " + (y * (xSize * 4)) + x;
                        this.threadCounter += 1;
                        regionThread.IsBackground = true;
                        
                        regionThreads.Add(regionThread);
                        regions.Add(region);                        
                    }
                }

                for (int k = 0; k < regionThreads.Count; k++)
                {
                    regionThreads[k].Start(regions[k]);
                    this.SetPcComplete(10 + Convert.ToInt32((((Math.Min(regions[k].Y, this.originalSize.Height) * (this.originalSize.Width - 1)) + Math.Min(regions[k].X, this.originalSize.Width)) / totalSize) * 10));
                }

                this.SetPcComplete(20);
                this.areAllThreadsCreated = true;
                lock ((this.threadCompleteCounterLock))
                {
                    if (this.threadCompleteCounter == this.threadCounter)
                    {
                        this.HandleUnpackComplete();
                    }
                }
            }
            catch (Exception ex)
            {
                ForkandBeard.Logic.ExceptionHandler.HandleException(ex, "cat@forkandbeard.co.uk");
            }
        }

        private void HandleDividedAreaThread(object regionObject)
        {
            try
            {
                if (Environment.ProcessorCount == 1)
                {
                    System.Threading.Thread.Sleep(25);
                }

                this.HandleDividedArea((Rectangle)regionObject, true);
            }
            catch (Exception ex)
            {
                ForkandBeard.Logic.ExceptionHandler.HandleException(ex, "cat@forkandbeard.co.uk");
            }
        }


        private void HandleDividedArea(Rectangle region, bool updateCounter)
        {
            if (region.Width > INT_MAX_REGION_WIDTH || region.Height > INT_MAX_REGION_WIDTH)
            {
                List<Rectangle> quarterRegions = new List<Rectangle>();

                // Top left.
                quarterRegions.Add(new Rectangle(region.X, region.Y, Convert.ToInt32(region.Width / 2), Convert.ToInt32(region.Height / 2)));
                // Top right.
                quarterRegions.Add(new Rectangle(region.X + Convert.ToInt32(region.Width / 2), region.Y, Convert.ToInt32(region.Width / 2) + 1, Convert.ToInt32(region.Height / 2)));
                // Bottom left.
                quarterRegions.Add(new Rectangle(region.X, region.Y + Convert.ToInt32(region.Height / 2), Convert.ToInt32(region.Width / 2), Convert.ToInt32(region.Height / 2) + 1));
                // Bottom right.
                quarterRegions.Add(new Rectangle(region.X + Convert.ToInt32(region.Width / 2), region.Y + Convert.ToInt32(region.Height / 2), Convert.ToInt32(region.Width / 2) + 1, Convert.ToInt32(region.Height / 2) + 1));
                foreach (Rectangle quarter in quarterRegions)
                {
                    this.HandleDividedArea(quarter, false);
                }
            }
            else
            {
                using (RegionUnpacker unpacker = new RegionUnpacker(this.analysisResult, region))
                {
                    unpacker.UnpackRegion();
                    lock ((this.areaUnpackedLock))
                    {
                        this.areaUnpacked += Convert.ToInt32((double)(region.Width * region.Height) * 0.8f);
                    }

                    this.SetPcComplete(20 + Convert.ToInt32(((double)this.areaUnpacked / (double)this.areaToUnpack) * 75f));

                    lock ((this.boxesLock))
                    {
                        this.boxes.AddRange(unpacker.Boxes);
                    }
                }

                lock ((this.areaUnpackedLock))
                {
                    this.areaUnpacked += Convert.ToInt32((double)(region.Width * region.Height) * 0.2f);
                }

                this.SetPcComplete(20 + Convert.ToInt32(((double)this.areaUnpacked / (double)this.areaToUnpack) * 80f));
            }

            if (updateCounter)
            {
                lock ((this.threadCompleteCounterLock))
                {
                    this.threadCompleteCounter += 1;
                    if (this.areAllThreadsCreated && this.threadCompleteCounter == this.threadCounter)
                    {
                        this.HandleUnpackComplete();
                    }
                }
            }
        }

        private void HandleUnpackComplete()
        {
            lock ((this.boxesLock))
            {
                RegionUnpacker.CombineBoxes(ref this.boxes, this.analysisResult);
            }

            this.FinishUnpacking();
        }

        /// <summary>
        /// Splitting path used by
        /// <see cref="BackgroundDetectionMethod.BorderConnectedBackground"/>. That
        /// mask already isolates every cell between the separator grid lines, so
        /// one global connected-component labelling replaces the per-region
        /// scanline heuristic (and its proximity merge, which would join
        /// neighbouring cells).
        /// </summary>
        private void UnpackConnectedComponents()
        {
            List<Rectangle> connectedBoxes = RegionUnpacker.ExtractConnectedComponents(this.analysisResult, this.SetPcComplete);

            lock ((this.boxesLock))
            {
                this.boxes.AddRange(connectedBoxes);
            }

            this.FinishUnpacking();
        }

        private void FinishUnpacking()
        {
            this.isUnpackingComplete = true;
            this._isUnpacking = false;
            this.SetPcComplete(100);
            if (UnpackingComplete != null)
            {
                UnpackingComplete();
            }
        }

        private Point GetCoordFromHash(uint hash)
        {
            return new Point((int)(hash >> 16), (int)(hash & 0x0000FFFF));
        }

        private uint GetHashFromCoord(Point point)
        {
            return (uint)((point.X << 16) + point.Y);
        }
    }
}
