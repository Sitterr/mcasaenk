﻿using CommunityToolkit.HighPerformance;
using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public interface IChunkInterpreter : IDisposable {
        ushort GetBiome(int cx, int cz, int cy);
        ushort GetBlock(int cx, int cz, int cy);

        short GetHeight(int cx, int cz);
        short GetMotionHeight(int cx, int cz);
        short GetTerrainHeight(int cx, int cz);

        byte GetBlockLight(int cx, int cz, int cy);

        int MaxHeight();
        int MinHeight();

        ushort SingleBlockSection(int i);
        bool ContainsInformation();
        bool ContainsHeightmaps();

        int GetValueFromBitArrayUninterrupted(int index, long[] blockStates, int bits) {
            throw new NotImplementedException();
        }
        int GetValueFromBitArray(int index, ArrTag<long> blockStates, int bits) {
            int indicesPerLong = (int)(64D / bits);
            int blockStatesIndex = index / indicesPerLong;
            int startBit = index % indicesPerLong * bits;
            return (int)(blockStates[blockStatesIndex] >> startBit) & (Global.Pow2(bits) - 1);
        }
    }
}
