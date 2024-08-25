using Mcasaenk.WorldInfo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Colormaping.Tint;
using static Mcasaenk.Global;

namespace Mcasaenk.Colormaping {
    public interface Tint {
        public string Name();
        public DynamicTintSettings Settings();

        public uint TintColorFor(ushort biome, short height);
        public uint GetTintedColor(uint baseColor, ushort biome, short height) => ColorMult(baseColor, TintColorFor(biome, height));


        public Blending GetBlendMode();
        public enum Blending { none, heightonly, biomeonly, full }


        public static (string name, string format, string[] blocks, string source, uint color) ReadTint(string path_properties, ReadInterface readInterface) {
            string name = Path.GetFileNameWithoutExtension(path_properties);
            string format = "vanilla";
            string[] blocks = [name.minecraftname()];
            string source = name + ".png";
            uint color = 0xFFFFFFFF;

            foreach(string _line in readInterface.ReadAllLines(path_properties)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "source":
                        source = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "format":
                        format = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "blocks":
                        blocks = line.Substring(line.IndexOf("=") + 1).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(l => l.minecraftnamecomplex()).ToArray();
                        break;
                    case "color":
                        color = 0xFF000000 | Convert.ToUInt32(line.Substring(line.IndexOf("=") + 1).Trim(), 16);
                        break;
                }
            }

            source = Path.Combine(Path.GetDirectoryName(path_properties), source);
            return (name, format, blocks, source, color);
        }

