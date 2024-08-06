using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

namespace Mcasaenk {
    public interface SaveInterface {
        void SaveImage(string loc, BitmapSource img);
        void SaveLines(string loc, IEnumerable<string> lines);
    }

    public interface ReadInterface {
        bool Exists(string path);
        string AllEntries();

        string ReadAllText(string path);
        IEnumerable<string> ReadAllLines(string path);
        BitmapSource ReadBitmap(string path);
     

        string[] GetFiles(string path);


        public static ReadInterface GetSuitable(string path) {
            if(!Path.Exists(path)) return null;
            if(path.EndsWith(".zip") || path.EndsWith(".jar")) return new ZipRead(path);
            else if(!File.Exists(path)) return new FileRead(path);
            else return null;
        }
    }

    public class FileRead : ReadInterface {
        private readonly string baselocation;
        private readonly string concatentries;
        public FileRead(string baselocation) {
            this.baselocation = baselocation;
            this.concatentries = string.Join(Environment.NewLine, Directory.GetFiles(baselocation, "*.*", SearchOption.AllDirectories).Select(f => f.Substring(Path.GetFullPath(baselocation).Length + 1).Replace("\\", "/")));
        }

        public bool Exists(string path) => File.Exists(Path.Combine(baselocation, path));
        public string AllEntries() => concatentries;

        public string ReadAllText(string path) => File.ReadAllText(Path.Combine(baselocation, path));
        public IEnumerable<string> ReadAllLines(string path) => File.ReadLines(Path.Combine(baselocation, path));
        public BitmapSource ReadBitmap(string path) {
            BitmapImage bitmap = new BitmapImage();

            // Initialize the BitmapImage from the file path
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.Combine(baselocation, path), UriKind.RelativeOrAbsolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Optional: to cache the image
            bitmap.EndInit();

            // Optionally, freeze the bitmap to make it cross-thread accessible
            bitmap.Freeze();

            return bitmap;
        }

        public string[] GetFiles(string path) => Directory.GetFiles(Path.Combine(baselocation, path));
    }

    public class FileSave : SaveInterface {
        private readonly string baselocation;
        public FileSave(string baselocation, bool clear_beforehand) {
            this.baselocation = baselocation;
            var di = Directory.CreateDirectory(baselocation);
            if(clear_beforehand) {
                foreach(FileInfo file in di.GetFiles()) {
                    file.Delete();
                }
                foreach(DirectoryInfo dir in di.GetDirectories()) {
                    dir.Delete(true);
                }
            }
        }

        public void SaveImage(string loc, BitmapSource img) {
            //img.Save(Path.Combine(baselocation, loc));
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img));

            using(FileStream fileStream = new FileStream(loc, FileMode.Create)) {
                encoder.Save(fileStream);
            }
        } 
        public void SaveLines(string loc, IEnumerable<string> lines) => File.WriteAllLines(Path.Combine(baselocation, loc), lines);
    }

    public class ZipRead : ReadInterface {
        private readonly ZipArchive archive;
        private readonly Dictionary<string, ZipArchiveEntry> entries;
        private readonly string concatentries;
        public ZipRead(string baselocation) {
            this.archive = ZipFile.Open(baselocation, ZipArchiveMode.Read);
            this.entries = archive.Entries.ToDictionary(entr => entr.FullName);
            this.concatentries = string.Join(Environment.NewLine, archive.Entries);
        }

        public bool Exists(string path) => entries.ContainsKey(path);
        public string AllEntries() => concatentries;

        public string ReadAllText(string path) {
            using var stream = entries[path].Open();
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
        public IEnumerable<string> ReadAllLines(string path) => this.ReadAllText(path).Split(Environment.NewLine);
        public BitmapSource ReadBitmap(string path) {
            using var stream = entries[path].Open();
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        }

        public string[] GetFiles(string path) { 
            return entries.Keys.Where(ek => ek.StartsWith(path)).ToArray();
        }
    }

    public class ZipSave : SaveInterface, IDisposable {
        private readonly FileStream _fileStream;
        private readonly ZipArchive _archive;
        public ZipSave(string ziplocation) {
            this._fileStream = new FileStream(ziplocation, FileMode.Create);
            this._archive = new ZipArchive(_fileStream, ZipArchiveMode.Create);
        }
        public void Dispose() {
            _archive.Dispose();
            _fileStream.Dispose();
        }



        public void SaveImage(string loc, BitmapSource img) => SaveFile(loc, ImageToByte2(img));
        public void SaveLines(string loc, IEnumerable<string> lines) => SaveFile(loc, Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)));



        public void SaveFile(string loc, byte[] content) {
            using var w = _archive.CreateEntry(loc, CompressionLevel.NoCompression).Open();
            w.Write(content, 0, content.Length);
        }
        private static byte[] ImageToByte2(BitmapSource img) {
            using var stream = new MemoryStream();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img));
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
