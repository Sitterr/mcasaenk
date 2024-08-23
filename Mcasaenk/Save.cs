using Mcasaenk.Colormaping;
using Mcasaenk.Resources;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Mcasaenk.Global;

namespace Mcasaenk {
    public class Save {
        public readonly LevelDatInfo levelDatInfo;
        public readonly DatapacksInfo datapackInfo;
        public readonly PackMetadata[] mods;
        public readonly string path;
        public readonly Dimension overworld, nether, end;
        public readonly List<Dimension> dimensions;

        public Save(string path, LevelDatInfo levelDat) {
            this.path = path;
            this.levelDatInfo = levelDat;

            using var modsInfo = new ModsInfo(levelDat);
            this.mods = modsInfo.mods.Select(f => f.meta).ToArray();

            this.datapackInfo = DatapacksInfo.FromPath(path, levelDat, modsInfo);

            this.overworld = new Dimension(this, Path.Combine(path, "region"), datapackInfo.dimensions["minecraft:overworld"], WPFBitmap.FromBytes(ResourceMapping.grass8).ToBitmapSource());
            this.nether = new Dimension(this, Path.Combine(path, "DIM-1", "region"), datapackInfo.dimensions["minecraft:the_nether"], WPFBitmap.FromBytes(ResourceMapping.nether8).ToBitmapSource());
            this.end = new Dimension(this, Path.Combine(path, "DIM1", "region"), datapackInfo.dimensions["minecraft:the_end"], WPFBitmap.FromBytes(ResourceMapping.end8).ToBitmapSource());

            dimensions = new List<Dimension>() { overworld, nether, end };

            var unknownPack = WPFBitmap.FromBytes(ResourceMapping.unknown_pack);
            foreach(var dim in datapackInfo.dimensions.Values) {
                if(dimensions.Any(d => d.name == dim.name)) continue;
                var name = dim.name.fromminecraftname();
                string regionpath = Path.Combine(path, "dimensions", name.@namespace, name.name, "region");
                if(Path.Exists(regionpath) == false) continue;
                dimensions.Add(new Dimension(this, regionpath, dim, dim.image != null ? dim.image : unknownPack.ToBitmapSource()));
            }

            if(Path.Exists(Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps"))) Directory.Delete(Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps"), true);
            if(levelDatInfo.resourcepack || modsInfo.mods.Count() > 0) {
                List<ReadInterface> respacks = new List<ReadInterface>();
                using ReadInterface vanilla = new ZipRead(Path.Combine(Global.App.APPFOLDER, "vanilla_resource_pack.zip"));
                using ReadInterface buildin = levelDatInfo.resourcepack ? new ZipRead(Path.Combine(path, "resources.zip")) : null;

                var list = new[] { vanilla }.Concat(modsInfo.mods.Select(m => m.read)).ToList();
                if(buildin != null) list.Add(buildin);

                RawColormap.Save(ResourcepackColormapMaker.Make(list.ToArray(), new Options()), Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps", "default"));
            }
        }

        public void Reset() {
            foreach(var dim in dimensions) {
                dim.Reset();
            }
        }

        public static Save FromPath(string path) {
            var level = LevelDatInfo.ReadWorld(path);
            return new Save(path, level);
        }


        public Dimension GetDimension(string name) {
            return dimensions.FirstOrDefault(d => d.info.name == name);
        }

        public string LevelPath() {
            return Path.Combine(path, "level.dat");
        }
    }

    public class Dimension {
        public static readonly Regex regionNamingConvention = new Regex("r.(-?\\d+).(-?\\d+).(mca|mcr)$");

        public readonly string name;
        public readonly string path;
        public readonly ImageSource image;
        public DimensionInfo info;
        public readonly Save save;
        public TileMap tileMap;

        public Dimension(Save save, string path, DimensionInfo info, ImageSource image) {
            this.save = save;
            this.info = info;
            this.image = image;
            this.name = info.name;
            this.path = path;
            this.tileMap = new TileMap(this, ExistingRegions());
        }

        public void Reset() {
            this.tileMap = new TileMap(this, ExistingRegions());
        }

        public (short height, short miny, short defheight) GetHeight() => (info.height, info.miny, info.defHeight);

        public string GetRegionPath(Point2i pos) {
            return Path.Combine(path, $"r.{pos.X}.{pos.Z}.mca");
        }

        private HashSet<Point2i> ExistingRegions() {
            var set = new HashSet<Point2i>();

            foreach(var file in Global.FromFolder(this.path, true, false)) {
                var match = regionNamingConvention.Match(file);
                if(match.Success) {
                    set.Add(new Point2i(Convert.ToInt32(match.Groups[1].Value), Convert.ToInt32(match.Groups[2].Value)));
                }
            }

            return set;
        }
    }
}
