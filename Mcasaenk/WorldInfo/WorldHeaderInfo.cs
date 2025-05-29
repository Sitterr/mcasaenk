using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mcasaenk.Nbt;
using Mcasaenk.Resources;

namespace Mcasaenk.WorldInfo {

    public class PackMetadata {
        public string id { get; set; }
        public string name { get; set; }
        public string path { get; set; }
        public ImageSource icon { get; set; }
        public string description { get; set; }
        public PackMetadata(string path, string name, ImageSource icon, string description = null, string id = null) {
            this.path = path;
            this.name = name;
            this.icon = icon;
            this.description = description;
            if(id == null) id = name;
            this.id = id;
        }

        public static unsafe bool ReadModMeta(ReadInterface read, out PackMetadata metadata) {
            metadata = null;
            try {
                ImageSource icon = null;
                string id = null, name = "";
                string description = null;
                if(read.ExistsFile(Path.Combine("META-INF", "mods.toml"))) { // forge
                    var lines = read.ReadAllLines(Path.Combine("META-INF", "mods.toml"));
                    if(lines.Count() == 1) lines = lines.First().Split(['\n']);

                    string* multylineval = null;
                    string multyline = "";
                    string chapter = "";
                    foreach(var _line in lines) {
                        var line = _line.Trim();
                        if(line.Contains('#')) line = line.Substring(0, line.IndexOf('#')).Trim();
                        if(line.StartsWith("[[") && line.EndsWith("]]")) {
                            chapter = line.Substring(2, line.Length - 4);
                            continue;
                        }

                        if(multylineval != null) {
                            if(!multyline.EndsWith(" ") && multyline != "") multyline += " ";
                            multyline += line;
                            if(line.EndsWith("'''")) {
                                multyline = multyline.Substring(0, multyline.Length - 3);
                                *multylineval = multyline;
                                multylineval = null;
                            }
                        }

                        if(line.Contains("=")) {
                            var parts = line.Split('=').Select(s => s.Trim()).ToArray();

                            if(chapter == "mods" || chapter == "") {
                                if(parts[0] == "modId") {
                                    name = parts[1].Substring(1, parts[1].Length - 2);
                                } else if(parts[0] == "logoFile") {
                                    string iconname = parts[1].Substring(1, parts[1].Length - 2);
                                    icon = read.ReadBitmap(iconname)?.ToBitmapSource();
                                } else if(parts[0] == "description") {
                                    if(parts[1].StartsWith("'''")) {
                                        if(parts[1].Substring(3).EndsWith("'''")) description = parts[1].Substring(3, parts[1].Length - 6);
                                        else {
                                            multyline = parts[1].Substring(3);
                                            multylineval = &description;
                                        }
                                    } else if(parts[1].StartsWith('"') || parts[1].StartsWith("'")) description = parts[1].Substring(1, parts[1].Length - 2);
                                }
                            }
                        }
                    }
                    if(icon == null) icon = read.ReadBitmap("logo.png")?.ToBitmapSource();


                } else if(read.ExistsFile("fabric.mod.json")) { // fabric
                    var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(read.ReadAllText("fabric.mod.json"));
                    if(json.TryGetValue("id", out var _id) == false) return false;
                    id = _id.GetString();
                    if(json.TryGetValue("name", out var _name)) name = _name.GetString();
                    if(json.TryGetValue("description", out var _descr)) description = _descr.GetString();
                    if(json.TryGetValue("icon", out var _icon)) icon = read.ReadBitmap(_icon.GetString())?.ToBitmapSource();
                } else return false;
                if(icon == null) icon = WPFBitmap.FromBytes(ResourceMapping.unknown_server)?.ToBitmapSource();
                metadata = new PackMetadata(read.GetBasePath(), name, icon, description, id);
                return true;
            } catch {
                return false;
            }
        }

        public static bool ReadPackMeta(ReadInterface read, string name, out PackMetadata metadata) {
            metadata = null;
            try {
                if(read.ExistsFile("pack.mcmeta") == false) return false;
                ImageSource icon = read.ReadBitmap("pack.png")?.ToBitmapSource();
                if(icon == null) icon = WPFBitmap.FromBytes(ResourceMapping.unknown_pack).ToBitmapSource();
                metadata = new PackMetadata(read.GetBasePath(), name, icon, readDescription(read.ReadAllText("pack.mcmeta")));
                return true;
            } catch {
                return false;
            }
        }

        private static string readDescription(string text) {
            try {
                var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
                if(json.TryGetValue("pack", out var pack) == false) return "";
                var descr = pack.EnumerateObject().Where(p => p.NameEquals("description"));
                if(descr.Count() == 0) return "";
                return Regex.Replace(descr.First().Value.GetString(), @"§.", "");
            } catch {
                return "";
            }
        }
    }

    public class LevelDatInfo {
        static BitmapSource defaultIcon;
        static LevelDatInfo() {
            defaultIcon = WPFBitmap.FromBytes(ResourceMapping.unknown_server).ToBitmapSource();
        }

        public DateOnly lastopened { get; private set; }
        public Difficulty difficulty { get; private set; }
        public Gamemode gamemode { get; private set; }
        public string name { get; private set; }
        public string version_name { get; private set; }
        public int version_id { get; private set; }
        public long seed { get; private set; }
        public ImageSource image { get; private set; }
        public string foldername { get; private set; }

        public string[] datapacks { get; private set; }
        public string[] mods { get; private set; }
        public bool resourcepack { get; private set; }

        public readonly PlayerLocData mainPlayer = new();
        public readonly PlayerLocData[] otherPlayers = [];

