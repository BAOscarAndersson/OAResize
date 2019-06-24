using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using BitMiracle.LibTiff.Classic;
using System.Drawing;
using System.Drawing.Imaging;



namespace BarebonesImageLibrary
{
    /// <summary>
    /// A 1-bit TIF image consisting only of width, width with pad, heigth and the bytestream.
    /// <para>Contains the methods setPixel and getPixel</para>
    /// </summary>
    public class BarebonesImage
    {
        //A few things that makes up a barebones image.
        internal int Height { get; set; }
        internal int Width { get; set; }
        internal int WidthWithPad { get; set; }
        internal List<List<byte>> ImageMatrix { get; set; }

        /// <summary>
        /// Get the value of a pixel in the image.
        /// </summary>
        /// <param name="x">The pixels x coordinate.</param>
        /// <param name="y">The pixels y coordinate.</param>
        /// <returns>Returns what the value of the pixel is.</returns>
        internal bool GetPixel(int x, int y)
        {
            return GetBit(ImageMatrix[y][x / 8], 7 - x % 8);
        }

        /// <summary>
        /// Set the value of a pixel in the image.
        /// </summary>
        /// <param name="x">The pixels x coordinate.</param>
        /// <param name="y">The pixels y coordinate.</param>
        /// <param name="value">The value the pixel should be set to.</param>
        /// <returns>True if successful.</returns>
        internal bool SetPixel(int x, int y, Boolean value)
        {
            ImageMatrix[y][x / 8] = SetByte(ImageMatrix[y][x / 8], 7 - x % 8, value);

            return true;
        }

        /// <summary>
        /// Gets a specific bit from a byte.
        /// </summary>
        /// <param name="b">The byte to get the bit from.</param>
        /// <param name="index">The bit to get from the byte.</param>
        /// <returns>The value of the bit.</returns>
        private bool GetBit(byte b, int index)
        {
            return (b & (1 << index)) != 0;
        }

        /// <summary>
        /// Sets a bit in a byte to a specific value
        /// </summary>
        /// <param name="b">The byte to change.</param>
        /// <param name="index">The bit which should be changed.</param>
        /// <param name="bitValue">The value to change to.</param>
        /// <returns>The changed byte.</returns>
        private byte SetByte(byte b, int index, bool bitValue)
        {
            return (byte)(bitValue ? (b | (1 << index)) : (b & ~(1 << index)));
        }

