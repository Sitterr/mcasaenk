using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Rendering {

    public class GenData { // 72bit
        public GenDataColumn[] columns; // including depthColumn
        public readonly int[] chunkisscreenshotable;
        public ushort[] topblocks;

        public GenDataColumn depthColumn;
        private bool istemp, temp_waterRelief = false;

        private ushort depthblock;
        public GenData(RawData rawData, ushort depthblock) {
            this.chunkisscreenshotable = rawData.chunkisscreenshotable;
            this.columns = new GenDataColumn[rawData.columns.Length + 1];

            for(int i = 0; i < rawData.columns.Length; i++) {
                this.columns[i] = GenDataColumn.Suitable(rawData.columns[i], false);
            }
            depthColumn = this.columns[rawData.columns.Length] = GenDataColumn.Suitable(rawData.depthColumn, true);

            this.topblocks = rawData.topblocks;

            this.depthblock = depthblock;
            this.istemp = false;
        }
        private GenData(GenData genData) {
            this.chunkisscreenshotable = genData.chunkisscreenshotable;
            this.columns = new GenDataColumn[genData.columns.Length];
            for(int i = 0; i < columns.Length; i++) {
                if(genData.columns[i] is GenDataColumnColor c) this.columns[i] = new GenDataColumnColor(c);
                else if(genData.columns[i] is GenDataColumnId id) this.columns[i] = new GenDataColumnId(id);
            }
            this.depthColumn = this.columns.Last();
            this.depthblock = genData.depthblock;
            this.istemp = true;
        }

        public void SetTemporal_WaterRelief() {
            if(istemp == false) throw new Exception();
            if(depthColumn.depths == null) return;

            temp_waterRelief = true;
            var old = depthColumn.depths;
            depthColumn.depths = ArrayPool<short>.Shared.Rent(512 * 512);
            for(int i = 0; i < 512 * 512; i++) depthColumn.depths[i] = old[i];
        }

        public void DisposeTemporal() {
            if(istemp == false) throw new Exception();
            if(temp_waterRelief) ArrayPool<short>.Shared.Return(depthColumn.depths);
        }

        public GenData GetTempInstance() {
            return new GenData(this);
        }
        public bool IsChunkScreenshotable(int x, int z) {
            return ((chunkisscreenshotable[z] >> x) & 1) == 1;
        }
    }

    public abstract class GenDataColumn {
        public abstract bool ContainsInfo(int i);

        public short[] heights, depths;
        public byte[] relvis; // DEPRECATED KEEPING IT IF I THINK OF SOMETHING GENIUSLY
        private readonly bool maybedepth;

        public GenDataColumn(RawDataColumn rawcolumn, bool depthcolumn) {
            this.heights = rawcolumn.heights;
            this.depths = rawcolumn.depths;
            this.maybedepth = depthcolumn && depths != null;
            this.relvis = rawcolumn.relvis;
        }
        public GenDataColumn(GenDataColumn gencolumn) {
            this.heights = gencolumn.heights;
            this.depths = gencolumn.depths;
            this.maybedepth = gencolumn.maybedepth;
            this.relvis = gencolumn.relvis;
        }
        public static GenDataColumn Suitable(RawDataColumn rawcol, bool depthcolumn) {
            if(rawcol is RawDataColumnColor c) return new GenDataColumnColor(c, depthcolumn);
            else if(rawcol is RawDataColumnId id) return new GenDataColumnId(id, depthcolumn);
            return null;
        }

        public bool IsDepth(int i) => maybedepth && depths[i] != 0;
        public abstract byte BlockLight(int i);
        public abstract byte Shade(int i);
        public abstract void set_shade(int i, byte shade);
        public abstract ushort BiomeId(int i);

        public abstract Filter Filter(int i);
        public abstract Tint Tint(int i);
        public abstract bool NeedShade(int i);
        public abstract uint ActColor(int i);
        public uint Color(int i) => IsDepth(i) ? Global.App.Colormap.BaseColor(Global.App.Colormap.depth) : ActColor(i);

        public short Height(int i) => heights[i];
        public short TerrHeight(int i) => TerrHeight(IsDepth(i), Height(i), Depth(i));
        public short Depth(int i) => depths != null ? Math.Max(depths[i], (short)1) : (short)1;

        public static short TerrHeight(bool isdepth, short height, short depth) => isdepth ? (short)(height - depth) : height;
        public static short Depth(bool isdepth, short depth) => !isdepth ? Math.Max(depth, (short)1) : (short)1;
    }

    public class GenDataColumnColor : GenDataColumn {
        public uint[] color24_light4_shade4;
        public ushort[] biomeIds10_groupIds6;

        public GenDataColumnColor(RawDataColumnColor rawcolumn, bool depthcolumn) : base(rawcolumn, depthcolumn) {
            this.color24_light4_shade4 = rawcolumn.color24_light4_none4;
            this.biomeIds10_groupIds6 = rawcolumn.biomeIds10_groupIds6;
        }
        public GenDataColumnColor(GenDataColumnColor gencolumn) : base(gencolumn) { 
            this.color24_light4_shade4 = gencolumn.color24_light4_shade4;
            this.biomeIds10_groupIds6 = gencolumn.biomeIds10_groupIds6;
        }

        public override byte BlockLight(int i) => (byte)((color24_light4_shade4[i] & 0x000000FF) >> 4);
        public override byte Shade(int i) => (byte)((color24_light4_shade4[i] & 0x0000000F));
        public override void set_shade(int i, byte shade) => color24_light4_shade4[i] = (color24_light4_shade4[i] & 0xFFFFFFF0) + shade;
        public override ushort BiomeId(int i) => (ushort)(biomeIds10_groupIds6[i] >> 6);
        public override Tint Tint(int i) => Global.App.Colormap.Grouping.GetGroup(GroupId(i)).tint;
        public override Filter Filter(int i) => Global.App.Colormap.Grouping.GetGroup(GroupId(i)).filter;
        public override bool NeedShade(int i) => Global.App.Colormap.Grouping.GetGroup(GroupId(i)).shade;
        public override uint ActColor(int i) => 0xFF000000 | (color24_light4_shade4[i] >> 8);

        public int GroupId(int i) => biomeIds10_groupIds6[i] & 0b0000000000111111;

        public override bool ContainsInfo(int i) => heights[i] != default || color24_light4_shade4[i] != default || biomeIds10_groupIds6[i] != default;
    }

    public class GenDataColumnId : GenDataColumn {
        public ushort[] blockIds;
        public ushort[] biomeIds8_light4_shade4;

        public GenDataColumnId(RawDataColumnId rawcolumn, bool depthcolumn) : base(rawcolumn, depthcolumn) {
            this.blockIds = rawcolumn.blockIds;
            this.biomeIds8_light4_shade4 = rawcolumn.biomeIds8_light4_none4;
        }
        public GenDataColumnId(GenDataColumnId gencolumn) : base(gencolumn) {
            this.blockIds = gencolumn.blockIds;
            this.biomeIds8_light4_shade4 = gencolumn.biomeIds8_light4_shade4;
        }

        public override byte BlockLight(int i) => (byte)((biomeIds8_light4_shade4[i] & 0x00FF) >> 4);
        public override byte Shade(int i) => (byte)((biomeIds8_light4_shade4[i] & 0x000F));
        public override void set_shade(int i, byte shade) => biomeIds8_light4_shade4[i] = (ushort)((biomeIds8_light4_shade4[i] & 0xFFF0) + shade);
        public override ushort BiomeId(int i) => (ushort)(biomeIds8_light4_shade4[i] >> 8);
        public override Tint Tint(int i) => Global.App.Colormap.TintManager.GetBlockVal(TopBlockId(i));
        public override Filter Filter(int i) => Global.App.Colormap.FilterManager.GetBlockVal(blockIds[i]);
        public override bool NeedShade(int i) => !Global.App.Colormap.noShades.Contains(blockIds[i]);
        public override uint ActColor(int i) => IsDepth(i) ? Global.App.Colormap.FullColor(blockIds[i], BiomeId(i), TerrHeight(i)) : Global.App.Colormap.BaseColor(blockIds[i]);

        public ushort BlockId(int i) => blockIds[i];
        public ushort TopBlockId(int i) => IsDepth(i) ? Global.App.Colormap.depth : blockIds[i];


        public override bool ContainsInfo(int i) => heights[i] != default || blockIds[i] != default || biomeIds8_light4_shade4[i] != default;
    }


    public class RawData {
        public RawDataColumn[] columns;
        public RawDataColumn depthColumn;
        public int[] chunkisscreenshotable; // bit
        public ushort[] topblocks;

        // disolves in the gendata stage
        public byte[] shadeFrame; // 4bit

        public RawData() {
            chunkisscreenshotable = new int[32 * 32 / 32];

            int colcount = Global.App.Colormap.FilterManager.AreThereHalfTransp() ? Math.Max(0, Global.Settings.TRANSPARENTLAYERS - 1) : 0;

            if(Global.Settings.DATASTORAGEMODEL == GenDataModel.ID) {
                columns = new RawDataColumnId[colcount];
                for(int i = 0; i < columns.Length; i++) {
                    columns[i] = new RawDataColumnId(true);
                }
                depthColumn = new RawDataColumnId(Global.Settings.TRANSPARENTLAYERS > 0);
            } else if(Global.Settings.DATASTORAGEMODEL == GenDataModel.COLOR) {
                columns = new RawDataColumn[colcount];
                for(int i = 0; i < columns.Length; i++) {
                    columns[i] = new RawDataColumnColor(true);
                }
                depthColumn = new RawDataColumnColor(Global.Settings.TRANSPARENTLAYERS > 0);

                if(Global.Settings.BLOCKINFO) {
                    topblocks = new ushort[512 * 512];
                    Array.Fill(topblocks, Colormap.INVBLOCK);
                }
            }

            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) {
                shadeFrame = new byte[(ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512)];
            }
        }

        public void SetChunkScreenshotable(int x, int z, bool val) {
            if(val) chunkisscreenshotable[z] |= (1 << x);
            else chunkisscreenshotable[z] &= ~(1 << x);
        }
    }
    public abstract class RawDataColumn {    
        public short[] heights, depths;
        public byte[] shadeValues; // 4bit

        public byte[] relvis; // DEPRECATED KEEPING IT IF I THINK OF SOMETHING GENIUSLY

        public RawDataColumn(bool candepth = true) {
            heights = new short[512 * 512];
            if(candepth) depths = new short[512 * 512];
            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) {
                shadeValues = new byte[512 * 512 * ShadeConstants.GLB.blockReachLenMax];
            }
            if(Global.Settings.DarfRelVisPrecompute() && Global.Settings.TRANSPARENTLAYERS > 1) relvis = new byte[512 * 512];
        }
        public abstract bool ContainsInfo(int i);
        
        protected void Input(int i, short height, short depth, int absortion, ref byte relvisostatuk) {
            heights[i] = height;
            if(depths != null) depths[i] = depth;
            if(relvis != null) {
                relvis[i] = (byte)(relvisostatuk * (1 - Math.Pow(1 - (absortion / 15d), Math.Max((short)1, depth))));
                relvisostatuk -= relvis[i];
            }
        }
    }
    public class RawDataColumnColor : RawDataColumn {
        public uint[] color24_light4_none4;
        public ushort[] biomeIds10_groupIds6;
        public RawDataColumnColor(bool candepth = true) : base(candepth) {
            color24_light4_none4 = new uint[512 * 512];
            biomeIds10_groupIds6 = new ushort[512 * 512];
        }
        public override bool ContainsInfo(int i) => heights[i] != default || biomeIds10_groupIds6[i] != default || color24_light4_none4[i] != default;


        public void Input(int i, uint color, byte light, ushort biomeid, byte groupid, short height, short depth, int r_absortion, ref byte r_relvisost) {
            color24_light4_none4[i] = ColorLightMaker(color, light);
            biomeIds10_groupIds6[i] = BiomeGroupMaker(biomeid, groupid);
            base.Input(i, height, depth, r_absortion, ref r_relvisost);
        }

        static ushort BiomeGroupMaker(ushort biomeid, byte groupid) {
            return (ushort)((biomeid << 6) + (groupid & 0b00111111));
        }

        static uint ColorLightMaker(uint color, byte light) {
            return (uint)((color << 8) + (light << 4));
        }
    }   
    public class RawDataColumnId : RawDataColumn {
        public ushort[] blockIds;
        public ushort[] biomeIds8_light4_none4;

        public RawDataColumnId(bool candepth = true) : base(candepth) { 
            blockIds = new ushort[512 * 512];
            biomeIds8_light4_none4 = new ushort[512 * 512];
        }
        public override bool ContainsInfo(int i) => heights[i] != default || blockIds[i] != default || biomeIds8_light4_none4[i] != default;

        public void Input(int i, ushort blockid, byte light, ushort biomeid, short height, short depth, int r_absortion, ref byte r_relvisost) {
            blockIds[i] = blockid;
            biomeIds8_light4_none4[i] = BiomeLightMaker(biomeid, light);
            base.Input(i, height, depth, r_absortion, ref r_relvisost);
        }

        static ushort BiomeLightMaker(ushort biomeid, byte light) {
            return (ushort)((biomeid << 8) + (light << 4));
        }
    }

}
