using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using ASU.Gpu;

namespace ASU.BO
{
    public class RegionUnpacker : IDisposable
    {
        public static object Wait = new object();
        public static int Counter;
        private ImageAnalysisResult Data;
        private Rectangle Region;

        public List<Rectangle> Boxes = new List<Rectangle>();
        public RegionUnpacker(ImageAnalysisResult data, Rectangle region)
        {
            this.Data = data;
            this.Region = region;
        }

        public void UnpackRegion()
        {
            this.Boxes = CreateBoxes(this.Data, this.Region);
            CombineBoxes(ref this.Boxes, this.Data);
            lock ((Wait))
            {
                Counter += 1;
                System.Threading.Monitor.PulseAll(Wait);
            }
        }

        /// <summary>
        /// Refines a single region (typically a box that was wrongly merged from
        /// several sprites) by re-running sprite-boundary detection on just that
        /// region, using the global foreground mask. Returns the sub-boxes found.
        /// </summary>
        public static List<Rectangle> ExtractSubBoxes(ImageAnalysisResult data, Rectangle region)
        {
            List<Rectangle> subBoxes = CreateBoxes(data, region);
            CombineBoxes(ref subBoxes, data);
            return subBoxes;
        }

        /// <summary>
        /// Minimum foreground area a component needs before it is reported as a
        /// sprite. Smaller islands are anti-aliasing specks left behind where the
        /// sprite meets the separator grid.
        /// </summary>
        public const int MinimumComponentArea = 16;

        /// <summary>
        /// Splits a whole sheet into sprites with a 4-connected component labelling
        /// of the foreground mask, one bounding box per component. Used by
        /// <see cref="BackgroundDetectionMethod.BorderConnectedBackground"/>, whose
        /// mask already isolates each cell: the scanline heuristic above would
        /// instead grow boxes across the grid and merge neighbouring cells.
        /// </summary>
        public static List<Rectangle> ExtractConnectedComponents(ImageAnalysisResult data, Action<int> progress)
        {
            int width = data.Width;
            int height = data.Height;
            byte[] mask = data.Mask;
            bool[] visited = new bool[mask.Length];
            List<Rectangle> boxes = new List<Rectangle>();
            List<int> pending = new List<int>();

            for (int index = 0; index < mask.Length; index++)
            {
                if (progress != null && (index & 0xFFFF) == 0)
                {
                    progress(Convert.ToInt32(90.0 * index / mask.Length));
                }

                if (mask[index] != 1 || visited[index])
                {
                    continue;
                }

                visited[index] = true;
                pending.Add(index);

                int left = index % width;
                int right = left;
                int top = index / width;
                int bottom = top;
                int area = 0;

                while (pending.Count > 0)
                {
                    int last = pending.Count - 1;
                    int current = pending[last];
                    pending.RemoveAt(last);

                    int x = current % width;
                    int y = current / width;
                    area += 1;

                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;

                    if (x > 0 && mask[current - 1] == 1 && !visited[current - 1])
                    {
                        visited[current - 1] = true;
                        pending.Add(current - 1);
                    }
                    if (x < width - 1 && mask[current + 1] == 1 && !visited[current + 1])
                    {
                        visited[current + 1] = true;
                        pending.Add(current + 1);
                    }
                    if (y > 0 && mask[current - width] == 1 && !visited[current - width])
                    {
                        visited[current - width] = true;
                        pending.Add(current - width);
                    }
                    if (y < height - 1 && mask[current + width] == 1 && !visited[current + width])
                    {
                        visited[current + width] = true;
                        pending.Add(current + width);
                    }
                }

                if (area >= MinimumComponentArea)
                {
                    boxes.Add(new Rectangle(left, top, right - left + 1, bottom - top + 1));
                }
            }

            return boxes;
        }

        private static List<Rectangle> CreateBoxes(ImageAnalysisResult data, Rectangle region)
        {
            List<Rectangle> boxes = new List<Rectangle>();
            Point presentPixel;
            Rectangle newBox;
            int x2 = 0;
            int y2 = 0;

            for (int y = region.Top; y <= region.Bottom; y++)
            {

                for (int x = region.Left; x <= region.Right; x++)
                {
                    if (x > 0 && x < data.Width && y > 0 && y < data.Height)
                    {
                        presentPixel = new Point(x, y);

                        if (data.IsForeground(x, y))
                        {
                            newBox = new Rectangle(presentPixel, new Size(0, 0));
                            x2 = x;

                            while (x2 < (data.Width - 1) && data.IsForeground(x2, y))
                            {
                                x2 += 1;
                                newBox = new Rectangle(newBox.X, newBox.Y, newBox.Width + 1, newBox.Height);
                            }

                            y2 = y;
                            while (y2 < (data.Height - 1) && data.IsForeground(x2, y2))
                            {
                                y2 += 1;
                                newBox = new Rectangle(newBox.X, newBox.Y, newBox.Width, newBox.Height + 1);
                            }

                            y2 = y + newBox.Height;
                            while (y2 < (data.Height - 1) && data.IsForeground(x, y2))
                            {
                                y2 += 1;
                                newBox = new Rectangle(newBox.X, newBox.Y, newBox.Width, newBox.Height + 1);
                            }

                            boxes.Add(newBox);

                            x += (newBox.Width + 1);
                        }
                    }
                }
            }

            return boxes;
        }