        /// <summary>
        /// Method is called to read a tiff image
        /// </summary>
        /// <param name="fileToRead">The file that will be read including the path.</param>
        /// <returns>A BarebonesImage.</returns>
        public BarebonesImage ReadATIFF(string fileToRead)
        {
            //Overrides the error handler so that it won't spit out a bunch of warnings.
            BBErrorHandler bbErrorHandler = new BBErrorHandler();
            Tiff.SetErrorHandler(bbErrorHandler);

            Tiff inputImage = Tiff.Open(fileToRead, "r");
            BarebonesImage bbImage = new BarebonesImage
            {
                Width = inputImage.GetField(TiffTag.IMAGEWIDTH)[0].ToInt(),
                Height = inputImage.GetField(TiffTag.IMAGELENGTH)[0].ToInt()
            };

            #region Check that the file is of the correct format. Lots of boring code.
            int samplesPerPixel = inputImage.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            if (samplesPerPixel != 1)
                throw new ArgumentException("samplesPerPixel must be 1");

            int bitsPerSample = inputImage.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
            if (bitsPerSample != 1)
                throw new ArgumentException("bitsPerSample must be 1");

            string photo = inputImage.GetField(TiffTag.PHOTOMETRIC)[0].ToString();
            if (string.Compare(photo, @"MINISWHITE") != 0)
                throw new ArgumentException("photo must be MINISWHITE");

            //string compression = inputImage.GetField(TiffTag.COMPRESSION)[0].ToString();
            // if (string.Compare(compression, @"CCITT_T6") != 0)
            //      throw new ArgumentException("Compression must be CCITT_T6");

            string plane = inputImage.GetField(TiffTag.PLANARCONFIG)[0].ToString();
            if (string.Compare(plane, @"CONTIG") != 0)
                throw new ArgumentException("plane must be CONTIG");

            string resUnit = inputImage.GetField(TiffTag.RESOLUTIONUNIT)[0].ToString();
            if (string.Compare(resUnit, @"INCH") != 0)
                throw new ArgumentException("resUnit must be INCH");

            #endregion

            //Images are padded with zeros so the images consist of whole bytes as per the TIF specification.
            bbImage.WidthWithPad = bbImage.Width + bbImage.CalculatePad(bbImage.Width);

            byte[] ImageByteStream = new byte[bbImage.WidthWithPad / 8 * bbImage.Height];

            // Read for multiple strips
            int stripSize = inputImage.StripSize();
            int stripMax = inputImage.NumberOfStrips();
            int imageOffset = 0;

            //This ImageMatrix is where the data of the image will be stored in the Barebones Image.
            bbImage.ImageMatrix = new List<List<byte>>();

            // Goes through all the strips and put them into a single bytestreame.
            for (int stripCount = 0; stripCount < stripMax; stripCount++)
            {
                int result = inputImage.ReadEncodedStrip(stripCount, ImageByteStream, imageOffset, stripSize);
                if (result == -1)
                {
                    Console.Error.WriteLine("Read error on input strip number {0}", stripCount);
                }

                imageOffset += result;
            }

            //Convert the array to a list, chop it up and insert it into the ImageMatrix of the Barebones Image.
            List<byte> ImageByteList = ImageByteStream.ToList();
            for (int i = 0; i < ImageByteList.Count; i += bbImage.WidthWithPad / 8)
            {
                bbImage.ImageMatrix.Add(ImageByteList.GetRange(i, Math.Min(bbImage.WidthWithPad / 8, ImageByteList.Count - i)));
            }


            inputImage.Close();

            return bbImage;
        }

        public Task<BarebonesImage> ReadATIFFAsync(string fileToRead)
        {
            return Task.Run(() =>
            {
                return ReadATIFF(fileToRead);
            });
        }

        /// <summary>
        /// Method is called to save a BarebonesImage as a TIFF file.
        /// </summary>
        /// <param name="fileToSave"></param>
        /// <param name="pathNameAndFilename"> The path and file name the image will be saved to.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool SaveAsTIFF(string pathAndFilename)
        {

            using (Tiff image = Tiff.Open(pathAndFilename, "w"))
            {
                if (image == null)
                {
                    Console.Error.WriteLine("Could not open" + pathAndFilename + " for writing");
                    return false;
                }

                #region Sets a bunch of tags to standard values very boring.
                image.SetField(TiffTag.BITSPERSAMPLE, 1);
                image.SetField(TiffTag.SAMPLESPERPIXEL, 1);

                image.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX4);
                image.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
                image.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
                image.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                image.SetField(TiffTag.XRESOLUTION, 1200.0);
                image.SetField(TiffTag.YRESOLUTION, 1200.0);
                image.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                #endregion

                //Sets the width and height to that of the BarebonesImage
                image.SetField(TiffTag.IMAGEWIDTH, Width);
                image.SetField(TiffTag.IMAGELENGTH, Height);
                image.SetField(TiffTag.ROWSPERSTRIP, Height);

                //Construct a ImageByteStream from ImageMatrix
                byte[] imageByteStream = new byte[WidthWithPad / 8 * Height];

                for (int x = 0; x < Height; x++)
                {

                    for (int y = 0; y < WidthWithPad / 8; y++)
                    {
                        imageByteStream[x * WidthWithPad / 8 + y] = ImageMatrix[x][y];
                    }
                }

                // Write the information to the file
                image.WriteEncodedStrip(0, imageByteStream, WidthWithPad / 8 * Height);
            }

            return true;
        }

