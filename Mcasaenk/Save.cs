using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.UI;
using Mcasaenk.WorldInfo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mcasaenk {
    public class Save {
        public readonly LevelDatInfo levelDatInfo;
        public readonly DatapacksInfo datapackInfo;
        public readonly string path;
        public readonly Dimension overworld, nether, end;
        public readonly List<Dimension> dimensions;

        public Save(string path, LevelDatInfo levelDat, DatapacksInfo datapackInfo) {
            this.path = path;
            this.levelDatInfo = levelDat;
            this.datapackInfo = datapackInfo;

            this.overworld = new Dimension(this, Path.Combine(path, "region"), datapackInfo.dimensions["minecraft:overworld"]);
            this.nether = new Dimension(this, Path.Combine(path, "DIM-1", "region"), datapackInfo.dimensions["minecraft:the_nether"]);
            this.end = new Dimension(this, Path.Combine(path, "DIM1", "region"), datapackInfo.dimensions["minecraft:the_end"]);

            dimensions = new List<Dimension>() { overworld, nether, end };

            foreach(var dim in datapackInfo.dimensions.Values) {
                if(dimensions.Any(d => d.name == dim.name)) continue;
                var name = dim.name.fromminecraftname();
                dimensions.Add(new Dimension(this, Path.Combine(path, "dimensions", name.@namespace, name.name, "region"), dim));
            }          
        }

        public Dimension GetDimension(string name) {
            return dimensions.FirstOrDefault(d => d.info.name == name);
        }

        public Save(string path) : this(path, LevelDatInfo.ReadWorld(path), new DatapacksInfo(path)) { }

        public string LevelPath() {
            return Path.Combine(path, "level.dat");
        }
    }

    public class Dimension {
        public static readonly Regex regionNamingConvention = new Regex("r.(-?\\d+).(-?\\d+).(mca|mcr)$");

        public readonly string name;
        public readonly string path;
        public DimensionInfo info;
        public readonly Save save;
        public readonly TileMap tileMap;

        public Dimension(Save save, string path, DimensionInfo info) {
            this.save = save;
            this.info = info;
            this.name = info.name;
            this.path = path;
            this.tileMap = new TileMap(this, ExistingRegions());
        }

        public (int height, int miny) GetHeight(int version) {
            if(info.name == "minecraft:overworld" && info.fromparts) {
                if(version >= 2825) return (384, -64);
                else return (256, 0);
            }
            return (info.height, info.miny);
        }

        public string GetRegionPath(Point2i pos) {
            return Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
        }

        private HashSet<Point2i> ExistingRegions() { 
            var set = new HashSet<Point2i>();

            if(Directory.Exists(this.path)) {
                foreach(var file in Directory.GetFiles(this.path)) {
                    var match = regionNamingConvention.Match(file);
                    if(match.Success) {
                        set.Add(new Point2i(Convert.ToInt32(match.Groups[1].Value), Convert.ToInt32(match.Groups[2].Value)));
                    }
                }
            }

            return set;
        }
    }
}
