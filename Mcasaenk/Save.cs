using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk {
    public class Save {
        public readonly string path;
        public readonly Dimension overworld, nether, end;

        public Save(string path) { 
            this.path = path;

            this.overworld = new Dimension(this, Dimension.Type.Overworld);
            this.nether = new Dimension(this, Dimension.Type.Nether);
            this.end = new Dimension(this, Dimension.Type.End);
        }

        public string LevelPath() {
            return Path.Combine(path, "level.dat");
        }
    }

    public class Dimension {
        public readonly string path;
        public readonly Type type;
        public readonly Save save;
        public readonly TileMap tileMap;

        public Dimension(Save save, Type type) {
            this.save = save;
            this.type = type;
            this.path = type switch {
                Type.Overworld => Path.Combine(save.path, "region"),
                Type.Nether => Path.Combine(save.path, "DIM-1", "region"),
                Type.End => Path.Combine(save.path, "DIM1", "region"),
            };
            this.tileMap = new TileMap(this);
        }

        public string GetRegionPath(Point2i pos) {
            return Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
        }

        public enum Type { 
            Overworld, Nether, End
        }
    }
}