        /// <summary>
        /// Takes two BBImages and merges them into a single BBImage.
        /// </summary>
        /// <param name="Left">The BBImage that will be to the left in the new BBImage.</param>
        /// <param name="Right">The BBImage that will be to the right in the new BBImage.</param>
        /// <returns>True upon completion</returns>
        public bool Merge(BarebonesImage Left, BarebonesImage Right)
        {
            //Width of the merged image is the widths of both the input images.
            Width = Left.Width + Right.Width;

            //Padding for width is recalculated.
            WidthWithPad = Width + CalculatePad(Width);

            //Sets the height of the merged image to the tallest of the input images.
            if (Left.Height > Right.Height)
            {
                Height = Left.Height;
            }
            else if (Left.Height < Right.Height)
            {
                Height = Right.Height;

            }
            else
                Height = Left.Height;

            // The image is offset vertically so it ends up in the middle.
            int verticalOffset = (Height - Left.Height) / 2;
            this.Insert(Left, verticalOffset, 0);

            //Offset recalculated for the right image.
            verticalOffset = (Height - Right.Height) / 2;
            this.Insert(Right, verticalOffset, 0);

            return true;
        }

        /// <summary>
        /// Writes the input string to the barebones image at a certain location.
        /// </summary>
        /// <param name="textToWrite">A string to write to the barebones image.</param>
        /// <param name="fontSize">The size of the font to be written to the barebones image.</param>
        /// <param name="x">The width position to write the string at.</param>
        /// <param name="y">The height position to write the string at.</param>
        /// <returns>True upon completion.</returns>
        public bool WriteText(string textToWrite, int x, int y, int fontSize)
        {
            //Initialize an bitmap image and a graphic object that the input text will be drawn on.
            Bitmap image = new Bitmap(textToWrite.Length * fontSize, fontSize * 2);
            Graphics graph = Graphics.FromImage(image);

            //Draws the text onto the bitmap image
            graph.DrawString(textToWrite, new Font(new FontFamily("Calibri"), fontSize, FontStyle.Regular), Brushes.Black, 0, 0);

            //Goes through all the pixels in the image and transfers them to the barebones image.
            for (int i = 0; i < textToWrite.Length * fontSize; i++)
            {
                for (int j = 0; j < fontSize * 2; j++)
                {
                    //Reads the colour from the bitmap image the text was written to.
                    Color colour = image.GetPixel(i, j);
                    int aRGBvalue = colour.ToArgb();

                    //Rotate the text so it ends up at the bottom of the printing plate.
                    if (aRGBvalue == 0)
                        SetPixel(fontSize * 2 - j + x, i + y, false);
                    else
                        SetPixel(fontSize * 2 - j + x, i + y, true);
                }
            }

            return true;
        }

        /// <summary>
        /// Writes the input string to the barebones image at a certain location.
        /// </summary>
        /// <param name="BarcodeToWrite">A string to write to the barebones image.</param>
        /// <param name="fontSize">The size of the font to be written to the barebones image.</param>
        /// <param name="x">The width position to write the barcode at.</param>
        /// <param name="y">The height position to write the barcode at.</param>
        /// <returns>True upon completion.</returns>
        public bool WriteBarcode(string BarcodeToWrite, int x, int y)
        {
            //Loop through all the characters to be written and write them one by one.
            for (int i = 0; i < BarcodeToWrite.Length; i++)
            {
                //Convert the UTF-8 characters to Code39 and add a spacing after each.
                string code39 = GetCode39(BarcodeToWrite[i]);
                code39 += "w";

                //Loop through all the bars that make up the coding for the character.
                for (int j = 0; j < code39.Length; j++)
                {
                    switch (code39[j])
                    {
                        /*If the bar is a wide white, make it 37 pixels and set the pixels to white. 
                          Update the y-poistion to reflect that a line has been drawn.*/
                        case 'W':
                            DrawLine(x, y, 39, 500, 0, false);
                            y = y + 39;
                            break;
                        /*If the bar is a thin white, make it 13 pixels and set the pixels to white.
                          Update the y - poistion to reflect that a line has been drawn.*/
                        case 'w':
                            DrawLine(x, y, 13, 500, 0, false);
                            y = y + 13;
                            break;
                        /*If the bar is a wide black, make it 37 pixels and set the pixels to black.
                          Update the y - poistion to reflect that a line has been drawn.*/
                        case 'B':
                            DrawLine(x, y, 39, 500, 0, true);
                            y = y + 39;
                            break;
                        /*If the bar is a wide black, make it 13 pixels and set the pixels to black.
                          Update the y - poistion to reflect that a line has been drawn.*/
                        case 'b':
                            DrawLine(x, y, 13, 500, 0, true);
                            y = y + 13;
                            break;

                        default:
                            return false;
                    }
                }
            }

            return true;

        }

