using Mcasaenk.Colormaping;
using Mcasaenk.Nbt;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public interface IChunkInterpreter : IDisposable {

        Colormap Colormap { get; }

        ushort GetBiome(int cx, int cz, int cy);
        ushort GetBlock(int cx, int cz, int cy);

        short GetHeight(int cx, int cz);
        short GetMotionHeight(int cx, int cz);
        short GetTerrainHeight(int cx, int cz);

        byte GetBlockLight(int cx, int cz, int cy);

        ushort SingleBlockSection(int i);
        bool ContainsInformation();
        bool ContainsHeightmaps();

        int GetValueFromBitArrayUninterrupted(int index, ArrTag<long> blockStates, int bits) {
            var i = Math.DivRem((index * bits), 64);

            //double blockStatesIndex = index / (4096D / blockStates.Length);

            int longIndex = i.Quotient;
            int startBit = i.Remainder;

            if(startBit + bits > 64) {
                // get msb from current long, no need to cleanup manually, just fill with 0
                int previous = (int)(blockStates[longIndex] >>> startBit);

                // cleanup pattern for bits from next long
                int remainingClean = (Global.Pow2(startBit + bits - 64) - 1);

                // get lsb from next long
                int next = ((int)blockStates[longIndex + 1]) & remainingClean;
                return (next << 64 - startBit) + previous;
            } else {
                return (int)(blockStates[longIndex] >> startBit) & (Global.Pow2(bits) - 1);
            }
        }
        int GetValueFromBitArray(int index, ArrTag<long> blockStates, int bits) {
            int indicesPerLong = (int)(64D / bits);
            int blockStatesIndex = index / indicesPerLong;
            int startBit = index % indicesPerLong * bits;
            return (int)(blockStates[blockStatesIndex] >> startBit) & (Global.Pow2(bits) - 1);
        }
    }
}
