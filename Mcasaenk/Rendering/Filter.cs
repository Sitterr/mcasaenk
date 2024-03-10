using Mcasaenk.Rendering.ChunkRenderData;
using System;
using System.Collections.Generic;
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

        public static short Strict(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= -64; h--) {
                var block = data.GetBlock(x, z, h);

                bool isEmpty = IsEmpty(block);
                if(!isEmpty) return (short)h;
            }
            return -64;
        }

        static bool IsEmpty(ushort blockid) {
            if(blockid == ColorMapping.BLOCK_AIR) return true;
            return false;
        }
    }
    public static class WaterFilter {
        public static short Def(IChunkInterpreter data, int x, int z, short startY) {
            for(int h = startY; h >= -64; h--) {
                var block = data.GetBlock(x, z, h);

                bool isWater = IsWater(block);
                if(!isWater) return (short)h;
            }
            return -64;
        }

        public static short List(IChunkInterpreter data, int x, int z, short startY) {
            return Def(data, x, z, startY); // TODO
        }

        static bool IsWater(ushort blockid) {
            if(blockid == ColorMapping.BLOCK_WATER) return true;
            return false;
        }
    }
    public static class AirWaterFilter {
        public static short Filter(IChunkInterpreter data, int x, int z, short startY) {
            startY = AirFilter.List(data, x, z, startY);
            startY = WaterFilter.List(data, x, z, startY);
            return startY;
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