        /// <summary>
        /// Converts letters A-X, numbers 0-9 and a few special characters to Code39.
        /// <para>Encoding for wide white is W, narrow white is w, wide black is B and narrow black is b.</para>
        /// </summary>
        /// <param name="charToBarcode">A character that can be encoded as Code39.</param>
        /// <returns>Code 39 charcters as a string of W,w,B and b's.</returns>
        private string GetCode39(char charToBarcode)
        {

            switch (charToBarcode)
            {
                //The letters
                case 'A':
                    return @"BwbwbWbwB";
                case 'B':
                    return @"bwBwbWbwB";
                case 'C':
                    return @"BwBwbWbwb";
                case 'D':
                    return @"bwbwBWbwB";
                case 'E':
                    return @"BwbwBWbwb";
                case 'F':
                    return @"bwBwBWbwb";
                case 'G':
                    return @"bwbwbWBwB";
                case 'H':
                    return @"BwbwbWBwb";
                case 'I':
                    return @"bwBwbWBwb";
                case 'J':
                    return @"bwbwBWBwb";
                case 'K':
                    return @"BwbwbwbWB";
                case 'L':
                    return @"bwBwbwbWB";
                case 'M':
                    return @"BwBwbwbWb";
                case 'N':
                    return @"bwbwBwbWB";
                case 'O':
                    return @"BwbwBwbWb";
                case 'P':
                    return @"bwBwBwbWb";
                case 'Q':
                    return @"bwbwbwBwB";
                case 'R':
                    return @"BwbwbwBWb";
                case 'S':
                    return @"bwBwbwBWb";
                case 'T':
                    return @"bwbwBwBWb";
                case 'U':
                    return @"BWbwbwbwB";
                case 'V':
                    return @"bWBwbwbwB";
                case 'W':
                    return @"BWBwbwbwb";
                case 'X':
                    return @"bWbwBwbwB";
                case 'Y':
                    return @"BWbwBwbwb";
                case 'Z':
                    return @"bWBwBwbwb";

                //Numbers
                case '0':
                    return @"bwbWBwBwb";
                case '1':
                    return @"BwbWbwbwB";
                case '2':
                    return @"bwBWbwbwB";
                case '3':
                    return @"BwBWbwbwB";
                case '4':
                    return @"bwbWBwbwB";
                case '5':
                    return @"BwbWBwbwb";
                case '6':
                    return @"bwBWBwbwb";
                case '7':
                    return @"bwbWbwBwB";
                case '8':
                    return @"BwbWbwBwb";
                case '9':
                    return @"bwBwbwBwb";

                //Special characters including start/stop *.
                case ' ':
                    return @"bWBwbwBwb";
                case '-':
                    return @"bWbwbwBwB";
                case '$':
                    return @"bWbWbWbwb";
                case '%':
                    return @"bwbWbWbWb";
                case '.':
                    return @"BWbwbwBwb";
                case '/':
                    return @"bWbWbwbWb";
                case '+':
                    return @"bWbwbWbWb";
                case '*':
                    return @"bWbwBwBwb";

                default:
                    return @"bWBwbwBwb";
            }

        }

        /// <summary>
        /// Draws a line from (x,y) of a provided thickness, length, colour and angle.
        /// </summary>
        /// <param name="x">Start position for the line.</param>
        /// <param name="y">Start position for the line.</param>
        /// <param name="size">Thickness of the line.</param>
        /// <param name="length">Length of the line.</param>
        /// <param name="angle">The angle of the line.</param>
        /// <param name="colour">Black or white lines are possible. False is black and true is white.</param>
        /// <returns>True if line was drawn.</returns>
        private bool DrawLine(int x, int y, int size, int length, float angle, bool colour)
        {
            //Only implemented for horizontal lines...
            if (angle == 0)
            {
                for (int m = 0; m <= length; m++)
                {
                    for (int n = 0; n <= size; n++)
                    {
                        SetPixel(x + m, y + n, colour);
                    }
                }

                return true;
            }
            else
                return false;

        }

