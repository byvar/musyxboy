using BinarySerializer;
using BinarySerializer.Audio.GBA.MusyX;
using CommandLine;
using Konsole;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {

    class Program {
        public class Options {
            [Option('i', "input", Required = true, HelpText = "Input file to be processed.")]
            public string InputFile { get; set; }

            [Option('o', "output", Required = false, HelpText = "Directory to save files in. Set to the file's basename if not specified.")]
            public string OutputDirectory { get; set; }

            [Option('l', "log", Required = false, HelpText = "Directory to store serializer logs in. Logging disabled if not specified.")]
            public string LogDirectory { get; set; }

            /*[Option('x', "xmlog", Required = false, HelpText = "Directory to store XM logs in. Logging disabled if not specified.")]
            public string XMLogDirectory { get; set; }*/

            /*[Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }*/

            // TODO: Allow option for providing the address of a single MusyX file
        }


        static void Main(string[] args) {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o => {
                       ParseROM(o);
                   });
        }

        private const int progressSize = 50;
        private const int progressTextWidth = 40;

        private static void ParseROM(Options options) {
            //Settings.Verbose = options.Verbose;
            Settings.Log = !string.IsNullOrEmpty(options.LogDirectory);
            Settings.LogDirectory = options.LogDirectory;
            //Settings.XMLog = !string.IsNullOrEmpty(options.XMLogDirectory);
            //Settings.XMLogDirectory = options.XMLogDirectory;

            string fullPath = Path.GetFullPath(options.InputFile);
            string basePath = Path.GetDirectoryName(fullPath);
            string filename = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(options.OutputDirectory)) {
                options.OutputDirectory = Path.GetFileNameWithoutExtension(fullPath);
            }

            MusyX_Settings musyXSettings = null;
            Context.ConsoleLogger logger = new Context.ConsoleLogger();
            List<MusyX_File> musyxFiles = new List<MusyX_File>();

            using (Context context = new Context(basePath, log: false, verbose: false)) {
                context.AddFile(new MemoryMappedFile(context, filename, 0x08000000, Endian.Little));
                context.GetMusyXSettings().EnableErrorChecking = true;
                musyXSettings = context.GetMusyXSettings();
                Pointer basePtr = context.FilePointer(filename);
                BinaryDeserializer s = context.Deserializer;
                s.Goto(basePtr);

                // Scan ROM for pointers
                Dictionary<Pointer, List<int>> pointers = FindPointers(s, basePtr);

                FastScan(s, pointers, logger, musyxFiles);
            };

            if (musyxFiles.Count != 0) {
                if (Settings.Log) {
                    // Log song data
                    ProgressBar ProgressBarLog = new ProgressBar(progressSize, progressTextWidth);
                    Console.WriteLine();

                    // Create a separate log file for each song
                    for (int i = 0; i < musyxFiles.Count; i++) {
                        var song = musyxFiles[i];
                        ProgressBarLog.Refresh((int)((i / (float)musyxFiles.Count) * progressSize), $"Logging {i}/{musyxFiles.Count}: {song.Offset.StringAbsoluteOffset}");

                        using (Context context = new Context(basePath, log: Settings.Log, verbose: true)) {
                            context.SetMusyXSettings(musyXSettings);
                            Directory.CreateDirectory(Settings.LogDirectory);
                            ((Context.SimpleSerializerLogger)context.SerializerLogger).OverrideLogPath = Path.Combine(Settings.LogDirectory, $"{song.Offset.StringAbsoluteOffset}.txt");
                            context.AddFile(new MemoryMappedFile(context, filename, 0x08000000, Endian.Little));
                            var basePtr = context.FilePointer(filename);
                            var s = context.Deserializer;

                            // Re-read song. We could have just done this before,
                            // but we only want to log valid songs, so we do it after we've verified that it's valid
                            s.DoAt(song.Offset, () => {
                                MusyX_File File = s.SerializeObject<MusyX_File>(default, name: nameof(File));
                            });
                        };
                    }
                    ProgressBarLog.Refresh(progressSize, $"Logging: Finished");
                }


                // Convert songs
                ProgressBar ProgressBarConvert = new ProgressBar(progressSize, progressTextWidth);
                Console.WriteLine();

                for (int i = 0; i < musyxFiles.Count; i++) {
                    var musyxFile = musyxFiles[i];
                    ProgressBarConvert.Refresh((int)((i / (float)musyxFiles.Count) * progressSize), $"Converting {i}/{musyxFiles.Count}: {musyxFile.Offset.StringAbsoluteOffset}");
                    try {
                        MusyXHelpers.ExportMusyX(basePath, options.OutputDirectory, musyxFile);
                    } catch (Exception ex) {
                        logger.LogError(ex.ToString());
                    }
                }
                ProgressBarConvert.Refresh(progressSize, $"Converting: Finished");
            }
        }

        private static void FastScan(SerializerObject s, Dictionary<Pointer, List<int>> pointers, Context.ConsoleLogger logger, List<MusyX_File> musyxFiles) {
            ProgressBar ProgressBarGaxScan = new ProgressBar(progressSize, progressTextWidth);
            Console.WriteLine();

            int pointerIndex = 0;
            float pointersCountFloat = pointers.Count;
            var context = s.Context;

            foreach (Pointer p in pointers.Keys) {
                s.DoAt(p, () => {
                    context.Cache.Structs.Clear();
                    context.MemoryMap.ClearPointers();

                    try {
                        MusyX_File File = s.SerializeObject<MusyX_File>(default, name: nameof(File));
                        logger.LogInfo($"{File.Offset}: {File.SampleTable.Value.Samples.Length} Samples - {File.SongTable.Value?.Songs?.Length ?? 0} Songs");
                        musyxFiles.Add(File);
                    } catch {
                    }

                });

                pointerIndex++;

                if (pointerIndex % 16 == 0 || pointerIndex == pointers.Count)
                    ProgressBarGaxScan.Refresh((int)(pointerIndex / pointersCountFloat * progressSize), $"Scanning: {pointerIndex}/{pointers.Count}");
            }
            Console.WriteLine();
        }

        private static Dictionary<Pointer, List<int>> FindPointers(SerializerObject s, Pointer basePtr) {
            ProgressBar progressBarPointerScan = new ProgressBar(progressSize, progressTextWidth);
            Console.WriteLine();

            long len = s.CurrentLength - 4;
            float lenFloat = len;
            int curPtr = 0;

            // Find all pointers in the ROM and attempt to find the GAX structs from those
            var pointers = new Dictionary<Pointer, List<int>>();

            while (curPtr < len) {
                Pointer p = s.DoAt(basePtr + curPtr, () => s.SerializePointer(default, allowInvalid: true));

                if (p != null) {
                    if (!pointers.ContainsKey(p)) pointers[p] = new List<int>();
                    pointers[p].Add(curPtr);
                }

                curPtr += 4;

                if (curPtr % (1 << 16) == 0 || curPtr == len)
                    progressBarPointerScan.Refresh((int)((curPtr / lenFloat) * progressSize), $"Scanning pointers: {curPtr:X8}/{len:X8}");
            }

            return pointers;
        }
    }
}
