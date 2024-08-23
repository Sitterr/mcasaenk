using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using static Mcasaenk.Global;
using System.Collections.Frozen;

namespace Mcasaenk {
    public interface SaveInterface : IDisposable {
        void SaveImage(string loc, WPFBitmap img);
        void SaveLines(string loc, IEnumerable<string> lines);


        public static SaveInterface GetSuitable(string path) {
            if(path.EndsWith(".zip") || path.EndsWith(".jar")) return new ZipSave(path);
            else if(!File.Exists(path)) return new FileSave(path, true);
            else return null;
        }
    }

    public interface ReadInterface : IDisposable {
        string GetBasePath();
        bool ExistsFile(string path);
        bool ExistsFolder(string path);
        string AllEntries();

        string ReadAllText(string path);
        IEnumerable<string> ReadAllLines(string path);
        WPFBitmap ReadBitmap(string path);
     

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
            if(baselocation != "") this.concatentries = string.Join(Environment.NewLine, Directory.GetFiles(baselocation, "*.*", SearchOption.AllDirectories).Select(f => f.Substring(Path.GetFullPath(baselocation).Length + 1).Replace("\\", "/")));
        }
        public FileRead() : this("") { }
        public void Dispose() { }
        public string GetBasePath() => baselocation;
        public bool ExistsFile(string path) => File.Exists(Path.Combine(baselocation, path));
        public bool ExistsFolder(string path) => Path.Exists(Path.Combine(baselocation, path));
        public string AllEntries() => concatentries;

        public string ReadAllText(string path) => File.ReadAllText(Path.Combine(baselocation, path));
        public IEnumerable<string> ReadAllLines(string path) => File.ReadLines(Path.Combine(baselocation, path));
        public static WPFBitmap ReadFromFile(string path) {
            if(File.Exists(path) == false) return null;
            BitmapImage bitmap = new BitmapImage();

            // Initialize the BitmapImage from the file path
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Optional: to cache the image
            bitmap.EndInit();

            // Optionally, freeze the bitmap to make it cross-thread accessible
            bitmap.Freeze();

            return new WPFBitmap(bitmap);
        }
        public WPFBitmap ReadBitmap(string path) => ReadFromFile(Path.Combine(baselocation, path));

        public string[] GetFiles(string path) => Global.FromFolder(Path.Combine(baselocation, path), true, false).ToArray();
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
        public void Dispose() { }