        /// <summary>
        /// Cacluates the padding of a barebones image for a certain width.
        /// </summary>
        /// <param name="inputWidth">The width that the padding for will be calculated.</param>
        /// <returns>Padding only, width not included.</returns>
        private int CalculatePad(int inputWidth)
        {
            //Padding for width iscalculated.
            int padding = inputWidth;
            while ((padding % 8) != 0)
            {
                padding++;
            }

            return padding - inputWidth;
        }

        /// <summary>
        /// Converts a BarebonesImages to a BMP image.
        /// </summary>
        /// <param name="outWidth">The width of the BBImage.</param>
        /// <param name="outHeight">The height of the BBImage.</param>
        /// <returns>A bitmap.</returns>
        //public Bitmap ToBitmap(int outWidth, int outHeight)
        //{
        //    //Instanciate the image that will be returned and an graphic object that will do some manipulations of the image.
        //    Bitmap convertedImage = new Bitmap(outWidth, outHeight, PixelFormat.Format1bppIndexed);
        //    Rectangle rect = new Rectangle(0, 0, outWidth, outHeight);

        //    BitmapData bmpData = convertedImage.LockBits(rect, ImageLockMode.ReadWrite, convertedImage.PixelFormat);

        //    // Get the address of the first line.
        //    IntPtr ptr = bmpData.Scan0;

        //    // Copy the array into the bitmap.
        //    System.Runtime.InteropServices.Marshal.Copy(this.ImageByteStream, 0, ptr, this.Height * this.WidthWithPad / 8);

        //    // Unlock the bits.
        //    convertedImage.UnlockBits(bmpData);

        //    return convertedImage;
        //}

        /// <summary>
        /// Turns a 1bpp bitmap into a Barebones Image. 
        /// </summary>
        /// <param name="inputBitmap">The image to be turned into a BB image.</param>
        /// <returns>true upon completion</returns>
        //public bool BitmapToBBImage(Bitmap inputBitmap)
        //{
        //    //New dimensions of the BBImage
        //    this.Width = inputBitmap.Width;
        //    this.Height = inputBitmap.Height;

        //    //Padding for width is recalculated.
        //    WidthWithPad = Width + CalculatePad(Width);

        //    Rectangle rect = new Rectangle(0, 0, inputBitmap.Width, inputBitmap.Height);

        //    BitmapData bmpData = inputBitmap.LockBits(rect, ImageLockMode.ReadWrite, inputBitmap.PixelFormat);

        //    // Get the address of the first line.
        //    IntPtr ptr = bmpData.Scan0;

        //    System.Runtime.InteropServices.Marshal.Copy(ptr, this.ImageByteStream, 0, this.Height * this.WidthWithPad / 8);

        //    inputBitmap.UnlockBits(bmpData);

        //    return true;

        //}

        /// <summary>
        /// Makes the BBImage.height smaller by a scale factor.
        /// </summary>
        /// <param name="scale">Height size will be multiplied with 1-(1/scale).</param>
        public void DownsizeHeight(int scale)
        {
            if (scale == 0)
                return;

            //Go through the image and remove all the rows that are an multiple of scale.
            for (int y = Height; y > 0; y--)
            {
                if (y % scale == 0)
                    ImageMatrix.RemoveAt(y - 1);
            }

            //The number of rows of the picture has been changed so height must be recalulated.
            Height = Convert.ToInt32(Math.Truncate((double)this.Height * ((double)1 - ((double)1 / (double)scale))));

            return;
        }

        /// <summary>
        /// Inserts an BBImage into this BBImage.
        /// </summary>
        /// <param name="insertImage">The BBImage that will be inserted into this BBImage.</param>
        /// <param name="x">The place where the image will be inserted.</param>
        /// <param name="y">The place where the image will be inserted.</param>
        /// <returns>True upon completion</returns>
        public bool Insert(BarebonesImage insertImage, int x, int y)
        {

            /* Loop through all the pixles in the image to be inserted.
             * Offset it vertically so it ends up in the right position*/

            for (int i = 0; i < insertImage.Width; i++)
            {
                for (int j = 0; j < insertImage.Height; j++)
                {
                    SetPixel(i + x, j + y, insertImage.GetPixel(i, j));
                }
            }

            return true;
        }

