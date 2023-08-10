using BinarySerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {
    public class Context : BinarySerializer.Context {
        public Context(string basePath, bool log, bool verbose = true) : base(
            basePath: basePath, // Pass in the base path
            settings: new SerializerSettings(), // Pass in the settings
            serializerLogger: log ? new SimpleSerializerLogger() : null, // Use serializer logger for logging to a file
            fileManager: new CustomFileManager(),
            systemLogger: verbose ? new ConsoleLogger() : null) // Use console logger
        { }

        public class CustomFileManager : IFileManager {
            public PathSeparatorChar SeparatorCharacter => PathSeparatorChar.ForwardSlash;

            public bool DirectoryExists(string path) => Directory.Exists(path);
            public bool FileExists(string path) => File.Exists(path);

            public Stream GetFileReadStream(string path) => new MemoryStream(File.ReadAllBytes(path));
            public Stream GetFileWriteStream(string path, bool recreateOnWrite = true) => recreateOnWrite ? File.Create(path) : File.OpenWrite(path);

            public Task FillCacheForReadAsync(long length, Reader reader) => Task.CompletedTask;
        }

        public class SerializerSettings : ISerializerSettings {
            /// <summary>
            /// The default string encoding to use when none is specified
            /// </summary>
            public Encoding DefaultStringEncoding => Encoding.GetEncoding(1252);

            /// <summary>
            /// Indicates if a backup file should be created when writing to a file
            /// </summary>
            public bool CreateBackupOnWrite => false;

            /// <summary>
            /// Indicates if pointers should be saved in the Memory Map for relocation
            /// </summary>
            public bool SavePointersForRelocation => false;

            /// <summary>
            /// Indicates if caching read objects should be ignored
            /// </summary>
            public bool IgnoreCacheOnRead => false;

            /// <summary>
            /// The pointer size to use when logging a <see cref="Pointer"/>. Set to <see langword="null"/> to dynamically determine the appropriate size.
            /// </summary>
            public PointerSize? LoggingPointerSize => PointerSize.Pointer32;

			public Endian DefaultEndianness => Endian.Little;

			// TODO: Set to true when debugging?
			public bool LogAlignIfNotNull => false;

			public bool AutoInitReadMap => false;

			public bool AutoExportReadMap => false;
		}

        public class ConsoleLogger : ISystemLogger {
			public void Log(LogLevel logLevel, object log, params object[] args) {
				switch (logLevel) {
					case LogLevel.Error:
						Console.WriteLine(log?.ToString() ?? String.Empty);
						break;
					case LogLevel.Warning:
						Console.WriteLine(log?.ToString() ?? String.Empty);
						break;
					case LogLevel.Info:
						Console.WriteLine(log?.ToString() ?? String.Empty);
						break;
				}
			}
		}

        public class SimpleSerializerLogger : ISerializerLogger {
            public bool IsEnabled => !string.IsNullOrEmpty(LogFile);

            private StreamWriter _logWriter;

            protected StreamWriter LogWriter => _logWriter ??= GetFile();

            public string OverrideLogPath { get; set; }
            public string LogFile => OverrideLogPath;
            public int BufferSize => 0x8000000; // 1 GB

            public StreamWriter GetFile() {
                return new StreamWriter(File.Open(LogFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8, BufferSize);
            }

            public void Log(object obj) {
                if (IsEnabled)
                    LogWriter.WriteLine(obj != null ? obj.ToString() : "");
            }

            public void Dispose() {
                _logWriter?.Dispose();
                _logWriter = null;
            }
        }
    }
}
