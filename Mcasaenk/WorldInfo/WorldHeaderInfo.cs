using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mcasaenk.Resources;

namespace Mcasaenk.WorldInfo {
    public class LevelDatInfo {
        static BitmapImage defaultIcon;
        static LevelDatInfo() {
            defaultIcon = new BitmapImage();
            using(MemoryStream memoryStream = new MemoryStream(ResourceMapping.unknown_server)) {
                defaultIcon.BeginInit();
                defaultIcon.StreamSource = memoryStream;
                defaultIcon.CacheOption = BitmapCacheOption.OnLoad;
                defaultIcon.EndInit();
            }
            defaultIcon.Freeze();
        }

        public DateOnly lastopened { get; private set; }
        public Difficulty difficulty { get; private set; }
        public Gamemode gamemode { get; private set; }
        public string name { get; private set; }
        public string version_name { get; private set; }
        public int version_id { get; private set; }
        public long seed { get; private set; }
        public bool hardcore { get; private set; }
        public ImageSource image { get; private set; }
        public string foldername { get; private set; }

        public string[] datapacks { get; private set; }
        public string[] mods { get; private set; }
        public bool resourcepack { get; private set; }

        public string pd { get; private set; }
        public int px { get; private set; }
        public int py { get; private set; }
        public int pz { get; private set; }
        public int sx { get; private set; }
        public int sy { get; private set; }
        public int sz { get; private set; }

        private LevelDatInfo(Tag _tag, DirectoryInfo folder, ImageSource image, DateOnly lastopened) {
            if(image == null) image = defaultIcon;
            if(image.CanFreeze) image.Freeze();
            this.image = image;
            this.foldername = folder.Name;         
            this.lastopened = lastopened;
            this.resourcepack = folder.GetFiles().Any(f => f.Name == "resources.zip");

            var tag = (CompoundTag_Optimal)_tag;
            var data = (CompoundTag_Optimal)tag["Data"];
            {
                var d = (NumTag<sbyte>)data["Difficulty"];
                this.difficulty = d != null ? (Difficulty)(sbyte)d : Difficulty.Normal;

                this.name = (NumTag<string>)data["LevelName"];
                this.gamemode = (Gamemode)(int)(NumTag<int>)data["GameType"];
                var version = (CompoundTag_Optimal)data["Version"];
                if(version != null) {
                    this.version_name = (NumTag<string>)version["Name"];
                    this.version_id = (NumTag<int>)version["Id"];
                } else {
                    this.version_name = "unknown";
                    this.version_id = -1;
                }
                var worldGenSettings = (CompoundTag_Optimal)data["WorldGenSettings"];
                if(worldGenSettings != null) {
                    this.seed = (NumTag<long>)worldGenSettings["seed"];
                } else {
                    this.seed = (NumTag<long>)data["RandomSeed"];
                }
                this.hardcore = ((NumTag<sbyte>)data["hardcore"]) > 0;
                this.sx = (NumTag<int>)data["SpawnX"];
                this.sy = (NumTag<int>)data["SpawnY"];
                this.sz = (NumTag<int>)data["SpawnZ"];
                var player = (CompoundTag_Optimal)data["Player"];
                {
                    
                    if(player?["Pos"] != null) {
                        var pos = (List<Tag>)(ListTag)player["Pos"];
                        this.px = (int)(NumTag<double>)pos[0];
                        this.py = (int)(NumTag<double>)pos[1];
                        this.pz = (int)(NumTag<double>)pos[2];
                        if(player["Dimension"] is NumTag<string> st) this.pd = st;
                        else if(player["Dimension"] is NumTag<int> it) {
                            if(it == 0) this.pd = "minecraft:overworld";
                            if(it == 1) this.pd = "minecraft:the_nether";
                            if(it == 2) this.pd = "minecraft:the_end";
                        }
                    } else {
                        this.px = this.py = this.pz = int.MaxValue;
                        this.pd = "";
                    }
                }
                var datapacks = (CompoundTag_Optimal)data["DataPacks"];
                if(datapacks != null) {
                    var enabled = ((List<Tag>)(ListTag)datapacks["Enabled"]).Select(e => (string)(NumTag<string>)e);

                    this.datapacks = enabled.Where(e => e.StartsWith("file/")).Select(e => e.Substring(5)).ToArray();
                    this.mods = enabled.Where(e => e.StartsWith("mod:")).Select(e => e.Substring(4)).ToArray();
                }
            }
        }
        private LevelDatInfo() { }


        public static LevelDatInfo ReadWorld(string path) {
            try {
                using var pointer = new MemoryStream(File.ReadAllBytes(Path.Combine(path, "level.dat")));

                if(pointer == null) return null;
                using var zlip = new GZipStream(pointer, CompressionMode.Decompress);
                using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared);


                var nbtreader = new NbtReader(decompressedStream);
                bool error = nbtreader.TryRead(out var _g);

                var globaltag = (CompoundTag_Optimal)_g;

                ImageSource icon = null;
                if(File.Exists(Path.Combine(path, "icon.png"))) {
                    icon = new BitmapImage(new Uri(Path.Combine(path, "icon.png")));
                }
                return new LevelDatInfo(globaltag, new DirectoryInfo(path), icon, DateOnly.FromDateTime(File.GetLastWriteTime(Path.Combine(path, "level.dat"))));
            }
            catch {
                return null;
            }
        }

        public static LevelDatInfo ReadRegionFolder() {
            return new LevelDatInfo() { };
        }
    }
    public enum Difficulty : sbyte {
        [Description("peaceful")]
        Peaceful = 0,
        [Description("easy")]
        Easy = 1,
        [Description("normal")]
        Normal = 2,
        [Description("hard")]
        Hard = 3,
    }
    public enum Gamemode : int {
        [Description("survival")]
        Survival = 0,
        [Description("creative")]
        Creative = 1,
        [Description("adventure")]
        Adventure = 2,
        [Description("spectator")]
        Spectator = 3,
    }
}
