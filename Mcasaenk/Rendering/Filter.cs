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
        public delegate short filter(IChunkInterpreter data, int x, int z, short startY);

        public static short NullFilter(IChunkInterpreter data, int x, int z, short startY) => startY;
    }
    public static class AirFilter {
        public static short Def(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= -64; h--) {
                var block = data.GetBlock(x, z, h);

                bool isEmpty = IsEmpty(block);
                if(!isEmpty) return (short)h;
            }
            return -64;
        }

        public static short List(IChunkInterpreter data, int x, int z, short startY) {
            return Def(data, x, z, startY); // TODO
        }

        static bool IsEmpty(ushort blockid) {
            if(blockid == Colormap.BLOCK_AIR) return true;
            return false;
        }
    }
    public static class Shade3DFilter {
        static ISet<ushort> ids = new HashSet<ushort>(); 
        static Shade3DFilter() {
            TxtFormatReader.ReadStandartFormat(Resources.ResourceMapping.shade3d_filter, (_, parts) => {
                string name = parts[0];
                if(!name.Contains(":")) name = "minecraft:" + name;
                ushort id = Global.App.Colormap.Block.GetId(name);
                ids.Add(id);
            });
            ids = ids.ToFrozenSet();
        }

        public static short Inner(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= -64; h--) {
                var block = data.GetBlock(x, z, h);

                bool isEmpty = ids.Contains(block);
                if(!isEmpty) return (short)h;
            }
            return -64;
        }

        public static short List(IChunkInterpreter data, int x, int z, short startY) {
            while(true) {
                short a = startY;
                startY = AirFilter.List(data, x, z, startY);
                startY = Inner(data, x, z, startY);
                startY = DepthFilter.List(data, x, z, startY);

                if(a == startY) break;
            }
            return startY;
        }
    }

    public static class DepthFilter {
        public static short Def(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= -64; h--) {
                var block = data.GetBlock(x, z, h);

                bool isWater = IsDepth(block);
                if(!isWater) return (short)h;
            }
            return -64;
        }

        public static short List(IChunkInterpreter data, int x, int z, short startY) {
            return Def(data, x, z, startY); // TODO
        }

        static bool IsDepth(ushort blockid) {
            if(blockid == Global.App.Colormap.depthBlock) return true;
            return false;
        }
    }

    public static class HeightmapFilter {
        public static short FilterAir(IChunkInterpreter data, int x, int z, short startY) {
            return data.GetHeight(x, z);
        }

        public static short FilterWater(IChunkInterpreter data, int x, int z, short startY) {
            short surface_height = data.GetHeight(x, z);
            short floor_height = data.GetTerrainHeight(x, z);
            short motion_height = data.GetMotionHeight(x, z);

            if(motion_height == surface_height) {
                return floor_height;
            }
            return surface_height;
        }
    }
}
