using Mcasaenk.Nbt;
using Mcasaenk.Rendering.ChunkRenderData._117;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Buffers;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public unsafe class ChunkInterpreterStartingPoint {

        public static IChunkInterpreter Read(Stream pointer) {
            return ReadNbtDeterministically(pointer);
        }

        private static IChunkInterpreter ReadNbtDeterministically(Stream pointer) {
            if(pointer == null) return null;
            using var zlip = new ZLibStream(pointer, CompressionMode.Decompress);
            using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared, 512);

            var nbtreader = new NbtReader(decompressedStream);
            bool error = nbtreader.TryRead(out var _g);
            try {
                var globaltag = (CompoundTag)_g;

                int version = (NumTag<int>)globaltag["DataVersion"];
                IChunkInterpreter chunkreader;
                if(version > 0) {
                    chunkreader = new ChunkDataInterpreter117(globaltag, error);
                } else throw new Exception();

                return chunkreader;
            }
            catch {
                throw new Exception("chunk version is strange");
            }          
        }
    }
}
