using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Mcasaenk {
    public interface SaveInterface {
        void SaveImage(string loc, BitmapSource img);
        void SaveLines(string loc, IEnumerable<string> lines);
    }

    public interface ReadInterface {
        bool Exists(string path);
        IEnumerable<string> ReadAllLines(string path);
        string[] GetFiles(string path, string pattern = "");

        BitmapSource ReadBitmap(string path);
    }

    public class FileRead : ReadInterface {
        public bool Exists(string path) => File.Exists(path);
        public IEnumerable<string> ReadAllLines(string path) => File.ReadLines(path);
        public string[] GetFiles(string path, string pattern = "") => Directory.GetFiles(path, pattern);

        public BitmapSource ReadBitmap(string path) {
            BitmapImage bitmap = new BitmapImage();

            // Initialize the BitmapImage from the file path
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Optional: to cache the image
            bitmap.EndInit();

            // Optionally, freeze the bitmap to make it cross-thread accessible
            bitmap.Freeze();

            return bitmap;
        }
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
