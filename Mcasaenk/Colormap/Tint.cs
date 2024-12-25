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
    public class TintMeta {
        public readonly Type tintclass;
        public readonly string format, kurzformat;
        public readonly int maxblend; // the upper bound is hypotetically 1025
        private TintMeta(string format, string kurzformat, int maxblend, Type tintclass) {
            this.format = format;
            this.kurzformat = kurzformat;
            this.tintclass = tintclass;
            this.maxblend = maxblend;
        }

        public static TintMeta[] formats = [
            new TintMeta("vanilla", "vn", 32 + 1, typeof(OrthodoxVanillaTint)),
            new TintMeta("vanilla_grass", "vn_g", 32 + 1, typeof(Vanilla_Grass)),
            new TintMeta("vanilla_foliage", "vn_f", 32 + 1, typeof(Vanilla_Foliage)),
            new TintMeta("vanilla_water", "vn_w", 128 + 1, typeof(Vanilla_Water)),
            new TintMeta("fixed", "fx", 32 + 1, typeof(FixedTint)),
            new TintMeta("grid", "gr", 32 + 1, typeof(GridTint)),
        ];

        public static TintMeta GetFormat(string format) {
            return formats.FirstOrDefault(f => format == f.format || format == f.kurzformat);
        }
        public static TintMeta GetFormat(Type tintclass) {
            foreach(var tint in formats) {
                if(tint.tintclass == tintclass) return tint;
            }
            return null;
        }
    }
    public abstract class Tint : GroupElement<Tint> {
        public Tint(GroupManager<Tint> groupManager, string name) : base(groupManager, name) { }


        public abstract uint TintColorFor(ushort biome, short height);
        public virtual uint GetTintedColor(uint baseColor, ushort biome, short height) => ColorMult(baseColor, TintColorFor(biome, height));

        public abstract Blending GetBlendMode();
        public enum Blending { none, heightonly, biomeonly, full }

    }

    public abstract class DynamicTint : Tint {
        public DynamicTint(GroupManager<Tint> groupManager, string name) : base(groupManager, name) {
            On = true;
            Blend = 9;
        }

        private bool on;
        public bool On {
            get => on;
            set {
                if(value == on) return;

                on = value;
                OnLightChange(nameof(On));
            }
        }

        private int blend;
        public int Blend {
            get => blend;
            set {
                if(value == blend) return;

                blend = value;
                OnLightChange(nameof(Blend));
            }
        }
    }
    public abstract class VanillaDynTint : DynamicTint {
        public VanillaDynTint(GroupManager<Tint> groupManager, string name) : base(groupManager, name) {
            TemperatureVariation = 0;
        }

        private double tempHeight;
        public double TemperatureVariation {
            get => tempHeight;
            set {
                if(value == tempHeight) return;

                tempHeight = value;
                Global.Settings.OnLightChange(nameof(TemperatureVariation));
            }
        }
    }

    public class OrthodoxVanillaTint : VanillaDynTint {
        private readonly int version;
        private uint[,] sprite;

        private DatapacksInfo datapacksInfo;

        public OrthodoxVanillaTint(GroupManager<Tint> groupManager, string name, int verion, uint[,] sprite, DatapacksInfo datapacksInfo) : base(groupManager, name) {
            version = verion;
            this.datapacksInfo = datapacksInfo;

            this.sprite = sprite;
            if(sprite == null) this.sprite = new uint[256, 256];
            else if(sprite.GetLength(0) != 256 && sprite.GetLength(0) != 256) throw new Exception();
        }

        public override uint TintColorFor(ushort biome, short height) {
            return datapacksInfo.biomes[biome].GetOrthodox(sprite, height, version, TemperatureVariation);
        }

        public override Blending GetBlendMode() {
            if(!On) return Blending.none;
            if(Blend == 1) return Blending.heightonly;
            if(TemperatureVariation > 0) return Blending.full;
            else return Blending.biomeonly;
        }
    }
    public class HardcodedVanillaTint : VanillaDynTint {
        private readonly string tint;
        private readonly int version;
        private uint[,] sprite;

        private DatapacksInfo datapacksInfo;

        protected HardcodedVanillaTint(GroupManager<Tint> groupManager, string name, string format, int version, uint[,] sprite, DatapacksInfo datapackInfo) : base(groupManager, name) {
            this.tint = format.Replace("vanilla_", "");
            this.version = version;
            datapacksInfo = datapackInfo;

            this.sprite = sprite;
            if(sprite == null) this.sprite = new uint[256, 256];
            else if(sprite.GetLength(0) != 256 && sprite.GetLength(0) != 256) throw new Exception();
        }

        public override uint TintColorFor(ushort biome, short height) {
            return datapacksInfo.biomes[biome].GetVanilla(tint, sprite, sprite, height, version, TemperatureVariation);
        }

        public override Blending GetBlendMode() {
            if(!On) return Blending.none;
            if(Blend == 1) return Blending.heightonly;
            if(TemperatureVariation > 0 && (tint == "grass" || tint == "foliage")) return Blending.full;
            else return Blending.biomeonly;
        }
    }
    public class Vanilla_Grass : HardcodedVanillaTint {
        public Vanilla_Grass(GroupManager<Tint> groupManager, string name, int version, uint[,] sprite, DatapacksInfo datapackInfo) : base(groupManager, name, TintMeta.GetFormat(typeof(Vanilla_Grass)).format, version, sprite, datapackInfo) { }
    }
    public class Vanilla_Foliage : HardcodedVanillaTint {
        public Vanilla_Foliage(GroupManager<Tint> groupManager, string name, int version, uint[,] sprite, DatapacksInfo datapackInfo) : base(groupManager, name, TintMeta.GetFormat(typeof(Vanilla_Foliage)).format, version, sprite, datapackInfo) { }
    }
    public class Vanilla_Water : HardcodedVanillaTint {
        public Vanilla_Water(GroupManager<Tint> groupManager, string name, int version, uint[,] sprite, DatapacksInfo datapackInfo) : base(groupManager, name, TintMeta.GetFormat(typeof(Vanilla_Water)).format, version, sprite, datapackInfo) { }
    }

    public class FixedTint : Tint { // for every block its own tint
        private uint tint, baseColor;
        private bool hasBaseColor;
        public FixedTint(GroupManager<Tint> groupManager, string name, uint tint, uint baseColor) : base(groupManager, name) {
            this.tint = tint;
            this.baseColor = ColorMult(tint, baseColor);

            hasBaseColor = true;
        }
        public override Blending GetBlendMode() => Blending.none;

        public FixedTint(GroupManager<Tint> groupManager, string name, uint tint) : base(groupManager, name) {
            this.tint = tint;
            hasBaseColor = false;
        }

        public override uint TintColorFor(ushort biome, short height) => tint;
        public override uint GetTintedColor(uint baseColor, ushort biome, short height) => hasBaseColor ? this.baseColor : ColorMult(baseColor, tint);
    }

    public class GridTint : DynamicTint {
        private uint[,] sprite;
        private int offset;

        private bool heightparity;
        public GridTint(GroupManager<Tint> groupManager, string name, int offset, uint[,] sprite) : base(groupManager, name) {
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

        public override uint TintColorFor(ushort biome, short height) {
            int x = On ? biome : Global.Settings.DEFBIOME;
            if(x >= sprite.GetLength(0) || x < 0) x = Global.Settings.DEFBIOME;
            int y = Math.Clamp(height - offset, 0, sprite.GetLength(1) - 1);
            return sprite[x, y];
        }



        public override Blending GetBlendMode() {
            if(!On) return Blending.none;
            else if(sprite.GetLength(0) == 1 || Blend == 1) return Blending.heightonly;
            else if(sprite.GetLength(1) == 1 || heightparity) return Blending.biomeonly;
            else return Blending.full;
        }


    }
    public class NullTint : Tint {
        //public static NullTint Tint = new NullTint(), Error = new NullTint();
        public NullTint(GroupManager<Tint> groupManager) : base(groupManager, "nulltint") { }

        public override Blending GetBlendMode() => Blending.none;

        public override uint TintColorFor(ushort biome, short height) => 0xFFFFFFFF;

        public override uint GetTintedColor(uint baseColor, ushort biome, short height) => baseColor;
    }
}
