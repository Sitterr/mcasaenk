using Mcasaenk.Rendering.ChunkRenderData;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering {
    public static class Filter {
        public delegate short filter(IChunkInterpreter data, int x, int z, short startY, short minY);

        public static short NullFilter(IChunkInterpreter data, int x, int z, short startY, short minY) => startY;


        public static Filter.filter AIR_FILTER(int renderheight, int maxheight) {
            if(Global.Settings.PREFERHEIGHTMAPS && (renderheight >= maxheight) && Global.Settings.DIMENSION != Dimension.Type.End) return HeightmapFilter.FilterAir;
            else return AirFilter.List;
        }
        public static Filter.filter DEPTH_FILTER(int renderheight, int maxheight) {
            if(Global.Settings.PREFERHEIGHTMAPS && (renderheight >= maxheight) && Global.App.Colormap.depth == Colormap.BLOCK_WATER && Global.Settings.DIMENSION != Dimension.Type.End) return HeightmapFilter.FilterWater;
            else return DepthFilter.List;
        }
    }
    public static class AirFilter {
        public static short Def(IChunkInterpreter data, int x, int z, short startY, short minY) {
            for(int h = startY; h >= minY; h--) {
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
            return minY;

        }

        public static short List(IChunkInterpreter data, int x, int z, short startY, short minY) {
            return Def(data, x, z, startY, minY); // TODO
        }

        static bool IsEmpty(ushort blockid) {
            if(blockid == Colormap.BLOCK_AIR || blockid == Colormap.INVBLOCK) return true;
            return false;
        }
    }
    public static class Shade3DFilter {
        static ISet<ushort> ids; 
        public static void ReInit(Colormap colormap) {
            ids = new HashSet<ushort>();
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.shade3d_filter, (_, parts) => {
                string name = parts[0];
                if(!name.Contains(":")) name = "minecraft:" + name;
                ushort id = colormap.Block.GetId(name);
                ids.Add(id);
            });
            ids = ids.ToFrozenSet();
        }

        public static short Inner(IChunkInterpreter data, int x, int z, short startY, short minY) {
            for(int h = startY; h >= minY; h--) {
                var block = data.GetBlock(x, z, h);

                bool isEmpty = ids.Contains(block);
                if(!isEmpty) return (short)h;
            }
            return -64;
        }

        public static short List(IChunkInterpreter data, int x, int z, short startY, short minY) {
            while(true) {
                short a = startY;
                startY = AirFilter.List(data, x, z, startY, minY);
                startY = Inner(data, x, z, startY, minY);
                startY = DepthFilter.List(data, x, z, startY, minY);

                if(a == startY) break;
            }
            return startY;
        }
    }

    public static class DepthFilter {
        public static short Def(IChunkInterpreter data, int x, int z, short startY, short minY) {
            for(int h = startY; h >= minY; h--) {
                var block = data.GetBlock(x, z, h);

                bool isWater = IsDepth(block);
                if(!isWater) return (short)h;
            }
            return minY;
        }

        public static short List(IChunkInterpreter data, int x, int z, short startY, short minY) {
            return Def(data, x, z, startY, minY); // TODO
        }

        static bool IsDepth(ushort blockid) {
            if(blockid == Global.App.Colormap.depth) return true;
            return false;
        }
    }

    public static class HeightmapFilter {
        public static short FilterAir(IChunkInterpreter data, int x, int z, short startY, short minY) {
            if(data.ContainsHeightmaps() == false) return AirFilter.Def(data, x, z, startY, minY);
            short hm = data.GetHeight(x, z);
            hm = AirFilter.Def(data, x, z, hm, minY);
            return hm;
        }

        public static short FilterWater(IChunkInterpreter data, int x, int z, short startY, short minY) {
            if(data.ContainsHeightmaps() == false) return DepthFilter.Def(data, x, z, startY, minY);

            short surface_height = data.GetHeight(x, z);
            short floor_height = data.GetTerrainHeight(x, z);
            short motion_height = data.GetMotionHeight(x, z);

            if(motion_height == surface_height) {
                return floor_height;
            }

            short hm = surface_height;
            hm = AirFilter.Def(data, x, z, hm, minY);
            return hm;
        }
    }
}