        /// <summary>
        /// Pads the image with a specified amount of rows at a specified row position.
        /// </summary>
        /// <param name="x">The row where the padding should be inserted</param>
        /// <param name="amount">The amount of rows to insert.</param>
        public void PadHeight(int x, int amount)
        {
            byte[] zeroByteRow = new byte[this.WidthWithPad / 8];

            for (int i = 0; i < amount; i++)
                this.ImageMatrix.Insert(x, zeroByteRow.ToList());

            this.Height = this.Height + amount;

            return;
        }

        /// <summary>
        /// Moves an image up or down.
        /// </summary>
        /// <param name="upOrDown">"up" or "down", everything else throws an expection.</param>
        /// <param name="amount">How much to move the image in pixels.</param>
        public void MoveImage(string upOrDown, int amount)
        {
            if (upOrDown == "up")
            {
                //Removes rows at the top of the image and then inserts zero padding at the bottom to compensate.
                for (int i = 0; i < amount; i++)
                {
                    this.ImageMatrix.RemoveAt(0);
                }
                this.Height = this.Height - amount;
                PadHeight(this.ImageMatrix.Count - 1, amount);
            }
            else if (upOrDown == "down")
            {
                //Removes bottom rows and the inserts zero padding at the top to compensate.
                for (int i = 0; i < amount; i++)
                {
                    this.ImageMatrix.RemoveAt(this.ImageMatrix.Count - 1);
                }
                this.Height = this.Height - amount;
                PadHeight(0, amount);
            }
            else
                throw new ArgumentException("Argument must be 'up' or 'down'.");

        }