        public void SaveImage(string loc, WPFBitmap img) {
            //img.Save(Path.Combine(baselocation, loc));
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img.ToBitmapSource(true)));

            using(FileStream fileStream = new FileStream(Path.Combine(baselocation, loc), FileMode.Create)) {
                encoder.Save(fileStream);
            }
        } 
        public void SaveLines(string loc, IEnumerable<string> lines) => File.WriteAllLines(Path.Combine(baselocation, loc), lines);
    }

    public class ZipRead : ReadInterface {
        private readonly ZipArchive archive;
        private readonly IDictionary<string, ZipArchiveEntry> entries;
        private readonly string file, concatentries;
        public ZipRead(string file, string inside = "") {
            this.file = file;
            this.archive = ZipFile.Open(file, ZipArchiveMode.Read);
            var e = archive.Entries.Where(entr => entr.FullName.StartsWith(inside));
            // super ineffective but speed not important here prob
            this.entries = e.SelectMany(e => new KeyValuePair<string, ZipArchiveEntry>[] { new(e.FullName, e), new(e.FullName.Replace("/", "\\"), e), new(e.FullName.Replace("/", "\\\\"), e) }).Distinct().ToFrozenDictionary();
            this.concatentries = string.Join(Environment.NewLine, e);
        }

        public void Dispose() => archive.Dispose();
        public string GetBasePath() => file;
        public bool ExistsFile(string path) => entries.ContainsKey(path);
        public bool ExistsFolder(string path) => entries.Keys.Any(x => x.StartsWith(path));
        public string AllEntries() => concatentries;

        public string ReadAllText(string path) {
            if(entries.TryGetValue(path, out var res) == false) return null;
            using var stream = res.Open();
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
        public IEnumerable<string> ReadAllLines(string path) => this.ReadAllText(path).Split(Environment.NewLine);
        public WPFBitmap ReadBitmap(string path) {
            if(entries.TryGetValue(path, out var res) == false) return null;
            using var stream = res.Open();
            MemoryStream mstream = new MemoryStream(); // ??!?!?!?!?!?!?!??!?!?!?!?!
            stream.CopyTo(mstream); // ??!?!?!?!?!?!?!??!?!?!?!?!
            mstream.Position = 0; // ??!?!?!?!?!?!?!??!?!?!?!?!
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = mstream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            //bitmap.Freeze();
            return new WPFBitmap(bitmap);


            /* ?!??!?!?!?!?!?!?!?!!?!?!?!?!?!?!?!?!?!?!?!?!?!??!!??!!??!!?!?!?!?!?!?!??!?!?!?!?!?!?!?!??!?!?!?!?!?!?!?!?!?!??!?!?!?!?!?!?
              ?!??!?!?!?!?!?!?!?!!?!?!?!?!?!?!?!?!?!?!?!?!?!??!!??!!??!!?!?!?!?!?!?!??!?!?!?!?!?!?!?!??!?!?!?!?!?!?!?!?!?!??!?!?!?!?!?!?
              ?!??!?!?!?!?!?!?!?!!?!?!?!?!?!?!?!?!?!?!?!?!?!??!!??!!??!!?!?!?!?!?!?!??!?!?!?!?!?!?!?!??!?!?!?!?!?!?!?!?!?!??!?!?!?!?!?!?  */
            //var decoder = new PngBitmapDecoder(mstream, BitmapCreateOptions.None, BitmapCacheOption.Default);
            ////var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.Default);
            //decoder.DownloadFailed += (o, e) => {
            //    int fg = 3;
            //    stream.Dispose();
            //};
            //decoder.DownloadCompleted += (o, e) => {
            //    var frames = decoder.Frames;
            //    var fr0 = frames[0];
            //    int g = 4;
            //    stream.Dispose();
            //};
            //return new WPFBitmap(decoder.Frames[0]);
            /* ?!??!?!?!?!?!?!?!?!!?!?!?!?!?!?!?!?!?!?!?!?!?!??!!??!!??!!?!?!?!?!?!?!??!?!?!?!?!?!?!?!??!?!?!?!?!?!?!?!?!?!??!?!?!?!?!?!?
              ?!??!?!?!?!?!?!?!?!!?!?!?!?!?!?!?!?!?!?!?!?!?!??!!??!!??!!?!?!?!?!?!?!??!?!?!?!?!?!?!?!??!?!?!?!?!?!?!?!?!?!??!?!?!?!?!?!?
              ?!??!?!?!?!?!?!?!?!!?!?!?!?!?!?!?!?!?!?!?!?!?!??!!??!!??!!?!?!?!?!?!?!??!?!?!?!?!?!?!?!??!?!?!?!?!?!?!?!?!?!??!?!?!?!?!?!?  */
        }

        public string[] GetFiles(string path) { 
            return entries.Keys.Where(ek => ek.StartsWith(path)).ToArray();
        }
    }

    public class ZipSave : SaveInterface {
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



        public void SaveImage(string loc, WPFBitmap img) => SaveFile(loc, ImageToByte2(img));
        public void SaveLines(string loc, IEnumerable<string> lines) => SaveFile(loc, Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)));



        public void SaveFile(string loc, byte[] content) {
            using var w = _archive.CreateEntry(loc, CompressionLevel.NoCompression).Open();
            w.Write(content, 0, content.Length);
        }
        private static byte[] ImageToByte2(WPFBitmap img) {
            using var stream = new MemoryStream();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img.ToBitmapSource(true)));
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
