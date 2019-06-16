using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Configuration;
using System.Xml;
using System.Xml.Linq;
using BitMiracle.LibTiff.Classic;
using BarebonesImageLibrary;

namespace OAResize
{
    /// <summary>
    /// OAR uses a few differnt folders to handle different files. They are stored in this class.
    /// </summary>
    internal class DirPaths
    {
        /* The three folders that the program moves files between.
         * source is input where you drop of 1bpp .TIFs.
         * middle is just for files that are currently being processed,
         * freeing up the input folder for another file.
         * source is the CTP(or any form of output).*/
        internal string source;
        internal string middle;
        internal string target;

        //Folder for logging.
        internal string Log;

        /* The program uses small 1bpp .TIFs that are written onto the image being processed, 
         * these images are saved in this folder.*/
        internal string regMarks;

        /* File that contains the information about how to process the images,
         * which is determined by how the situation looks in the press.*/
        internal string pressConfig;

        //Constructor sets the paths to those from the config file.
        internal DirPaths(ReadConfig readConfig)
        {
            source = readConfig.ReadString("source");
            middle = readConfig.ReadString("middle");
            target = readConfig.ReadString("target");

            Log = readConfig.ReadString("Log");

            regMarks = readConfig.ReadString("regMarks");

            pressConfig = System.AppDomain.CurrentDomain.BaseDirectory;
            pressConfig = Path.Combine(pressConfig, readConfig.ReadString("pressConfig"));
        }

