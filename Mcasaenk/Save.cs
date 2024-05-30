using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mcasaenk {
    public class Save {
        public readonly LevelDat levelDat;
        public readonly string path;
        public readonly Dimension overworld, nether, end;

        public Save(LevelDat levelDat) { this.levelDat = levelDat; }
        public Save(string path, LevelDat levelDat) : this(levelDat) {
            this.path = path;

            this.overworld = new Dimension(this, Path.Combine(path, "region"), Dimension.Type.Overworld);
            this.nether = new Dimension(this, Path.Combine(path, "DIM-1", "region"), Dimension.Type.Nether);
            this.end = new Dimension(this, Path.Combine(path, "DIM1", "region"), Dimension.Type.End);
        }
        public Save(string path) : this(path, LevelDat.ReadWorld(path)) { }

        public string LevelPath() {
            return Path.Combine(path, "level.dat");
        }

        public virtual Dimension GetDimension(Dimension.Type type) {
            if(overworld.type == type) return overworld;
            if(nether.type == type) return nether;
            if(end.type == type) return end;
            return null;
        }
    }

    public class DimensionSave : Save {
        private readonly Dimension region;
        public DimensionSave(string path) : base(default(LevelDat)) {
            this.region = new Dimension(this, path, Dimension.Type.Overworld);
        }
        public override Dimension GetDimension(Dimension.Type type) {
            return region;
        }
    }

    public class Dimension {
        public readonly string path;
        public readonly Type type;
        public readonly Save save;
        public readonly TileMap tileMap;

        public static readonly Regex regionNamingConvention = new Regex("r.(-?\\d+).(-?\\d+).(mca|mcr)$");

        public Dimension(Save save, string path, Type type) {
            this.save = save;
            this.type = type;
            this.path = path;
            this.tileMap = new TileMap(this, ExistingRegions());
        }

        public string GetRegionPath(Point2i pos) {
            return Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
        }

        public enum Type { Undefined, Overworld, Nether, End }

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
