using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public interface IChunkInterpreter : IDisposable {
        ushort GetBiome(int cx, int cz, int cy, int i);
        ushort GetBlock(int cx, int cz, int cy, int i);

        short GetHeight(int cx, int cz);
        short GetTerrainHeight(int cx, int cz);

        bool CanSkipSection(int i);
        bool ContainsInformation();
        bool ContainsHeightmaps();
    }
}