        private LevelDatInfo(Tag _tag, DirectoryInfo folder, ImageSource image, DateOnly lastopened) {
            if(image == null) image = defaultIcon;
            if(image.CanFreeze) image.Freeze();
            this.image = image;
            this.foldername = folder.Name;
            this.lastopened = lastopened;
            this.resourcepack = folder.GetFiles().Any(f => f.Name == "resources.zip");

            bool hardcore = false;

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
                hardcore = ((NumTag<sbyte>)data["hardcore"]) > 0;
                mainPlayer.sp_x = (NumTag<int>)data["SpawnX"];
                mainPlayer.sp_y = (NumTag<int>)data["SpawnY"];
                mainPlayer.sp_z = (NumTag<int>)data["SpawnZ"];
                if(data["SpawnDimension"] is NumTag<string> st) mainPlayer.sp_d = st;
                else if(data["SpawnDimension"] is NumTag<int> it) {
                    if(it == 0) mainPlayer.sp_d = "minecraft:overworld";
                    if(it == 1) mainPlayer.sp_d = "minecraft:the_nether";
                    if(it == 2) mainPlayer.sp_d = "minecraft:the_end";
                } else mainPlayer.sp_d = "minecraft:overworld";

                var player = (CompoundTag_Optimal)data["Player"];
                {

                    if(player?["Pos"] != null) {
                        var pos = (List<Tag>)(ListTag)player["Pos"];
                        mainPlayer.pl_x = (int)(NumTag<double>)pos[0];
                        mainPlayer.pl_y = (int)(NumTag<double>)pos[1];
                        mainPlayer.pl_z = (int)(NumTag<double>)pos[2];
                        if(player["Dimension"] is NumTag<string> st2) mainPlayer.pl_d = st2;
                        else if(player["Dimension"] is NumTag<int> it) {
                            if(it == 0) mainPlayer.pl_d = "minecraft:overworld";
                            if(it == 1) mainPlayer.pl_d = "minecraft:the_nether";
                            if(it == 2) mainPlayer.pl_d = "minecraft:the_end";
                        } else mainPlayer.pl_d = "minecraft:overworld";
                    }
                }
                var datapacks = (CompoundTag_Optimal)data["DataPacks"];
                this.mods = [];
                this.datapacks = [];
                if(datapacks != null) {
                    var enabled = ((List<Tag>)(ListTag)datapacks["Enabled"]).Select(e => (string)(NumTag<string>)e).Where(e => e != "vanilla");

                    if(enabled.Any(e => e == "fabric")) {
                        this.mods = enabled.Where(e => !e.StartsWith("file/")).ToArray();
                    } else if(enabled.Any(e => e == "mod:forge" || e == "mod:neoforge")) {
                        this.mods = enabled.Where(e => e.StartsWith("mod:")).Select(e => e.Substring(4)).ToArray();
                    }
                    this.datapacks = enabled.Where(e => e.StartsWith("file/")).Select(e => e.Substring(5)).ToArray();

                }
            }


            // other players
            {
                var plf = folder.GetDirectories().FirstOrDefault(sd => sd.Name == "playerdata");
                List<PlayerLocData> otherPlayers = new();
                if(plf != null) {
                    foreach(var datf in plf.GetFiles().Where(f => f.Extension == ".dat")) {
                        try {
                            PlayerLocData player = new PlayerLocData();

                            using var pointer = new MemoryStream(File.ReadAllBytes(datf.FullName));
                            if(pointer == null) continue;
                            using var zlip = new GZipStream(pointer, CompressionMode.Decompress);
                            using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared);
                            var nbtreader = new NbtReader(decompressedStream);
                            _ = nbtreader.TryRead(out var _g);
                            var globaltag = (CompoundTag_Optimal)_g;
                            player.sp_x = (NumTag<int>)globaltag["SpawnX"];
                            player.sp_y = (NumTag<int>)globaltag["SpawnY"];
                            player.sp_z = (NumTag<int>)globaltag["SpawnZ"];
                            if(globaltag["SpawnDimension"] is NumTag<string> st) player.sp_d = st;
                            else if(globaltag["SpawnDimension"] is NumTag<int> it) {
                                if(it == 0) player.sp_d = "minecraft:overworld";
                                if(it == 1) player.sp_d = "minecraft:the_nether";
                                if(it == 2) player.sp_d = "minecraft:the_end";
                            } else player.sp_d = "minecraft:overworld";

                            if(globaltag?["Pos"] != null) {
                                var pos = (List<Tag>)(ListTag)globaltag["Pos"];

                                player.pl_x = (int)(NumTag<double>)pos[0];
                                player.pl_y = (int)(NumTag<double>)pos[1];
                                player.pl_z = (int)(NumTag<double>)pos[2];
                                if(globaltag["Dimension"] is NumTag<string> st3) player.pl_d = st3;
                                else if(globaltag["Dimension"] is NumTag<int> it) {
                                    if(it == 0) player.pl_d = "minecraft:overworld";
                                    if(it == 1) player.pl_d = "minecraft:the_nether";
                                    if(it == 2) player.pl_d = "minecraft:the_end";
                                } else player.pl_d = "minecraft:overworld";

                            }

                            otherPlayers.Add(player);
                        } catch {
                            continue;
                        }
                    }
                }
                this.otherPlayers = otherPlayers.ToArray();
            }


            if(hardcore && gamemode == Gamemode.Survival) gamemode = Gamemode.Hardcore;
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
            } catch {
                return null;
            }
        }

        public static LevelDatInfo ReadRegionFolder() {
            return new LevelDatInfo() { };
        }
    }

    public struct PlayerLocData {
        public string sp_d, pl_d;
        public int pl_x, pl_y, pl_z;
        public int sp_x, sp_y, sp_z;
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
        [Description("hardcore")]
        Hardcore = 10,
    }
}
