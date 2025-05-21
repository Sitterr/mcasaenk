using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using Mcasaenk.Shaders;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.TextFormatting;

namespace Mcasaenk.Rendering {

    public unsafe class GenData : IDisposable {
        public GenDataColumn[] columns; // including depthColumn
        public readonly int[] chunkisscreenshotable;
        public ushort[] topblocks;

        public GenDataColumn depthColumn;

        private ShaderTexture2D texture;
        short[] texturedata;
        private void FreeTextureData() {
            if(texturedata != default) {      
                ArrayPool<short>.Shared.Return(texturedata, true);
                texturedata = default;
            }
        }
        //private short[] texturedata;
        private bool textureupdate = false, freed = false, disposed = false;

        public GenData(RawData rawData) {
            this.chunkisscreenshotable = rawData.chunkisscreenshotable;
            this.columns = new GenDataColumn[rawData.columns.Length + 1];

            for(int i = 0; i < rawData.columns.Length; i++) {
                this.columns[i] = new GenDataColumn(rawData.columns[i], false);
            }
            depthColumn = this.columns[rawData.columns.Length] = new GenDataColumn(rawData.depthColumn, true);

            //texturedata = Marshal.AllocHGlobal(columns.Length * 512 * 512 * 4 * sizeof(short));
            texturedata = ArrayPool<short>.Shared.Rent(columns.Length * 512 * 512 * 4);
            UpdateTextureData();
        }

        public void Dispose() {
            if(!disposed) {
                texture?.Dispose();
                if(!freed) {
                    foreach(var col in columns) col.FreeData();              
                }
                FreeTextureData();

                disposed = true;
            }
        }

        public bool IsChunkScreenshotable(int x, int z) {
            return ((chunkisscreenshotable[z] >> x) & 1) == 1;
        }



        public void FreeData() {
            foreach(var col in columns) col.FreeData();
            if(textureupdate == false) {
                FreeTextureData();
            }
            freed = true;
        }
        public ShaderTexture2D GetTexture() {
            if(texture == null) texture = ShaderTexture2D.CreateRGBA16i_Array(columns.Length, 512, 512);
            if(textureupdate && texturedata != default) {
                texture.Data(texturedata);

                if(freed) {
                    FreeTextureData();
                }
            }
            textureupdate = false;
            return texture;
        }
        public void UpdateTextureData() {
            if(texturedata == null) return;
            for(int i = 0; i < columns.Length; i++) columns[i].UpdateTextureData(texturedata.AsSpan().Slice(i * 512 * 512 * 4));
            //for(int i = 0; i < columns.Length; i++) columns[i].UpdateTextureData(MemoryMarshal.Cast<byte, short>(new Span<byte>((byte*)texturedata.ToPointer(), columns.Length * 512 * 512 * 4 * sizeof(short))).Slice(i * 512 * 512 * 4));
            textureupdate = true;
        }
    }
    public class GenDataColumn { // 64bit
        private bool maybedepth;
        public short[] heights, depths15_lightfrombottom1;
        public ushort[] blockIds;
        public ushort[] biomeIds8_light4_shade4;

        private GenDataTileMap pool;

        public GenDataColumn(RawDataColumn rawcolumn, bool depthcolumn) {
            this.pool = rawcolumn.pool;

            this.heights = rawcolumn.heights;
            this.depths15_lightfrombottom1 = rawcolumn.depths15_lightfrombottom1;
            this.blockIds = rawcolumn.blockIds;
            this.biomeIds8_light4_shade4 = rawcolumn.biomeIds8_light4_none4;

            this.maybedepth = depthcolumn && depths15_lightfrombottom1 != null;
        }

        public void FreeData() {
            if(heights != null) pool.heightPool.Return(heights, true);
            if(depths15_lightfrombottom1 != null) pool.depthsPool.Return(depths15_lightfrombottom1, true);
            if(blockIds != null) pool.blockIdsPool.Return(blockIds, true);
            if(biomeIds8_light4_shade4 != null) pool.biomeIds8_light4_shade4Pool.Return(biomeIds8_light4_shade4, true);

            heights = null;
            depths15_lightfrombottom1 = null;
            blockIds = null;
            biomeIds8_light4_shade4 = null;
        }
        public void UpdateTextureData(Span<short> texturedata) {
            if(heights == null) return;
            for(int i = 0; i < 512 * 512; i++) {
                if(heights == null) return;
                texturedata[i * 4 + 0] = heights[i];
                texturedata[i * 4 + 1] = depths15_lightfrombottom1 != null ? depths15_lightfrombottom1[i] : (short)0;
                texturedata[i * 4 + 2] = (short)blockIds[i];
                texturedata[i * 4 + 3] = (short)biomeIds8_light4_shade4[i];
            }
        }



