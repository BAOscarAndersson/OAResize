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
        private int height;
        private int width;
        private int widthWithPad;

        private byte[] imageByteStream;

        #region A bunch of gets and sets for all those private things above.
        public int Height
        {
            get => height;
            set => height = value;
        }

        public int Width
        {
            get => width;
            set => width = value;
        }

        public int WidthWithPad
        {
            get => widthWithPad;
            set => widthWithPad = value;
        }

        public byte[] ImageByteStream
        {
            get => imageByteStream;
            set => imageByteStream = value;
        }
        #endregion
        
        /// <summary>
        /// Get the value of a pixel in the image.
        /// </summary>
        /// <param name="x">The pixels x coordinate.</param>
        /// <param name="y">The pixels y coordinate.</param>
        /// <returns>Returns what the value of the pixel is.</returns>
        internal bool GetPixel(int x, int y)
        {
            bool twoBool = new bool();

            //if one of the coordinates are outside of the image return false.
            if (x >= width || y >= height || x <= 0 || y <= 0)
            {

                return false;

            }

            //Change the indexes to the once used in C#.
            x--;
            y--;

            twoBool = GetBit(imageByteStream[y * widthWithPad / 8 + x / 8], 7 - x % 8);
            
            return twoBool;
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
            //if one of the coordinates are outside of the image return false (failure to set.)
            if (x >= width || y >= height || x <= 0 || y <= 0)
            {
                return false;
            }

            //Change the indexes to the once used in C#.
            x--;
            y--;

            /* y*widthWithPad/8+x/8 converts the x and y coordinate to a the pixels byte-position. 
             * 7-x%8 converts a pixels x coordinate to its place within a byte.*/
            imageByteStream[y * widthWithPad / 8 + x / 8] = SetByte(imageByteStream[y * widthWithPad / 8 + x / 8], 7 - x % 8, value);

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
            bool bit = (b & (1 << index)) != 0;
            return bit;
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
            BarebonesImage bbReturnImage = new BarebonesImage
            {
                width = inputImage.GetField(TiffTag.IMAGEWIDTH)[0].ToInt(),
                height = inputImage.GetField(TiffTag.IMAGELENGTH)[0].ToInt()
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

            //Not working for some reason.
            //string xres = inputImage.GetField(TiffTag.XRESOLUTION)[0].ToString();
            //if (string.Compare(xres, @"1200") != 0)
            //    throw new ArgumentException("xres must be 1200");

            //Not working for some reason.
            //string yres = inputImage.GetField(TiffTag.YRESOLUTION)[0].ToString();
            //if (string.Compare(yres, @"1200") != 0)
            //    throw new ArgumentException("yres must be 1200");

            #endregion
            
            //Images are padded with zeros so the images consist of whole bytes.
            bbReturnImage.widthWithPad = bbReturnImage.width + bbReturnImage.CalculatePad(bbReturnImage.width);

            bbReturnImage.imageByteStream = new byte[bbReturnImage.widthWithPad / 8 * bbReturnImage.height];

            // Read for multiple strips
            int stripSize = inputImage.StripSize();
            int stripMax = inputImage.NumberOfStrips();
            int imageOffset = 0;

            // Goes through all the strips and put them into a single bytestream in the barebonesImage.
            for (int stripCount = 0; stripCount < stripMax; stripCount++)
            {
                int result = inputImage.ReadEncodedStrip(stripCount, bbReturnImage.imageByteStream, imageOffset, stripSize);
                if (result == -1)
                {
                    Console.Error.WriteLine("Read error on input strip number {0}", stripCount);
                }

                imageOffset += result;
            }
            
            inputImage.Close();
            
            return bbReturnImage;
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
                image.SetField(TiffTag.IMAGEWIDTH, width);
                image.SetField(TiffTag.IMAGELENGTH, height);
                image.SetField(TiffTag.ROWSPERSTRIP, height);

                // Write the information to the file
                image.WriteEncodedStrip(0, imageByteStream, widthWithPad / 8 * height);

            }

            return true;
        }

        /// <summary>
        /// Takes two Mergeables and merges them into a single Mergeable, consisting of both images.
        /// </summary>
        /// <param name="Left">The Mergeable that will be to the left in the new Mergeable.</param>
        /// <param name="Right">The Mergeable that will be to the right in the new Mergeable.</param>
        /// <returns>True upon completion</returns>
        public bool Merge(BarebonesImage Left, BarebonesImage Right)
        {
            //Width of the merged image is the widths of both the input images.
            width = Left.width + Right.width;

            //Padding for width is recalculated.
            widthWithPad = width + CalculatePad(width);

            //Sets the height of the merged image to the tallest of the input images.
            if (Left.height > Right.height)
            {
                height = Left.height;
            }
            else if (Left.height < Right.height)
            {
                height = Right.height;

            }
            else
                height = Left.height;

            //Initilize the byte array for the merged image.
            imageByteStream = new byte[widthWithPad / 8 * height];
            
            /* Loop through all the pixles in the left image
             * and insert them to the left in the new image.
             * Offset it vertically so it ends up in the middle.*/
            int verticalOffset = (height - Left.height) / 2;

            for (int i = 1; i <= Left.height; i++)
            {
                for (int j = 1; j <= Left.width; j++)
                {

                    SetPixel(j, i + verticalOffset, Left.GetPixel(j, i));

                }
            }

            /* Loop through all the pixles in the right image
             * and insert them to the right in the new image.
             * Offset it vertically so it ends up in the middle.
             * And offset horizontally so it ends up to the right*/
            verticalOffset = (height - Right.height) / 2;
            for (int i = 1; i <= Right.height; i++)
            {
                for (int j = 1; j <= Right.width; j++)
                {

                    SetPixel(j + Left.width, i + verticalOffset, Right.GetPixel(j, i));

                }
            }

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
            for (int i = 1; i < textToWrite.Length * fontSize; i++)
            {
                for (int j = 1; j < fontSize * 2; j++)
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
        public Bitmap ToBitmap(int outWidth, int outHeight)
        {
            //Instanciate the image that will be returned and an graphic object that will do some manipulations of the image.
            Bitmap convertedImage = new Bitmap(outWidth, outHeight, PixelFormat.Format1bppIndexed);
            Rectangle rect = new Rectangle(0, 0, outWidth, outHeight);

            BitmapData bmpData = convertedImage.LockBits(rect, ImageLockMode.ReadWrite, convertedImage.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Copy the array into the bitmap.
            System.Runtime.InteropServices.Marshal.Copy(this.ImageByteStream, 0, ptr, this.Height * this.WidthWithPad / 8);

            // Unlock the bits.
            convertedImage.UnlockBits(bmpData);
            
            return convertedImage;
        }

        /// <summary>
        /// Turns a 1bpp bitmap into a Barebones Image. 
        /// </summary>
        /// <param name="inputBitmap">The image to be turned into a BB image.</param>
        /// <returns>true upon completion</returns>
        public bool BitmapToBBImage(Bitmap inputBitmap)
        {
            //New dimensions of the BBImage
            this.width = inputBitmap.Width;
            this.height = inputBitmap.Height;

            //Padding for width is recalculated.
            widthWithPad = width;
            while ((widthWithPad % 8) != 0)
            {
                widthWithPad++;
            }

            Rectangle rect = new Rectangle(0, 0, inputBitmap.Width, inputBitmap.Height);

            BitmapData bmpData = inputBitmap.LockBits(rect, ImageLockMode.ReadWrite, inputBitmap.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            System.Runtime.InteropServices.Marshal.Copy(ptr, this.ImageByteStream, 0, this.Height * this.WidthWithPad / 8);

            inputBitmap.UnlockBits(bmpData);

            return true;

        }
        
        /// <summary>
        /// Makes the BBImage.height smaller by a scale factor.
        /// </summary>
        /// <param name="scale">Height size will be multiplied with 1-(1/scale).</param>
        /// <returns>A barebones image that have been made smaller.</returns>
        public BarebonesImage DownsizeHeight(int scale)
        {
            if (scale == 0)
                return this;


            BarebonesImage smallerImage = new BarebonesImage();
            
            smallerImage.Width = this.width;
            smallerImage.Height = Convert.ToInt32(Math.Truncate((double)this.height * ((double)1 - ((double)1 / (double)scale))));
            smallerImage.WidthWithPad = smallerImage.width + CalculatePad(smallerImage.width);
            smallerImage.ImageByteStream = new byte[smallerImage.widthWithPad / 8 * smallerImage.height];



            /* Loop through all the pixles in the bigger image
             * and insert them into the smaller unless they are
             * in a position modulo scale in which case they are dropped.*/
            int x = 1;
            int y = 1;
            for (int i = 1; i <= this.width; i++)
            {
                x += 1;
                y = 1;

                for (int j = 1; j <= this.height; j++)
                {

                    if (j % scale == 0)
                        continue;

                    smallerImage.SetPixel(x, y, this.GetPixel(i, j));

                    y += 1;
                }
            }
            
            return smallerImage;
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

            for (int i = 1; i <= insertImage.Width; i++)
            {
                for (int j = 1; j <= insertImage.Height; j++)
                {

                    SetPixel(i + x, j + y, insertImage.GetPixel(i, j));

                }
            }

            return true;
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
        /// Converts the four black and white BarebonesImages to a colourized BMP image.
        /// </summary>
        /// <param name="scale">The scale factor that the barebones will be scaled to.</param>
        /// <returns>a CMYKbbImage with a composedImage.</returns>
        public CMYKbbImage BBImageToBitmap(int outWidth, int outHeight)
        {
            //Instanciate the image that will be returned and an graphic object that will do some manipulations of the image.
            this.composedImage = new Bitmap(outWidth, outHeight);
            Graphics graph = Graphics.FromImage(composedImage);

            #region The primary colours which can all be derived from the CMYK colours.
            Color blackC = Color.FromName("Black");
            Color blueC = Color.FromName("Blue");
            Color cyanC = Color.FromName("Cyan");
            Color greenC = Color.FromName("Green");
            Color magentaC = Color.FromName("Magenta");
            Color redC = Color.FromName("Red");
            Color whiteC = Color.FromName("White");
            Color yellowC = Color.FromName("Yellow");
            #endregion

            /*Loop through all the pixels of the output and find what colours there are present in the CMYK images,
             since that will determine what colours of the output will be.*/
            for (int i = 0; i < outHeight; i++)
            {

                for (int j = 0; j < outWidth; j++)
                {
                    /*If the black image have an active pixel the other images need not be read at all 
                     since you can not get a colour darker than black.*/
                    if (black.GetPixel(i + 1, j + 1))
                        this.composedImage.SetPixel(i, j, blackC);
                    else
                    {
                        bool cI = cyan.GetPixel(i + 1, j + 1);
                        bool mI = magenta.GetPixel(i + 1, j + 1);
                        bool yI = yellow.GetPixel(i + 1, j + 1);

                        //Choose the primary colour based on which colours(ink) are present in the CMY images.
                        if (cI && mI && yI)
                            this.composedImage.SetPixel(i, j, blackC);         //All the inks makes black.
                        else if (!cI && !mI && !yI)
                            this.composedImage.SetPixel(i, j, whiteC);         //No ink result in substrate colour(paper) which is usually white.
                        else if (cI && !mI && !yI)
                            this.composedImage.SetPixel(i, j, cyanC);
                        else if (!cI && mI && !yI)
                            this.composedImage.SetPixel(i, j, magentaC);
                        else if (!cI && !mI && yI)
                            this.composedImage.SetPixel(i, j, yellowC);
                        else if (!cI && mI && yI)
                            this.composedImage.SetPixel(i, j, redC);           //Magenta and yellow ink makes a red colour.
                        else if (cI && !mI && yI)
                            this.composedImage.SetPixel(i, j, greenC);         //Cyan and yellow make a green colour.
                        else if (cI && mI && !yI)
                            this.composedImage.SetPixel(i, j, blueC);          //Cyan and magenta make a blue colour.
                        
                    }
                }
            }

            graph.Dispose();

            return this;
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

            //Loops through all the pixels and counts the black ones.
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