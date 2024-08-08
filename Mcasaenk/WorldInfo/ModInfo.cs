using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk.WorldInfo {

    public struct ModMetadata() {
        public string name;
        public ImageSource icon;
        public bool assets;
        public bool data;
    }

    public class ModsInfo : IDisposable {
        public readonly List<(ModMetadata meta, ZipRead read)> mods;
        public ModsInfo(LevelDatInfo levelDat) {
            mods = new List<(ModMetadata meta, ZipRead read)>();
            if(levelDat.mods.Length > 0) {
                foreach(var exmod in Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods"))) {
                    if(Path.GetExtension(exmod) != ".jar") continue;
                    ZipRead read = new ZipRead(exmod);
                    ImageSource icon = null;
                    string name = "";
                    if(read.ExistsFile(Path.Combine("META-INF", "mods.toml"))) { // forge
                        var lines = read.ReadAllLines(Path.Combine("META-INF", "mods.toml"));
                        if(lines.Count() == 1) lines = lines.First().Split(['\n']);

                        string chapter = "";
                        foreach(var _line in lines) {
                            var line = _line.Trim();
                            if(line.StartsWith("[[") && line.EndsWith("]]")) {
                                chapter = line.Substring(2, line.Length - 4);
                                continue;
                            }

                            if(line.Contains("=")) {
                                var parts = line.Split('=').Select(s => s.Trim()).ToArray();

                                if(parts[0] == "modId" && chapter == "mods") {
                                    name = parts[1].Substring(1, parts[1].Length - 2);
                                } else if(parts[0] == "logoFile" && chapter == "mods") {
                                    string iconname = parts[1].Substring(1, parts[1].Length - 2);
                                    icon = read.ReadBitmap(iconname).ToBitmapSource();
                                }
                            }
                        }
                        if(levelDat.mods.Contains(name) == false) continue;


                    } else if(read.ExistsFile("fabric.mod.json")) { // fabric
                        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(read.ReadAllText("fabric.mod.json"));
                        name = json["id"].GetString();
                        if(levelDat.mods.Contains(name) == false) continue;
                    } else continue;

                    mods.Add((new ModMetadata() { name = name, icon = icon, assets = read.ExistsFolder("assets"), data = read.ExistsFolder("data") }, read));
                }
            }
        }

        public void Dispose() {
            foreach(var mod in mods) mod.read.Dispose();
        }
    }
}