        public static void CombineBoxes(ref List<Rectangle> boxes, ImageAnalysisResult data)
        {
            int index = 0;
            do
            {
                index = CombineFirstOverlappingBox(ref boxes, data, index);
                // There is a bug here where -1 is returned even when boxes still need to be combined so just a hack to try again even if 
                // index is -1. Keep trying.
                if(index == -1)
                {
                    index = 0;
                    index = CombineFirstOverlappingBox(ref boxes, data, index);
                }
            } while (index != -1);
        }

        private static int CombineFirstOverlappingBox(ref List<Rectangle> boxes, ImageAnalysisResult data, int startIndex)
        {
            Rectangle newBox = Rectangle.Empty;
            List<Rectangle> oldBoxes = new List<Rectangle>();
            int returnIndex = -1;
            Rectangle box;
            
            for (int i = startIndex; i <= boxes.Count - 1; i++)
            {
                box = boxes[i];

                foreach (Rectangle collider in boxes)
                {
                    if (box != collider)
                    {
                        if (DoBoxesContainAdjacentOrOverlappingPixels(box, collider, data))
                        {
                            newBox = box;

                            returnIndex = i;
                            if (collider.Right > newBox.Right)
                            {
                                newBox.Width = collider.Right - newBox.Left;
                            }

                            if (collider.Left < newBox.Left)
                            {
                                newBox.Width += newBox.Left - collider.Left;
                            }

                            if (collider.Bottom > newBox.Bottom)
                            {
                                newBox.Height = collider.Bottom - newBox.Top;
                            }

                            if (collider.Top < newBox.Top)
                            {
                                newBox.Height += newBox.Top - collider.Top;
                            }

                            newBox.X = Math.Min(newBox.X, collider.X);
                            newBox.Y = Math.Min(collider.Y, newBox.Y);

                            oldBoxes.Add(box);
                            oldBoxes.Add(collider);
                            break; // TODO: might not be correct. Was : Exit For
                        }
                    }
                }

                if (newBox != Rectangle.Empty)
                {
                    break; // TODO: might not be correct. Was : Exit For
                }
            }

            if (newBox != Rectangle.Empty)
            {
                foreach (Rectangle oldBox in oldBoxes)
                {
                    boxes.Remove(oldBox);
                }
                boxes.Add(newBox);
            }

            return returnIndex;
        }

        public static void DeleteAllTempFiles()
        {
            try
            {
                Console.WriteLine("Deleting from " + Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                foreach (string file in System.IO.Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "asu_temp_spritesheet*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    Console.WriteLine("Deleting " + file);
                    System.IO.File.Delete(file);
                }
            }
            catch (Exception ignore)
            {
                Console.WriteLine(ignore.ToString());
            }
        }

        private static bool DoBoxesContainAdjacentOrOverlappingPixels(Rectangle box1, Rectangle box2, ImageAnalysisResult data)
        {
            Rectangle intersection;

            if (box1.IntersectsWith(box2))
            {
                intersection = Rectangle.Intersect(box1, box2);
                for (int x = intersection.X; x <= intersection.Right; x++)
                {
                    for (int y = intersection.Y; y <= intersection.Bottom; y++)
                    {                       
                        if (data.IsForeground(x, y))
                        {
                            return true;
                        }
                    }
                }

            }

            if (ForkandBeard.Util.Geometry.GeometryHelper.GetXGapBetweenRectangles(box1, box2) <= UI.MainForm.DistanceBetweenTiles)
            {
                for (int y = box1.Y - UI.MainForm.DistanceBetweenTiles; y <= box1.Bottom + UI.MainForm.DistanceBetweenTiles; y++)
                {
                    if (y >= box2.Top && y <= box2.Bottom)
                    {
                        if (box2.Left > box1.Right)
                        {
                            if (data.IsForeground(box1.Right, y))
                            {
                                if (data.IsForeground(box2.Left, y))
                                {
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            if (data.IsForeground(box1.Left, y))
                            {
                                if (data.IsForeground(box2.Right, y))
                                {
                                    return true;
                                }
                            }
                        }

                    }
                }
            }

            if (ForkandBeard.Util.Geometry.GeometryHelper.GetYGapBetweenRectangles(box1, box2) <= UI.MainForm.DistanceBetweenTiles)
            {
                for (int x = box1.Left - UI.MainForm.DistanceBetweenTiles; x <= box1.Right + UI.MainForm.DistanceBetweenTiles; x++)
                {
                    if (x >= box2.Left && x <= box2.Right)
                    {
                        if (box2.Top > box1.Bottom)
                        {
                            if (data.IsForeground(x, box1.Bottom))
                            {
                                if (data.IsForeground(x, box2.Top))
                                {
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            if (data.IsForeground(x, box1.Top))
                            {
                                if (data.IsForeground(x, box2.Bottom))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        #region "IDisposable Support"
        // To detect redundant calls
        private bool disposedValue;

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                // TODO: set large fields to null.
            }
            this.disposedValue = true;
        }

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}
