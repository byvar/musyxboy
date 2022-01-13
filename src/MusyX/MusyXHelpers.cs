using BinarySerializer;
using BinarySerializer.Audio;
using BinarySerializer.Audio.RIFF;
using BinarySerializer.GBA.Audio.MusyX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {
	public static class MusyXHelpers {

        public static bool ByteArrayToFile(string fileName, byte[] byteArray) {
            if (byteArray == null)
                return false;

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));

                using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                fs.Write(byteArray, 0, byteArray.Length);
                return true;
            } catch (Exception ex) {
                Console.WriteLine("Exception caught in process: {0}", ex);
                return false;
            }
        }

        public static void ExportSampleSigned(string basePath, string directory, string filename, sbyte[] data, uint sampleRate, ushort channels) {
            // Create the directory
            Directory.CreateDirectory(directory);

            byte[] unsignedData = data.Select(b => (byte)(b + 128)).ToArray();

            // Create WAV data
            var wav = new WAV();
            var fmt = wav.Format;
            fmt.FormatType = 1;
            fmt.ChannelCount = channels;
            fmt.SampleRate = sampleRate;
            fmt.BitsPerSample = 8;
            wav.Data.Data = unsignedData;

            fmt.ByteRate = (fmt.SampleRate * fmt.BitsPerSample * fmt.ChannelCount) / 8;
            fmt.BlockAlign = (ushort)((fmt.BitsPerSample * fmt.ChannelCount) / 8);

            // Get the output path
            var outputFilePath = Path.Combine(directory, filename + ".wav");

            // Create and open the output file
            using (var outputStream = File.Create(outputFilePath)) {
                // Create a context
                using (var wavContext = new Context(basePath, log: false)) {
                    // Create a key
                    const string wavKey = "wav";

                    // Add the file to the context
                    wavContext.AddFile(new StreamFile(wavContext, wavKey, outputStream));

                    // Write the data
                    FileFactory.Write<WAV>(wavContext, wavKey, wav);
                }
            }
        }


        public static void ExportMusyX(string basePath, string mainDirectory, MusyX_File musyxFile) {
            Directory.CreateDirectory(mainDirectory);


            string outPath = Path.Combine(mainDirectory, "Sounds");
            for (int i = 0; i < (musyxFile.SampleTable?.Value?.Samples?.Length ?? 0); i++) {
                var e = musyxFile.SampleTable.Value.Samples[i].Value;
                //Util.ByteArrayToFile(outPath + $"{i}_{e.Offset.AbsoluteOffset:X8}.bin", e.SampleData);
                ExportSampleSigned(basePath, outPath, $"{i}_{musyxFile.SampleTable.Value.Samples[i].PointerValue.SerializedOffset:X8}", e.SampleData, e.SampleRate, 1);
            }
            outPath = Path.Combine(mainDirectory, "SongData");
            for (int i = 0; i < (musyxFile.SongTable?.Value?.Length ?? 0); i++) {
                var songBytes = musyxFile.SongTable.Value.Songs[i].SongBytes;
                ByteArrayToFile(Path.Combine(outPath, $"{i}_{musyxFile.SongTable.Value.SongPointers[i].SerializedOffset:X8}.son"), songBytes);
            }
            outPath = Path.Combine(mainDirectory, "InstrumentData");
            for (int i = 0; i < (musyxFile.InstrumentTable?.Value?.Macros?.Length ?? 0); i++) {
                var instrumentBytes = musyxFile.InstrumentTable.Value.Macros[i]?.Value?.InstrumentBytes;
                ByteArrayToFile(Path.Combine(outPath, $"{i}_{musyxFile.InstrumentTable.Value.Macros[i].PointerValue.SerializedOffset:X8}.bin"), instrumentBytes);
            }
            outPath = Path.Combine(mainDirectory, "SongMidi");
            for (int i = 0; i < (musyxFile.SongTable?.Value?.Length ?? 0); i++) {
                MusyX_MidiConverter conv = new MusyX_MidiConverter(musyxFile.SongTable.Value.Songs[i].Song);
                conv.Write(Path.Combine(outPath, $"{i}_{musyxFile.SongTable.Value.SongPointers[i].SerializedOffset:X8}.mid"));
            }
        }

    }
}
