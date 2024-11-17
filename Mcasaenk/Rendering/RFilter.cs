using Mcasaenk.Colormaping;
using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.Resources;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Global;

namespace Mcasaenk.Rendering {
    //public static class RFilter {
    //    public delegate short filter(IChunkInterpreter data, int x, int z, short startY);

    //    public static short NullFilter(IChunkInterpreter data, int x, int z, short startY) => startY;


    //    public static RFilter.filter AIR_FILTER(IChunkInterpreter data, int renderheight, int maxheight) {
    //        if(Global.Settings.PREFERHEIGHTMAPS && renderheight == maxheight/* && Global.Settings.DIMENSION != Dimension.Type.End*/) return HeightmapFilter.FilterAir;
    //        else return AirFilter.List;
    //    }
    //    public static RFilter.filter DEPTH_FILTER(IChunkInterpreter data, int renderheight, int maxheight) {
    //        if(Global.Settings.PREFERHEIGHTMAPS && renderheight == maxheight && data.Colormap.depth == data.Colormap.BLOCK_WATER/* && Global.Settings.DIMENSION != Dimension.Type.End*/) return HeightmapFilter.FilterWater;
    //        else return DepthFilter.List;
    //    }
    //}
    //public static class AirFilter {
    //    static ISet<ushort> ids = new HashSet<ushort>();
    //    public static short List(IChunkInterpreter data, int x, int z, short startY) {
    //        for(int h = startY; h >= 0; h--) {
    //            if(h % 16 == 15) {
    //                if(IsEmpty(data.SingleBlockSection(h / 16))) {
    //                    h -= 15;
    //                    continue;
    //                }
    //            }
    //            var block = data.GetBlock(x, z, h);

    //            bool isEmpty = IsEmpty(block);
    //            if(!isEmpty) return (short)h;
    //        }
    //        return -1;
    //    }

    //    public static bool IsEmpty(ushort blockid) {
    //        if(blockid == Colormap.INVBLOCK) return true;
    //        if(blockid == Global.App.Colormap.BLOCK_AIR) return true;
    //        if(ids.Contains(blockid)) return true;
    //        return false;
    //    }
    //}
    public static class Shade3DFilter {
        private static List<string> def;
        static Shade3DFilter() {
            def = new List<string>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.shade3d_filter, (_, parts) => {
                def.Add(parts[0].minecraftname());
            });
        }

        public static List<string> Default() => new List<string>(def);
    }

    //public static class DepthFilter {
    //    static ISet<ushort> ids = new HashSet<ushort>();
    //    public static short List(IChunkInterpreter data, int x, int z, short startY) {
    //        for(int h = startY; h >= 0; h--) {
    //            var block = data.GetBlock(x, z, h);

    //            bool isWater = IsDepth(block);
    //            if(!isWater) return (short)h;
    //        }
    //        return -1;
    //    }

    //    public static bool IsDepth(ushort blockid) {
    //        if(blockid == Colormap.INVBLOCK) return true;
    //        if(blockid == Global.App.Colormap.depth) return true;
    //        if(ids.Contains(blockid)) return true;
    //        return false;
    //    }
    //}




    public static class HeightmapFilter {
        public static string WATERBLOCK;
        public static List<string> AIRBLOCKS = [], WATERINVBLOCKS = [];

        static HeightmapFilter() {
            TxtFormatReader.ReadStandartFormat(ResourceMapping.heightmap_blocks, (group, parts) => {
                if(group == "AIR") {
                    AIRBLOCKS.AddRange(parts.Select(p => p.minecraftname()));
                } else if(group == "WATERINV") {
                    WATERINVBLOCKS.AddRange(parts.Select(p => p.minecraftname()));
                } else if(group == "WATER") { 
                    WATERBLOCK = parts[0].minecraftname();
                }
            });
        }


        public static short FilterAir(IChunkInterpreter data, int x, int z, short startY) {
            if(data.ContainsHeightmaps() == false) return startY;
            return data.GetHeight(x, z);
        }


        public static short FilterWater(IChunkInterpreter data, int x, int z, short startY) {
            if(data.ContainsHeightmaps() == false) return startY;

            short surface_height = data.GetHeight(x, z);
            short floor_height = data.GetTerrainHeight(x, z);
            short motion_height = data.GetMotionHeight(x, z);

            if(motion_height == surface_height && data.GetBlock(x, z, floor_height + 1) == data.Colormap.depth) {
                return floor_height;
            }

            return surface_height;
        }
    }
}