        public static RawTint ReadTint(ReadInterface read, string path_properties, string relbase = "") {
            RawTint tint = new RawTint() {
                name = Path.GetFileNameWithoutExtension(path_properties),
                format = "vanilla",
                color = WPFColor.White,
                yOffset = 0,
            };
            tint.blocks = [tint.name.minecraftname()];
            string source = tint.name + ".png";
            if(relbase == "") relbase = Path.GetDirectoryName(path_properties);

            foreach(string _line in read.ReadAllLines(path_properties)) {
                string line = _line.Trim();
                if(line.Length == 0) continue;

                switch(line.Substring(0, line.IndexOf('='))) {
                    case "source":
                        source = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "format":
                        tint.format = line.Substring(line.IndexOf("=") + 1).Trim();
                        break;
                    case "blocks":
                        tint.blocks = line.Substring(line.IndexOf("=") + 1).Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(l => l.minecraftnamecomplex()).ToList();
                        break;
                    case "color":
                        tint.color = WPFColor.FromHex(line.Substring(line.IndexOf("=") + 1).Trim());
                        break;
                    case "yOffset":
                        int.TryParse(line.Substring(line.IndexOf("=") + 1).Trim(), out tint.yOffset);
                        break;
                }
            }

            tint.image = read.ReadBitmap(Global.GetFullPath(source, relbase));
            return tint;
        }
    }

    public class TintFormat {
        public readonly Type tintclass;
        public readonly string format, kurzformat;
        private TintFormat(string format, string kurzformat, Type tintclass) {
            this.format = format;
            this.kurzformat = kurzformat;
            this.tintclass = tintclass;
        }

        public static TintFormat[] formats = [
            new TintFormat("vanilla", "vn", typeof(OrthodoxVanillaTint)),
            new TintFormat("vanilla_grass", "vn_g", typeof(HardcodedVanillaTint)),
            new TintFormat("vanilla_foliage", "vn_f", typeof(HardcodedVanillaTint)),
            new TintFormat("vanilla_water", "vn_w", typeof(HardcodedVanillaTint)),
            new TintFormat("fixed", "fx", typeof(FixedTint)),
            new TintFormat("grid", "gr", typeof(GridTint)),
        ];

        public static TintFormat GetFormat(string format) {
            return formats.FirstOrDefault(f => format == f.format || format == f.kurzformat);
        }
        public static TintFormat GetFormat(Type tintclass) {
            foreach(var tint in formats) {
                if(tint.tintclass == tintclass) return tint;
            }
            return null;
        }
    }

    public class OrthodoxVanillaTint : Tint {
        private readonly string name;
        private readonly int version;
        private uint[,] sprite;

        private DatapacksInfo datapacksInfo;
        private DynamicVanillaTintSettings settings = new DynamicVanillaTintSettings();

        public OrthodoxVanillaTint(string name, int verion, uint[,] sprite, DatapacksInfo datapacksInfo) {
            this.name = name;
            version = verion;
            this.datapacksInfo = datapacksInfo;

            this.sprite = sprite;
            if(sprite == null) this.sprite = new uint[256, 256];
            else if(sprite.GetLength(0) != 256 && sprite.GetLength(0) != 256) throw new Exception();
        }
        string Tint.Name() => name;
        DynamicTintSettings Tint.Settings() => settings;

        uint Tint.TintColorFor(ushort biome, short height) {
            return datapacksInfo.biomes[biome].GetOrthodox(sprite, height, version, settings.TemperatureVariation);
        }

        Blending Tint.GetBlendMode() {
            if(settings.On == false) return Blending.none;
            if(settings.Blend == 1) return Blending.heightonly;
            if(settings.TemperatureVariation > 0) return Blending.full;
            else return Blending.biomeonly;
        }
    }
    public class HardcodedVanillaTint : Tint {
        private readonly string name, tint, kurzformat;
        private readonly int version;
        private uint[,] sprite;

        private DatapacksInfo datapacksInfo;
        private DynamicVanillaTintSettings settings = new DynamicVanillaTintSettings();

        public HardcodedVanillaTint(string name, string format, int version, uint[,] sprite, DatapacksInfo datapackInfo) {
            this.name = name;
            var tformat = TintFormat.GetFormat(format);
            this.tint = tformat.format.Replace("vanilla_", "");
            this.version = version;
            datapacksInfo = datapackInfo;

            this.sprite = sprite;
            if(sprite == null) this.sprite = new uint[256, 256];
            else if(sprite.GetLength(0) != 256 && sprite.GetLength(0) != 256) throw new Exception();
        }

        string Tint.Name() => name;
        DynamicTintSettings Tint.Settings() => settings;

        uint Tint.TintColorFor(ushort biome, short height) {
            return datapacksInfo.biomes[biome].GetVanilla(tint, sprite, sprite, height, version, settings.TemperatureVariation);
        }

        Blending Tint.GetBlendMode() {
            if(settings.On == false) return Blending.none;
            if(settings.Blend == 1) return Blending.heightonly;
            if(settings.TemperatureVariation > 0 && (tint == "grass" || tint == "foliage")) return Blending.full;
            else return Blending.biomeonly;
        }

    }
    public class FixedTint : Tint { // for every block its own tint
        private readonly string name;

        private uint tint, baseColor;
        private bool hasBaseColor;
        public FixedTint(string name, uint tint, uint baseColor) {
            this.name = name;
            this.tint = tint;
            this.baseColor = ColorMult(tint, baseColor);

            hasBaseColor = true;
        }
        string Tint.Name() => name;
        DynamicTintSettings Tint.Settings() => null;
        Blending Tint.GetBlendMode() => Blending.none;

        public FixedTint(string name, uint tint) {
            this.name = name;
            this.tint = tint;
            hasBaseColor = false;
        }

        uint Tint.TintColorFor(ushort biome, short height) => tint;
        uint Tint.GetTintedColor(uint baseColor, ushort biome, short height) => hasBaseColor ? this.baseColor : ColorMult(baseColor, tint);
    }
    public class GridTint : Tint {
        private readonly string name;
        private uint[,] sprite;
        private int offset;

        private bool heightparity;
        private readonly DynamicTintSettings settings = new DynamicTintSettings();
        public GridTint(string name, int offset, uint[,] sprite) {
            this.name = name;
            this.offset = offset;
            this.sprite = sprite;

            if(sprite == null) this.sprite = new uint[1, 1];

            heightparity = true;
            for(int x = 0; x < sprite.GetLength(0); x++) {
                uint c = sprite[x, 0];
                for(int y = 0; y < sprite.GetLength(1); y++) {
                    if(sprite[x, y] != c) {
                        heightparity = false;
                        break;
                    }
                }
                if(heightparity == false) break;
            }
        }
        string Tint.Name() => name;
        DynamicTintSettings Tint.Settings() => settings;

        uint Tint.TintColorFor(ushort biome, short height) {
            int x = settings.On ? biome : Global.Settings.DEFBIOME;
            if(x >= sprite.GetLength(0) || x < 0) x = Global.Settings.DEFBIOME;
            int y = Math.Clamp(height - offset, 0, sprite.GetLength(1) - 1);
            return sprite[x, y];
        }



        Blending Tint.GetBlendMode() {
            if(settings.On == false) return Blending.none;
            else if(sprite.GetLength(0) == 1 || settings.Blend == 1) return Blending.heightonly;
            else if(sprite.GetLength(1) == 1 || heightparity) return Blending.biomeonly;
            else return Blending.full;
        }


    }
    public class NullTint : Tint {
        public static NullTint Tint = new NullTint();
        private NullTint() { }

        string Tint.Name() => "";
        DynamicTintSettings Tint.Settings() => null;
        Blending Tint.GetBlendMode() => Blending.none;

        uint Tint.TintColorFor(ushort biome, short height) => 0xFFFFFFFF;

        uint Tint.GetTintedColor(uint baseColor, ushort biome, short height) => baseColor;
    }
}
