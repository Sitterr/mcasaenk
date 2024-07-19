using Mcasaenk.Rendering.ChunkRenderData;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Global;

namespace Mcasaenk.Rendering {
    public static class Filter {
        public delegate short filter(IChunkInterpreter data, int x, int z, short startY);

        public static short NullFilter(IChunkInterpreter data, int x, int z, short startY) => startY;


        public static Filter.filter AIR_FILTER(int renderheight, int maxheight) {
            if(Global.Settings.PREFERHEIGHTMAPS && renderheight == maxheight/* && Global.Settings.DIMENSION != Dimension.Type.End*/) return HeightmapFilter.FilterAir;
            else return AirFilter.List;
        }
        public static Filter.filter DEPTH_FILTER(int renderheight, int maxheight) {
            if(Global.Settings.PREFERHEIGHTMAPS && renderheight == maxheight && Global.App.Colormap.depth == Global.App.Colormap.BLOCK_WATER/* && Global.Settings.DIMENSION != Dimension.Type.End*/) return HeightmapFilter.FilterWater;
            else return DepthFilter.List;
        }
    }
    public static class AirFilter {
        static ISet<ushort> ids = new HashSet<ushort>();
        public static short List(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= 0; h--) {
                if(h % 16 == 15) {
                    if(IsEmpty(data.SingleBlockSection(h / 16))) {
                        h -= 15;
                        continue;
                    }
                }
                var block = data.GetBlock(x, z, h);

                bool isEmpty = IsEmpty(block);
                if(!isEmpty) return (short)h;
            }
            return -1;
        }

        public static bool IsEmpty(ushort blockid) {
            if(blockid == Colormap.INVBLOCK) return true;
            if(blockid == Global.App.Colormap.BLOCK_AIR) return true;
            if(ids.Contains(blockid)) return true;
            return false;
        }
    }
    public static class Shade3DFilter {
        static ISet<ushort> ids; 
        public static void ReInit(Colormap colormap) {
            ids = new HashSet<ushort>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.shade3d_filter, (_, parts) => {
                string name = parts[0].minecraftname();
                ushort id = colormap.Block.GetId(name);
                ids.Add(id);
            });
            ids = ids.ToFrozenSet();
        }

        public static bool IsShade3D(ushort blockid) {
            if(AirFilter.IsEmpty(blockid)) return true;
            if(DepthFilter.IsDepth(blockid)) return true;
            if(ids.Contains(blockid)) return true;
            return false;
        }
    }

    public static class DepthFilter {
        static ISet<ushort> ids = new HashSet<ushort>();
        public static short List(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= 0; h--) {
                var block = data.GetBlock(x, z, h);

                bool isWater = IsDepth(block);
                if(!isWater) return (short)h;
            }
            return -1;
        }

        public static bool IsDepth(ushort blockid) {
            if(blockid == Colormap.INVBLOCK) return true;
            if(blockid == Global.App.Colormap.depth) return true;
            if(ids.Contains(blockid)) return true;
            return false;
        }
    }




    public static class HeightmapFilter {
        public static short FilterAir(IChunkInterpreter data, int x, int z, short startY) {
            if(data.ContainsHeightmaps() == false) return AirFilter.List(data, x, z, startY);
            return data.GetHeight(x, z);
        }

        public static short FilterWater(IChunkInterpreter data, int x, int z, short startY) {
            if(data.ContainsHeightmaps() == false) return DepthFilter.List(data, x, z, startY);

            short surface_height = data.GetHeight(x, z);
            short floor_height = data.GetTerrainHeight(x, z);
            short motion_height = data.GetMotionHeight(x, z);

            if(motion_height == surface_height && data.GetBlock(x, z, floor_height + 1) == Global.App.Colormap.depth) {
                return floor_height;
            }

            return surface_height;
        }
    }
}
