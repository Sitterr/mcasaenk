using Mcasaenk.Nbt;
using Mcasaenk.Rendering.ChunkRenderData._117;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public unsafe class ChunkInterpreterStartingPoint {

        public static IChunkInterpreter Read(Tile tile, byte* pointer) {
            return Settings.NBT_READING_METHOD switch {
                NbtReadingMethod.Standard => ReadNbtDeterministically(pointer),
                NbtReadingMethod.Lazy117 => ReadNbtCherrypick117(pointer, tile.GetOrigin().generateTilePool),

                _ => ReadNbtDeterministically(pointer),
            };
        }

        private static IChunkInterpreter ReadNbtDeterministically(byte* pointer) {
            using var decompressedStream = GetDecompressedStream(pointer);
            if(decompressedStream == null) return null;

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

        private static IChunkInterpreter ReadNbtCherrypick117(byte* pointer, GenerateTilePool pool) { // prob deprecated
            using var decompressedStream = GetDecompressedStream(pointer);
            if(decompressedStream == null) return null;

            var lazyreader = new CherrypickNbtReader(decompressedStream);
            return new ChunkDataCherrypickInterpreter117(pool, lazyreader);
        }







        static ZLibStream GetDecompressedStream(byte* pointer) {
            if(pointer == null) return null;
            int actualsize = pointer[0] << 24 | pointer[1] << 16 | pointer[2] << 8 | pointer[3];
            if(actualsize == 0) return null;

            return new ZLibStream(new UnmanagedMemoryStream(pointer + 5, actualsize - 1), CompressionMode.Decompress);
        }
    }
}
