using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ASU.BO
{
    /// <summary>
    /// Writes Adobe Color Table (.act) files. An .act file is a flat list of 256
    /// RGB triplets (768 bytes, no header, no per-entry alpha). ASU always stores
    /// the sheet's background colour in the reserved final slot (index 255) and
    /// pads every slot the palette does not use with that same background colour,
    /// so the table can be loaded straight into editors such as Photoshop,
    /// GraphicsGale or Aseprite.
    /// </summary>
    public static class AdobeColorTable
    {
        /// <summary>Number of colour slots in an .act file.</summary>
        public const int SlotCount = 256;

        /// <summary>
        /// Slot that holds the background colour when the palette is written in
        /// its natural order, and, when the optional trailer is written, is the
        /// one declared transparent.
        /// </summary>
        public const int BackgroundSlot = 0;

        /// <summary>
        /// Slot the background colour ends up in once the palette is flipped by
        /// <see cref="Save(string, IEnumerable{Color}, Color, bool, bool)"/>.
        /// </summary>
        public const int FlippedBackgroundSlot = SlotCount - 1;

        /// <summary>Size of the RGB table itself.</summary>
        public const int TableSize = SlotCount * 3;

        /// <summary>
        /// Extra bytes some .act writers append after the RGB table: a big-endian
        /// 16-bit colour count followed by a big-endian 16-bit index of the
        /// transparent colour. This is the only way the .act format can carry
        /// transparency, as the 256 entries themselves hold RGB only.
        /// </summary>
        public const int TrailerSize = 4;

        /// <summary>
        /// Writes <paramref name="colours"/> as an Adobe Color Table. Colours that
        /// match <paramref name="background"/> are left out of the palette body so
        /// the background only appears in its dedicated first slot
        /// (<see cref="BackgroundSlot"/>); every slot the palette does not fill is
        /// set to the background colour. Colours beyond the 255 usable slots are
        /// dropped, as the format cannot hold more.
        /// </summary>
        public static void Save(string path, IEnumerable<Color> colours, Color background)
        {
            Save(path, colours, background, false, false);
        }

        /// <summary>
        /// Same as <see cref="Save(string, IEnumerable{Color}, Color)"/> but when
        /// <paramref name="includeTransparentIndex"/> is set the 4-byte trailer is
        /// appended, declaring the colour count and the index of the slot holding
        /// the background, which is the one treated as transparent.
        /// </summary>
        public static void Save(string path, IEnumerable<Color> colours, Color background, bool includeTransparentIndex)
        {
            Save(path, colours, background, includeTransparentIndex, false);
        }

        /// <summary>
        /// Full overload. When <paramref name="flipPalette"/> is set the finished
        /// 256-entry table is written back to front: every colour ends up in
        /// reverse order, the background moves from the first slot to the last
        /// (<see cref="FlippedBackgroundSlot"/>), and the slots the colours vacated
        /// are refilled with the background colour at the front of the table.
        /// </summary>
        public static void Save(string path, IEnumerable<Color> colours, Color background, bool includeTransparentIndex, bool flipPalette)
        {
            byte[] table = new byte[TableSize + (includeTransparentIndex ? TrailerSize : 0)];
            int backgroundRgb = ToRgb(background);
            List<int> used = new List<int>();
            HashSet<int> seen = new HashSet<int>();

            seen.Add(backgroundRgb);

            if (colours != null)
            {
                foreach (Color colour in colours)
                {
                    if (used.Count >= SlotCount - 1)
                    {
                        break;
                    }

                    int rgb = ToRgb(colour);
                    if (seen.Add(rgb))
                    {
                        used.Add(rgb);
                    }
                }
            }

            int[] slots = new int[SlotCount];
            slots[BackgroundSlot] = backgroundRgb;
            for (int slot = 1; slot < SlotCount; slot++)
            {
                slots[slot] = (slot - 1) < used.Count ? used[slot - 1] : backgroundRgb;
            }

            int backgroundSlot = BackgroundSlot;
            if (flipPalette)
            {
                Array.Reverse(slots);
                backgroundSlot = FlippedBackgroundSlot;
            }

            for (int slot = 0; slot < SlotCount; slot++)
            {
                int rgb = slots[slot];

                table[slot * 3] = (byte)((rgb >> 16) & 0xFF);
                table[(slot * 3) + 1] = (byte)((rgb >> 8) & 0xFF);
                table[(slot * 3) + 2] = (byte)(rgb & 0xFF);
            }

            if (includeTransparentIndex)
            {
                WriteBigEndianShort(table, TableSize, SlotCount);
                WriteBigEndianShort(table, TableSize + 2, backgroundSlot);
            }

            File.WriteAllBytes(path, table);
        }

        /// <summary>Writes a 16-bit big-endian value, the byte order .act uses.</summary>
        private static void WriteBigEndianShort(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }

        /// <summary>
        /// Returns the distinct colours of <paramref name="image"/> in order of
        /// first appearance. Used as the palette source for sheets that do not
        /// carry an indexed palette of their own.
        /// </summary>
        public static List<Color> CollectColours(Bitmap image)
        {
            List<Color> colours = new List<Color>();
            HashSet<int> seen = new HashSet<int>();

            if (image == null)
            {
                return colours;
            }

            Rectangle full = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData data = image.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                bool negativeStride = stride < 0;
                int absStride = Math.Abs(stride);
                IntPtr basePtr = negativeStride ? data.Scan0 + (image.Height - 1) * absStride : data.Scan0;

                int[] row = new int[image.Width];
                for (int y = 0; y < image.Height; y++)
                {
                    Marshal.Copy(basePtr + (y * absStride), row, 0, row.Length);

                    for (int x = 0; x < row.Length; x++)
                    {
                        int rgb = row[x] & 0x00FFFFFF;
                        if (seen.Add(rgb))
                        {
                            colours.Add(Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF));
                        }
                    }
                }
            }
            finally
            {
                image.UnlockBits(data);
            }

            return colours;
        }

        /// <summary>Strips alpha so colours compare and store on RGB alone.</summary>
        private static int ToRgb(Color colour)
        {
            return colour.ToArgb() & 0x00FFFFFF;
        }
    }
}
