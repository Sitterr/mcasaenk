using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils {

    public interface SaveInterface {
        void SaveImage(string loc, Image img);
        void SaveLines(string loc, IEnumerable<string> lines);
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

        public void SaveImage(string loc, Image img) => img.Save(Path.Combine(baselocation, loc));
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



        public void SaveImage(string loc, Image img) => SaveFile(loc, ImageToByte2(img));
        public void SaveLines(string loc, IEnumerable<string> lines) => SaveFile(loc, Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)));



        public void SaveFile(string loc, byte[] content) {
            using var w = _archive.CreateEntry(loc, CompressionLevel.NoCompression).Open();
            w.Write(content, 0, content.Length);
        }
        private static byte[] ImageToByte2(Image img) {
            using var stream = new MemoryStream();
            img.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }
}