        /// <summary>
        /// Calculates the coverage of an area, that is the number of pixels that are active.
        /// The area is just a 3by3 for now.
        /// </summary>
        /// <param name="x">The x position of the pixel in the middle of the area.</param>
        /// <param name="y">The y position of the pixel in the middle of the area.</param>
        /// <returns>Amount of active pixels in the area.</returns>
        public ushort GetColourOfArea(int x, int y)
        {
            x--;
            y--;

            ushort resultColour = 0;

            if (GetPixel(x - 1, y - 1))
                resultColour += 1;
            if (GetPixel(x, y - 1))
                resultColour += 1;
            if (GetPixel(x - 1, y))
                resultColour += 1;
            if (GetPixel(x - 1, y + 1))
                resultColour += 1;
            if (GetPixel(x, y))
                resultColour += 1;
            if (GetPixel(x + 1, y - 1))
                resultColour += 1;
            if (GetPixel(x + 1, y))
                resultColour += 1;
            if (GetPixel(x, y + 1))
                resultColour += 1;
            if (GetPixel(x + 1, y + 1))
                resultColour += 1;

            return resultColour;

        }
    }

    /// <summary>
    /// Overrides the error handler so that it won't spit out a bunch of warnings.
    /// </summary>
    public class BBErrorHandler : TiffErrorHandler
    {
        public override void WarningHandler(Tiff tif, string method, string format, params object[] args)
        {
        }
        public override void WarningHandlerExt(Tiff tif, object clientData, string method, string format, params object[] args)
        {
        }
    }


    /// <summary>
    /// A CMYKbbImage consists of four BareboneImages, one for each of colours in a CMYK set.
    /// <para>It also contains a method to convert a CMYKbbImage to a colourized BPM image.</para>
    /// </summary>
    public class CMYKbbImage : BarebonesImage
    {

        public BarebonesImage cyan;
        public BarebonesImage magenta;
        public BarebonesImage yellow;
        public BarebonesImage black;
        public Bitmap composedImage;

        public CMYKbbImage()
        {
            cyan = new BarebonesImage();
            magenta = new BarebonesImage();
            yellow = new BarebonesImage();
            black = new BarebonesImage();
        }

        /// <summary>
        /// Takes a CMYKbbImage and turns it into a color BMP.
        /// </summary>
        /// <param name="outWidth">Size of image to make.</param>
        /// <param name="outHeight">Size of image to make.</param>
        /// <returns>A CMYKbbImage which contains a BMP composed of the CMYKbbImage's BBImages..</returns>
        public CMYKbbImage BBImageToBitmap(int outWidth, int outHeight, IProgress<int> progress)
        {
            //Instanciate the image that will be returned.
            this.composedImage = new Bitmap(outWidth / 3, outHeight / 3, PixelFormat.Format24bppRgb);

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, composedImage.Width, composedImage.Height);
            BitmapData bmpData = composedImage.LockBits(rect, ImageLockMode.ReadWrite, composedImage.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * composedImage.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Compute the black value for each pixel in the composed image from 3*3 pixels in the K image.  
            for (int i = 1; i < composedImage.Width - 1; i++)
            {
                for (int j = 1; j < composedImage.Height - 1; j++)
                {
                    ushort antiRed = cyan.GetColourOfArea(i * 3, j * 3);
                    ushort antiGreen = magenta.GetColourOfArea(i * 3, j * 3);
                    ushort antiBlue = yellow.GetColourOfArea(i * 3, j * 3);
                    ushort justBlack = black.GetColourOfArea(i * 3, j * 3);

                    rgbValues[j * bmpData.Stride + i * 3 + 2] = (byte)(252 - (antiRed * 12 + justBlack * 16));
                    rgbValues[j * bmpData.Stride + i * 3 + 1] = (byte)(252 - (antiGreen * 12 + justBlack * 16)); ;
                    rgbValues[j * bmpData.Stride + i * 3 + 0] = (byte)(252 - (antiBlue * 12 + justBlack * 16)); ;
                }

                //Reports progress because this method is used in an Async method.
                if (progress != null)
                {
                    progress.Report((i * 1000) / composedImage.Width);
                }
            }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            composedImage.UnlockBits(bmpData);

            return this;
        }

        /// <summary>
        /// Asynced version of BBImageToBitmap. Takes a CMYKbbImage and turns it into a color BMP.
        /// </summary>
        /// <param name="outWidth">Size of image to make.</param>
        /// <param name="outHeight">Size of image to make.</param>
        /// <returns>A CMYKbbImage with a composed BMP.</returns>
        public Task<CMYKbbImage> BBImageToBitmapAsync(int outWidth, int outHeight, IProgress<int> progress)
        {
            return Task.Run(() =>
            {
                return BBImageToBitmap(outWidth, outHeight, progress);
            });
        }


        /// <summary>
        /// Samples an area of the four BarebonesImages and calculates how many percent of the pixels are black.
        /// </summary>
        /// <param name="x">The width position to start the sampling at.</param>
        /// <param name="y">The highth position to start the sampling at.</param>
        /// <param name="sampleArea">How large an area to sample over.</param>
        /// <returns>The sampled CMYK values as a struct.</returns>
        public CMYKvalues TotalAreaCoverageSampler(int x, int y, int sampleArea)
        {

            CMYKvalues sampleValues = new CMYKvalues();

            //Loops through all the pixels and count the ones.
            for (int i = 0; i < sampleArea; i++)
            {

                for (int j = 0; i < sampleArea; i++)
                {
                    if (cyan.GetPixel(x + i, y + j))
                        sampleValues.C += 1;
                    if (magenta.GetPixel(x + i, y + j))
                        sampleValues.M += 1;
                    if (yellow.GetPixel(x + i, y + j))
                        sampleValues.Y += 1;
                    if (black.GetPixel(x + i, y + j))
                        sampleValues.K += 1;

                }

            }

            //Divide the number of black pixels with the area size.
            sampleValues.C = sampleValues.C / sampleArea;
            sampleValues.M = sampleValues.M / sampleArea;
            sampleValues.Y = sampleValues.Y / sampleArea;
            sampleValues.K = sampleValues.K / sampleArea;

            return sampleValues;

        }


    }

    /// <summary>
    /// Contains values for the sampling of TotalAreaCoverageSampler.
    /// </summary>
    public struct CMYKvalues
    {
        public double C;
        public double M;
        public double Y;
        public double K;

    }


}