        public bool IsDepth(int i) => maybedepth && depths15_lightfrombottom1[i] != 0;
        public uint Color(int i) => IsDepth(i) ? Global.App.Colormap.BaseColor(BlockManager.depth) : ActColor(i);
        public short Height(int i) => heights[i];
        public short TerrHeight(int i) => TerrHeight(IsDepth(i), Height(i), Depth(i));
        public short Depth(int i) => depths15_lightfrombottom1 != null ? (short)Math.Max(depths15_lightfrombottom1[i] >> 1, 1) : (short)1;
        public static short TerrHeight(bool isdepth, short height, short depth) => isdepth ? (short)(height - depth) : height;
        public static short Depth(bool isdepth, short depth) => !isdepth ? Math.Max(depth, (short)1) : (short)1;


        public byte BlockLight(int i) => (byte)((biomeIds8_light4_shade4[i] & 0x00FF) >> 4);
        public byte Shade(int i) => (byte)((biomeIds8_light4_shade4[i] & 0x000F));
        public void set_shade(int i, byte shade) => biomeIds8_light4_shade4[i] = (ushort)((biomeIds8_light4_shade4[i] & 0xFFF0) + shade);
        public ushort BiomeId(int i) => (ushort)(biomeIds8_light4_shade4[i] >> 8);
        public Tint Tint(int i) => Global.App.Colormap.TintManager.GetBlockVal(TopBlockId(i));
        public Filter Filter(int i) => Global.App.Colormap.FilterManager.GetBlockVal(blockIds[i]);
        public bool NeedShade(int i) => !Global.App.Colormap.noShades.Contains(blockIds[i]);
        public uint ActColor(int i) => IsDepth(i) ? Global.App.Colormap.FullColor(blockIds[i], BiomeId(i), TerrHeight(i)) : Global.App.Colormap.BaseColor(blockIds[i]);

        public ushort BlockId(int i) => blockIds[i];
        public ushort TopBlockId(int i) => IsDepth(i) ? BlockManager.depth : blockIds[i];

        public bool ContainsInfo(int i) => heights != null;
        //public bool ContainsInfo(int i) => heights[i] != default || blockIds[i] != default || biomeIds8_light4_shade4[i] != default;
    }


    public class RawData {
        public RawDataColumn[] columns;
        public RawDataColumn depthColumn;
        public int[] chunkisscreenshotable; // bit

        // disolves in the gendata stage
        public byte[] shadeFrame; // 4bit

        public RawData(GenDataTileMap pool) {
            chunkisscreenshotable = new int[32 * 32 / 32];

            int colcount = Global.App.Colormap.FilterManager.AreThereHalfTransp() ? Math.Max(0, Global.Settings.TRANSPARENTLAYERS - 1) : 0;

            columns = new RawDataColumn[colcount];
            for(int i = 0; i < columns.Length; i++) {
                columns[i] = new RawDataColumn(pool, true);
            }
            depthColumn = new RawDataColumn(pool, Global.Settings.TRANSPARENTLAYERS > 0);

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
        public short[] heights, depths15_lightfrombottom1;
        public byte[] shadeValues; // 4bit
        public ushort[] blockIds;
        public ushort[] biomeIds8_light4_none4;

        public GenDataTileMap pool;

        public RawDataColumn(GenDataTileMap pool, bool candepth = true) {
            this.pool = pool;

            heights = pool.heightPool.Rent(512 * 512);
            blockIds = pool.blockIdsPool.Rent(512 * 512);
            biomeIds8_light4_none4 = pool.biomeIds8_light4_shade4Pool.Rent(512 * 512);
            if(candepth) depths15_lightfrombottom1 = pool.depthsPool.Rent(512 * 512);

            //heights = new short[512 * 512];
            //blockIds = new ushort[512 * 512];
            //biomeIds8_light4_none4 = new ushort[512 * 512];
            //if(candepth) depths15_lightfrombottom1 = new short[512 * 512];

            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) {
                shadeValues = new byte[512 * 512 * ShadeConstants.GLB.blockReachLenMax];
            }
        }
        
        public void Input(int i, ushort blockid, byte light, bool lightfrombottom, ushort biomeid, short height, short depth, int r_absortion, ref byte r_relvisost) {
            blockIds[i] = blockid;
            biomeIds8_light4_none4[i] = BiomeLightMaker(biomeid, light);
            heights[i] = height;
            if(depths15_lightfrombottom1 != null) depths15_lightfrombottom1[i] = (short)((depth << 1) | Convert.ToInt16(lightfrombottom));
        }
        static ushort BiomeLightMaker(ushort biomeid, byte light) {
            return (ushort)((biomeid << 8) + (light << 4));
        }

        public bool ContainsInfo(int i) => heights[i] != default || blockIds[i] != default || biomeIds8_light4_none4[i] != default;
    }

}
