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
        public string resourcepack_path { get; private set; }
        public bool resourcepack => resourcepack_path != null;

        public readonly PlayerLocData mainPlayer = new();
        public readonly PlayerLocData[] otherPlayers = [];

        private LevelDatInfo(Tag _tag, DirectoryInfo folder, ImageSource image, DateOnly lastopened) {
            if(image == null) image = defaultIcon;
            if(image.CanFreeze) image.Freeze();
            this.image = image;
            this.foldername = folder.Name;
            this.lastopened = lastopened;
            string packpath = Path.Combine(folder.FullName, "resourcepacks", "resources.zip");
            if(!File.Exists(packpath)) packpath = Path.Combine(folder.FullName, "resources.zip");
            this.resourcepack_path = File.Exists(packpath) ? packpath : null;

            bool hardcore = false;

            var tag = (CompoundTag_Optimal)_tag;
            var data = (CompoundTag_Optimal)tag["Data"];
            var embeddedPlayer = data["Player"] as CompoundTag_Optimal;
            PlayerLocData? mainPosition = embeddedPlayer != null ? ReadPlayer(embeddedPlayer) : null;
            string singleplayer = null;
            var uuid = data["singleplayer_uuid"] ?? embeddedPlayer?["UUID"];
            if(uuid is ArrTag<int> ids && ids.Length == 4) {
                singleplayer = Guid.ParseExact($"{ids[0]:x8}{ids[1]:x8}{ids[2]:x8}{ids[3]:x8}", "N").ToString();
            } else if(uuid is NumTag<string> uuidString) singleplayer = uuidString;
            {
                var d = (NumTag<sbyte>)data["Difficulty"];
                this.difficulty = d != null ? (Difficulty)(sbyte)d : Difficulty.Normal;
                if(data["difficulty_settings"] is CompoundTag_Optimal difficultySettings &&
                    difficultySettings["difficulty"] is NumTag<string> difficultyName &&
                    Enum.TryParse<Difficulty>(difficultyName, true, out var difficulty)) this.difficulty = difficulty;

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
                string settingspath = Path.Combine(folder.FullName, "data", "minecraft", "world_gen_settings.dat");
                if(File.Exists(settingspath)) {
                    using var settings = ReadDat(settingspath);
                    var settingsData = (CompoundTag_Optimal)settings["data"];
                    this.seed = (NumTag<long>)settingsData["seed"];
                }
                hardcore = ((NumTag<sbyte>)data["hardcore"]) > 0;
                mainPlayer = ReadPlayer(data);
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
                string playerpath = Path.Combine(folder.FullName, "players", "data");
                if(!Directory.Exists(playerpath)) playerpath = Path.Combine(folder.FullName, "playerdata");
                List<PlayerLocData> otherPlayers = new();
                if(Directory.Exists(playerpath)) {
                    foreach(var datf in new DirectoryInfo(playerpath).GetFiles("*.dat")
                        .OrderByDescending(f => f.Name.Equals(singleplayer + ".dat", StringComparison.OrdinalIgnoreCase))) {
                        try {
                            using var globaltag = ReadDat(datf.FullName);
                            var player = ReadPlayer(globaltag);
                            if(datf.Name.Equals(singleplayer + ".dat", StringComparison.OrdinalIgnoreCase)) mainPosition = player;
                            otherPlayers.Add(player);
                        } catch {
                            continue;
                        }
                    }
                }
                this.otherPlayers = otherPlayers.ToArray();
            }

            if(mainPosition is PlayerLocData position) {
                mainPlayer.pl_x = position.pl_x;
                mainPlayer.pl_y = position.pl_y;
                mainPlayer.pl_z = position.pl_z;
                mainPlayer.pl_d = position.pl_d;
            }

            if(hardcore && gamemode == Gamemode.Survival) gamemode = Gamemode.Hardcore;
        }
        private LevelDatInfo() { }

        private static PlayerLocData ReadPlayer(CompoundTag_Optimal tag) {
            PlayerLocData player = new();
            var spawn = (CompoundTag_Optimal)(tag["spawn"] ?? tag["respawn"]);
            if(spawn?["pos"] is ArrTag<int> spawnPos && spawnPos.Length == 3) {
                player.sp_x = spawnPos[0];
                player.sp_y = spawnPos[1];
                player.sp_z = spawnPos[2];
                player.sp_d = ReadDimension(spawn["dimension"]);
            } else if(tag["SpawnX"] != null) {
                player.sp_x = (NumTag<int>)tag["SpawnX"];
                player.sp_y = (NumTag<int>)tag["SpawnY"];
                player.sp_z = (NumTag<int>)tag["SpawnZ"];
                player.sp_d = ReadDimension(tag["SpawnDimension"]);
            }

            if(tag["Pos"] is ListTag pos && pos.Length == 3) {
                var coords = (List<Tag>)pos;
                player.pl_x = (int)(NumTag<double>)coords[0];
                player.pl_y = (int)(NumTag<double>)coords[1];
                player.pl_z = (int)(NumTag<double>)coords[2];
                player.pl_d = ReadDimension(tag["Dimension"]);
            }
            return player;
        }

        private static string ReadDimension(Tag tag) {
            if(tag is NumTag<string> name) return name;
            if(tag is NumTag<int> id) {
                return (int)id switch {
                    -1 => "minecraft:the_nether",
                    1 => "minecraft:the_end",
                    _ => "minecraft:overworld",
                };
            }
            return "minecraft:overworld";
        }

        private static CompoundTag_Optimal ReadDat(string path) {
            using var pointer = File.OpenRead(path);
            using var zlip = new GZipStream(pointer, CompressionMode.Decompress);
            using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared);
            var nbtreader = new NbtReader(decompressedStream);
            _ = nbtreader.TryRead(out var tag);
            return (CompoundTag_Optimal)tag;
        }

        public static LevelDatInfo ReadWorld(string path) {
            try {
                using var globaltag = ReadDat(Path.Combine(path, "level.dat"));

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