        /// <summary>
        /// Checks that the paths exist and returns true if they do.
        /// <para>Otherwise writes an error to log and returns false.</para> 
        /// <returns>True if all paths are present false otherwise it returns error to log.</returns>
        /// <param name="logg">The logging function.</param>
        /// </summary>
        internal bool ValidatePaths(Action<string> logg)
        {
            List<string> errors = new List<string>();

            foreach (var path in (new string[] { this.source, this.middle, this.target, this.Log }))
            {
                if (!Directory.Exists(path))
                {
                    errors.Add("ERROR - " + DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - Directory " + path + " does not exist.");
                }
            }

            if (errors.Count > 0)
            {
                logg(string.Join(Environment.NewLine, errors));
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Moves files from the source to the middle 
    /// and from the middle to the target.
    /// </summary>
    internal class MoveFile
    {
        /// <summary>
        /// Moves a file from source and middle to the next folder as long as the next folder is empty and there's only one in the first.
        /// </summary>
        /// <param name="folderPath">The folder to move the file from.</param>
        /// <param name="logg">The logging function.</param>        
        /// <param name="dirPaths">Where the folders of the programs are located.</param>
        /// <returns>Name of the file that was moved or null if none was.</returns>
        internal string FromDir(string folderPath, Action<string> logg, DirPaths dirPaths)
        {
            //Moves any buffered file to the target so its journey may continue.
            string[] allFilesInDir = Directory.GetFiles(folderPath, "*.TIF");

            if (allFilesInDir.Length > 1)
            {
                logg("Warning - " + DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - More than one .TIF present in " + folderPath);
                Console.WriteLine(" Remove the files and press any key.");
                Console.ReadKey();
            }
            else if (allFilesInDir.Length == 0)
            {
                //If there are no files in the folder the program does nothing.
            }
            else
            {

                string fileName = Path.GetFileName(allFilesInDir[0]);

                //Files are moved differently from middle and from source.
                if (folderPath == dirPaths.source)
                {
                    //Check that there are no .TIF files in the dirPaths.target, only one file should be sent to the CTP at a time.
                    string[] allFilesInMiddle = Directory.GetFiles(dirPaths.middle, "*.TIF");

                    if (allFilesInMiddle.Length == 0)
                    {
                        //Get the full filenames for both files.
                        string sourceFile = Path.Combine(dirPaths.source, fileName);
                        string middleFile = Path.Combine(dirPaths.middle, fileName);

                        //Wait a bit before trying to move the file to avoid any IO problems.
                        System.Threading.Thread.Sleep(100);
                        File.Move(sourceFile, middleFile);

                        Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - " + allFilesInDir[0] + " moved to buffer.");

                        return fileName;
                    }

                    return null;
                }
                else if (folderPath == dirPaths.middle)
                {
                    //Check that there are no .TIF files in the dirPaths.target, only one file should be sent to the CTP at a time.
                    string[] allFilesInTarget = Directory.GetFiles(dirPaths.target, "*.TIF");

                    if (allFilesInTarget.Length == 0)
                    {
                        //Get the full filenames for both files.
                        string middleFile = Path.Combine(dirPaths.middle, fileName);
                        string destFile = Path.Combine(dirPaths.target, fileName);

                        //Wait a bit before trying to move the file to avoid any IO problems.
                        System.Threading.Thread.Sleep(100);

                        //Move the file to a .tmp file and then "move" it again to rename it. The CTP wants it thus.
                        File.Move(middleFile, destFile + @".tmp");
                        File.Move(destFile + @".tmp", destFile);

                        Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - " + allFilesInDir[0] + " moved to output.");

                        return fileName;
                    }

                    return null;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The program logs errors in text files that are unique per day.
    /// </summary>
    internal class Log
    {
        /// <summary>
        /// Writes text to a logg file
        /// <para>The logfile is called YYYMMDD_OARlog.txt, so a new one is created for every day.</para>
        /// </summary>
        /// <param name="textToLog">The text that should be logged.</param>
        /// <param name="dirPaths">The paths the program uses.</param>
        /// <returns>True upon completion of logging.</returns>
        internal bool Text(string textToLog, DirPaths dirPaths)
        {
            string logFile;

            logFile = Path.Combine(dirPaths.Log, DateTime.Now.ToString("yyyyMMdd"));
            logFile = string.Concat(logFile, @"_OARlog.txt");

            //Checks if there exists a file for todays date, otherwise it creats it.
            if (File.Exists(logFile))
            {
                Console.WriteLine(textToLog);

                using (StreamWriter writeString = File.AppendText(logFile))
                {
                    writeString.WriteLine(textToLog);
                }
            }

            else
            {
                Console.WriteLine(textToLog);

                using (StreamWriter writeString = File.CreateText(logFile))
                {
                    writeString.WriteLine(textToLog);
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Many of the variables in the program can be modified using a configure file called OARConfig.txt.
    /// This class handles the reading of this configure file.
    /// </summary>
    internal class LoadConfig
    {
        /// <summary>
        /// Function is called to read a string from OARConfig.txt
        /// </summary>
        /// <param name="variableToConfig">The parameter to get from the config file.</param>
        /// <returns>The string after "=".</returns>
        private string Read(string variableToConfig)
        {
            //The path where the OARConfig is located
            string pathFile = System.AppDomain.CurrentDomain.BaseDirectory;
            pathFile = string.Concat(pathFile, "OARConfig.txt");

            //The config file must exist if it doesn't the program wont work.
            if (!File.Exists(pathFile))
            {
                Console.WriteLine(pathFile + "OARConfig.txt file does not exist. Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            /*Reads all the lines in the config file and looks for the inputed string,
             * it looks at the first "word" separated by a space(tab or " ").
             * When found it will look for an "=" and try to convert the string after that to an int.*/
            using (StreamReader readString = new StreamReader(pathFile))
            {
                while (readString.Peek() >= 0)
                {
                    string tempLine = readString.ReadLine();
                    if (tempLine.Split('\t', ' ').First().Equals(variableToConfig))
                    {
                        return tempLine.Substring(tempLine.IndexOf(@"=") + 1);
                    }
                }
            }

            Console.WriteLine("ERROR " + variableToConfig + " Not found in config file. Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(0);

            //Very smooth return value.
            return "ERRORS abound, this one shouldn't have happend at all. The program should have quit at the line above.";
        }

        /// <summary>
        /// Called to read a number variable from the config file,
        /// such as a time, coordinates, scaling factors and parsing information.
        /// </summary>
        /// <param name="variableToConfig">The variable to be read.</param>
        /// <returns>Time in milliseconds, or x,y-coordinates.</returns>
        internal List<int> ReadNumber(string variableToConfig)
        {
            List<int> parameters = new List<int>();

            //Gets the string associated with the variable to be configured.
            variableToConfig = this.Read(variableToConfig);

            //Seperates the values in the case of multiple ones such as x and y coordinates.
            string[] stringParameters = variableToConfig.Split(',');

            //Goes through all the parameters and add them to the list.
            foreach (string stringParameter in stringParameters)
            {
                if (Int32.TryParse(stringParameter, out int tempInt))
                {
                    parameters.Add(tempInt);
                }
                else
                {
                    Console.WriteLine(variableToConfig + " could not be parsed. Press any key to exit.");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            }

            return parameters;
        }

        /// <summary>
        /// Called to read a string parameter from the config file. Such as paths, file names and zone names.
        /// Since zone names can be any number of parameters this is returned as a list.
        /// </summary>
        /// <param name="variableToConfig">The variable get the parameter values for.</param>
        /// <returns>A list of parameters associated with the input variable in the config file.</returns>
        internal List<string> ReadString(string variableToConfig)
        {
            List<string> parameters = new List<string>();

            variableToConfig = this.Read(variableToConfig);

            //Parameters are seperated by "," in the config file so it split up on this basis.
            string[] tempParameters = variableToConfig.Split(',');

            foreach (string tempParameter in tempParameters)
            {

                parameters.Add(tempParameter);

            }

            return parameters;
        }
    }

    /// <summary>
    /// Reads the XML config file, PressConfig.xml, which contains values specific to the press. 
    /// Such as fanout per tower and how much the plate-locks are offset.
    /// </summary>
    internal class ReadPressConfigXML
    {
        /// <summary>
        /// Gets the values for fanOut and rollPosition.
        /// </summary>
        /// <param name="Tower">Which tower the plate is in.</param>
        /// <param name="ElementInTower">"fanOut" or "rollPosition"</param>
        /// <param name="dirPaths">Where the folders of the programs are located.</param>
        /// <returns>Value of the specified element.</returns>
        internal string GetValue(string Tower, string ElementInTower, DirPaths dirPaths)
        {
            Console.WriteLine(dirPaths.pressConfig);
            XDocument doc = XDocument.Load(dirPaths.pressConfig);
            IEnumerable<XElement> childList =
                from x in doc.Root.Elements(Tower).Elements(ElementInTower)
                select x;

            return childList.First().FirstAttribute.Value;
        }

    }

    /// <summary>
    /// Many of the variables in the program can be modified using a configure file called OAResize.exe.config.
    /// This class handles the reading of this configure file.
    /// </summary>
    internal class ReadConfig
    {
        /// <summary>
        /// Reads a string from OAResize.exe.config.
        /// </summary>
        /// <param name="key">The key of the variable, usually same as the name of the variable.</param>
        /// <returns>The value that the variable should have.</returns>
        internal string ReadString(string key)
        {
            //The path where the Config file is located
            string pathFile = System.AppDomain.CurrentDomain.BaseDirectory;
            pathFile = string.Concat(pathFile, "OAResize.exe.config");

            //The config file must exist if it doesn't the program wont work.
            if (!File.Exists(pathFile))
            {
                Console.WriteLine(pathFile + "OAResize.exe.config file does not exist. Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            var appSettings = ConfigurationManager.AppSettings;
            return appSettings[key];
        }

        /// <summary>
        /// Reads a number from OAResize.exe.config.
        /// </summary>
        /// <param name="key">The key of the variable, usually same as the name of the variable.</param>
        /// <returns>The value that the variable should have.</returns>
        internal ushort ReadNumber(string key)
        {
            string readValueAsString = this.ReadString(key);

            if (ushort.TryParse(readValueAsString, out ushort tempInt))
            {
                return tempInt;
            }
            else
            {
                Console.WriteLine(key + " could not be parsed. Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            return 0; //Should not happen.
        }
    }

    /// <summary>
    /// The way a file will be processed is determined by this class.
    /// </summary>
    internal class ZoneCylinderProcess
    {
        public int Scale { get; }
        public string MoveThisWay { get; }
        public string Name { get; }

        /// <summary>
        /// A class of this type is created for a zoneCylinder which is the input which becomes the name.
        /// The other variables are read from OARconfig.txt
        /// </summary>
        /// <param name="zoneCylinder">This will be searched for in inputed images file name.</param>
        /// <param name="folderPath">The folder to move the file from.</param>     
        /// <param name="dirPaths">Where the folders of the programs are located.</param>
        internal ZoneCylinderProcess(string zoneCylinder, LoadConfig loadConfig, DirPaths dirPaths)
        {
            Name = zoneCylinder;
            List<string> parametersOfProcess = loadConfig.ReadString(zoneCylinder);

            if (Int32.TryParse(parametersOfProcess.First(), out int tempInt))
            {
                Scale = tempInt;
            }
            else
            {
                Console.WriteLine(parametersOfProcess.First() + " could not be parsed. Should be a positive integer for the scale factor. Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(0);
            }
            parametersOfProcess.RemoveAt(0);
            MoveThisWay = parametersOfProcess.First();
        }

    }

    /// <summary>
    /// Contains the information of which characters in the filename that will determine how
    /// the file will be processed. 
    /// Both of the variables are loaded form OARConfig.txt.
    /// "start" is the character where the parsing begins, counting from one.
    /// (The number in OARconfig.txt is therefore reduced by one when loaded into the program 
    /// so that it will work with C# where indexes start at 0)
    /// "length" is the number of characters that will be parsed.
    /// </summary>
    internal struct ParsingInformation
    {
        internal ushort towerStart;
        internal ushort towerLength;
        internal ushort cylinderStart;
        internal ushort cylinderLength;
        internal ushort sectionStart;
        internal ushort sectionLength;
        internal ushort halfStart;
        internal ushort halfLength;
    }

    /// <summary>
    /// The program goes through three phases, connected to it's three folders source, middle and target.
    /// </summary>
    internal class Phase
    {
        /// <summary>
        /// Does input things and validates the paths.
        /// </summary>
        /// <param name="logg">The logging function.</param>        
        /// <param name="dirPaths">Where the folders of the programs are located.</param>
        /// <param name="moveFile">Moves file from the source to the middle.</param>
        /// <returns>The name of the file if a file was moved, null otherwise.</returns>
        internal string Input(Action<string> logg, DirPaths dirPaths, MoveFile moveFile)
        {
            #region Validation of the maps
            /*The output map is often a mapped network drive,
               so if the map doesn't validate we continue to try at regular intervals
               as the drive might just be offline temporarily.*/
            int i = 0;
            while (!dirPaths.ValidatePaths(logg))
            {
                int validateWaitTime;

                if (i < 10)
                    validateWaitTime = 1000;
                else if (i < 100)
                    validateWaitTime = 10000;
                else
                    validateWaitTime = 600000;

                Console.WriteLine("Attempting to validate paths again in " + validateWaitTime / 1000 + " seconds");

                System.Threading.Thread.Sleep(validateWaitTime);

                i++;
            }
            if (i > 0)
                Console.WriteLine("Validated");
            #endregion

            return moveFile.FromDir(dirPaths.source, logg, dirPaths);
        }

        /// <summary>
        /// Changes a.TIF file differently depending on which group it belongs to
        /// based on a "zoneCylinder" code in its file name(as specified in OARconfig.txt).
        /// </summary>
        /// <param name = "fileToProcess" > The filename of the.TIF file to process, path not included.</param>
        /// <returns>True upon completion.</returns>
        internal bool Process(string fileTo, ParsingInformation parsingInfo, DirPaths dirPaths, LoadConfig loadConfig)
        {
            BarebonesImage processImage = new BarebonesImage();
            BarebonesImage resizedImage = new BarebonesImage();
            ReadPressConfigXML readPressConfigXML = new ReadPressConfigXML();

            Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - Processing " + fileTo);

            string tower = fileTo.Substring(parsingInfo.towerStart, parsingInfo.towerLength);
            string cylinder = fileTo.Substring(parsingInfo.cylinderStart, parsingInfo.cylinderLength);
            string section = fileTo.Substring(parsingInfo.sectionStart, parsingInfo.sectionLength);
            string half = fileTo.Substring(parsingInfo.halfStart, parsingInfo.halfLength);

            string rollPosition = readPressConfigXML.GetValue(tower, "rollPosition", dirPaths);
            string fanOut = readPressConfigXML.GetValue(tower, "fanOut", dirPaths);

            //Check if the zoneCylinder specified by the file name is one that will be processed.
            int index = -1; // Processes.FindIndex(zC => zC.Name == fileTo.Substring(parsingInfo.start, parsingInfo.length));

            //If the zoneCylinder in the filename isn't in the Processes list, just return and the file will be outputed as is.
            if (index == -1)
            {
                Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - Processing of " + fileTo + " complete.");
                return true;
            }

            string pathAndFileTo = Path.Combine(dirPaths.middle, fileTo);

            //Load the image that was moved from the source folder.
            processImage = processImage.ReadATIFF(pathAndFileTo);

            int originalWidth = processImage.Width;
            int originalHeight = processImage.Height;
            int originalWidthWithPad = processImage.WidthWithPad;

            //Scales the image to the scale of the blue zone.
            resizedImage = processImage.DownsizeHeight(Processes[index].Scale);

            //Outputs the resized width and height so you easyily can see changes you make to the "Scale" factor in OARconfig.txt
            Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - " + fileTo + " resized to: " + resizedImage.Width + " * " + resizedImage.Height);

            /* The resized image size needs to be changed back to the original size
             * so the end up in the correct place on the printing plate.
             * This is done by inserting its bytestream into an empty bytestream of the original size*/
            byte[] tempImageBytestream = new byte[originalHeight * originalWidthWithPad / 8];

            /* The image will be padded at different place depending on where in the machine the plate will go, 
             * this is determinied by it's zoneCylinder code wich is added to the file name and specified in OARConfig.txt*/
            switch (Processes[index].MoveThisWay)
            {
                case "up":
                    //The resized image byte stream is inserted into the start of the temporary stream, causing it to end up at the top of the bigger picture.
                    Array.Copy(resizedImage.ImageByteStream, 0, tempImageBytestream, 0, resizedImage.ImageByteStream.Length);
                    break;
                case "down":
                    //The stream is inserted into the difference of the two streams so it ends up in the bottom.
                    Array.Copy(resizedImage.ImageByteStream, 0, tempImageBytestream, (originalHeight - resizedImage.Height) * originalWidthWithPad / 8, resizedImage.ImageByteStream.Length);
                    break;
                default:
                    //The default makes the image end up in the middle.
                    Array.Copy(resizedImage.ImageByteStream, 0, tempImageBytestream, (originalHeight - resizedImage.Height) * originalWidthWithPad / 16, resizedImage.ImageByteStream.Length);
                    break;
            }

            //The resized have been padded with so it's the size of the original once again. 
            resizedImage.Height = originalHeight;
            resizedImage.Width = originalWidth;
            resizedImage.WidthWithPad = originalWidthWithPad;

            //The padded bytestream is inserted to the resized image.
            resizedImage.ImageByteStream = new byte[resizedImage.Height * resizedImage.WidthWithPad / 8];
            resizedImage.ImageByteStream = tempImageBytestream;

            //Saves the result of the above processing.
            resizedImage.SaveAsTIFF(pathAndFileTo);

            Console.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " - Processing of " + fileTo + " complete.");

            return true;
        }

        /// <summary>
        /// Does output stuff which is just moving a file from the middle to the target.
        /// </summary>
        /// <param name="logg">The logging function.</param>        
        /// <param name="dirPaths">Where the folders of the programs are located.</param>
        /// <param name="moveFile">Moves file from the source to the middle.</param>
        /// <returns>The name of the file if a file was moved, null otherwise.</returns>
        internal string Output(Action<string> logg, DirPaths dirPaths, MoveFile moveFile)
        {
            return moveFile.FromDir(dirPaths.middle, logg, dirPaths);
        }
    }

    /// <summary>
    /// Main class that only contains the main() method which does the stuff.
    /// </summary>
    class Resizer
    {
        static void Main()
        {
            #region Instantiation of classes, structs and stuff.
            LoadConfig loadConfig = new LoadConfig();
            Log Logg = new Log();
            ReadConfig readConfig = new ReadConfig();
            DirPaths dirPaths = new DirPaths(readConfig);
            MoveFile fileMove = new MoveFile();
            Phase phase = new Phase();
            ParsingInformation parsingInfo = new ParsingInformation();
            Action<string> logg = (str) => Logg.Text(str, dirPaths);
            #endregion

            #region Load a bunch of parameters from the config file.

            //Load the sleepTime from the config file.
            int sleepTime = loadConfig.ReadNumber("sleepTime").First();

            //Load parsing information for the files names from the config file.
            parsingInfo.towerStart = readConfig.ReadNumber("parseTowerStart");
            parsingInfo.towerStart -= 1;                                                         //C# starts to count at zero.
            parsingInfo.towerLength = readConfig.ReadNumber("parseTowerLength");

            parsingInfo.cylinderStart = readConfig.ReadNumber("parseCylinderStart");
            parsingInfo.cylinderStart -= 1;
            parsingInfo.cylinderLength = readConfig.ReadNumber("parseCylinderLength");

            parsingInfo.sectionStart = readConfig.ReadNumber("parseSectionStart");
            parsingInfo.sectionStart -= 1;
            parsingInfo.sectionLength = readConfig.ReadNumber("parseSectionLength");

            parsingInfo.halfStart = readConfig.ReadNumber("parseHalfStart");
            parsingInfo.halfStart -= 1;
            parsingInfo.halfLength = readConfig.ReadNumber("parseHalfLength");
            #endregion



            //Forever loop for now. Main loop of the program.
            string toExitOrNot = @"Never Exit";
            do
            {
                //Checks that all the folders are present and then moves any file from the source to middle.
                string fileToProcess = phase.Input(logg, dirPaths, fileMove);

                //If there is a file in the source folder the processing begins.
                if (fileToProcess != null)
                {

                    phase.Process(fileToProcess,parsingInfo, dirPaths, loadConfig);

                }

                //Any file in the middle should have by this time undergone processing and so is outputed.
                phase.Output(logg, dirPaths, fileMove);

                //Let the CPU get some rest. ("sleepTime" is set in OARConfig.txt)
                System.Threading.Thread.Sleep(sleepTime);

            } while (!toExitOrNot.Equals("Exit"));
        }
    }
}