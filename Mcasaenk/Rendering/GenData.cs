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
                this.columns[i] = new GenDataColumn(rawData.columns[i], false);
            }
            depthColumn = this.columns[rawData.columns.Length] = new GenDataColumn(rawData.depthColumn, true);
            this.topblocks = rawData.topblocks;

            this.depthblock = depthblock;
            this.istemp = false;
        }
        private GenData(GenData genData) {
            this.chunkisscreenshotable = genData.chunkisscreenshotable;
            this.columns = new GenDataColumn[genData.columns.Length];
            for(int i = 0; i < columns.Length; i++) {
                this.columns[i] = new GenDataColumn(genData.columns[i]);
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

    public class GenDataColumn {
        public bool ContainsInfo(int i) => heights[i] != default || biomeIds10_groupIds6[i] != default || color24_light4_shade4[i] != default;

        public uint[] color24_light4_shade4;
        public ushort[] biomeIds10_groupIds6;
        public short[] heights, depths;

        private readonly bool maybedepth;

        public GenDataColumn(RawDataColumn rawcolumn, bool depthcolumn) {
            this.color24_light4_shade4 = rawcolumn.color24_light4_none4;
            this.biomeIds10_groupIds6 = rawcolumn.biomeIds10_groupIds6;
            this.heights = rawcolumn.heights;
            this.depths = rawcolumn.depths;

            this.maybedepth = depthcolumn && depths != null;
        }

        public GenDataColumn(GenDataColumn gencolumn) {
            this.color24_light4_shade4 = gencolumn.color24_light4_shade4;
            this.biomeIds10_groupIds6 = gencolumn.biomeIds10_groupIds6;
            this.heights = gencolumn.heights;
            this.depths = gencolumn.depths;

            this.maybedepth = gencolumn.maybedepth;
        }
        
        public uint ActColor(int i) => 0xFF000000 | (color24_light4_shade4[i] >> 8);
        public bool IsDepth(int i) => maybedepth && depths[i] != 0;
        public uint Color(int i) => IsDepth(i) ? Global.App.Colormap.BaseColor(Global.App.Colormap.depth) : ActColor(i);
        public byte BlockLight(int i) => (byte)((color24_light4_shade4[i] & 0x000000FF) >> 4);
        public byte Shade(int i) => (byte)((color24_light4_shade4[i] & 0x0000000F));
        public void set_shade(int i, byte shade) => color24_light4_shade4[i] = (color24_light4_shade4[i] & 0xFFFFFFF0) + shade;
        public ushort BiomeId(int i) => (ushort)(biomeIds10_groupIds6[i] >> 6);
        public int GroupId(int i) => biomeIds10_groupIds6[i] & 0b0000000000111111;
        public short Height(int i) => heights[i];
        public short TerrHeight(int i) => TerrHeight(IsDepth(i), Height(i), Depth(i));
        public short Depth(int i) => depths != null ? Math.Max(depths[i], (short)1) : (short)1;

        public static short TerrHeight(bool isdepth, short height, short depth) => isdepth ? (short)(height - depth) : height;
        public static short Depth(bool isdepth, short depth) => !isdepth ? Math.Max(depth, (short)1) : (short)1;
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
            columns = new RawDataColumn[Math.Max(0, Global.Settings.TRANSPARENTLAYERS - 1)];
            for(int i = 0; i < columns.Length; i++) {
                columns[i] = new RawDataColumn(true);
            }
            depthColumn = new RawDataColumn(Global.Settings.TRANSPARENTLAYERS > 0);

            if(Global.Settings.BLOCKINFO) {
                topblocks = new ushort[512 * 512];
                Array.Fill(topblocks, Colormap.INVBLOCK);
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
    public class RawDataColumn {
        // for water technically this uses 1 more byte than the old method(2byte blockid, 1byte light), but it abstracts like half-transparent blocks      
        public bool ContainsInfo(int i) => heights[i] != default || biomeIds10_groupIds6[i] != default || color24_light4_none4[i] != default;
        
        public uint[] color24_light4_none4;
        public ushort[] biomeIds10_groupIds6;
        public short[] heights, depths;
        public byte[] shadeValues; // 4bit

        public RawDataColumn(bool candepth = true) {
            color24_light4_none4 = new uint[512 * 512];
            biomeIds10_groupIds6 = new ushort[512 * 512];
            heights = new short[512 * 512];
            if(candepth) depths = new short[512 * 512];
            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) {
                shadeValues = new byte[512 * 512 * ShadeConstants.GLB.blockReachLenMax];
            }
        }

        public static ushort BiomeGroupMaker(ushort biomeid, byte groupid) {
            return (ushort)((biomeid << 6) + (groupid & 0b00111111));
        }

        public static uint ColorLightMaker(uint color, byte light) {
            return (uint)((color << 8) + (light << 4));
        }
    }
}
