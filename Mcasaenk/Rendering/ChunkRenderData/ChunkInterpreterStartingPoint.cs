using System.Buffers;
using System.IO;
using System.IO.Compression;
using Mcasaenk.Nbt;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public unsafe class ChunkInterpreterStartingPoint {

        public static IChunkInterpreter Read(Stream pointer) {
            if(pointer == null) return null;
            using var zlip = new ZLibStream(pointer, CompressionMode.Decompress);
            using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared);

            var nbtreader = new NbtReader(decompressedStream);
            bool error = nbtreader.TryRead(out var _g);
            //try {
            var globaltag = (CompoundTag_Optimal)_g;


            int version = globaltag["DataVersion"] != null ? (NumTag<int>)globaltag["DataVersion"] : -1;

            var minmaxh = Global.App.OpenedSave.GetDimension(Global.Settings.DIMENSION).GetHeight();

            var colormap = Global.App.Colormap;

            IChunkInterpreter chunkreader = null;
            if(version >= 2825) chunkreader = new ChunkDataInterpreter118(colormap, globaltag, minmaxh.miny, minmaxh.height, error); // 1.18 - 1.21
            else if(version >= 2556) chunkreader = new ChunkDataInterpreter117(colormap, globaltag, minmaxh.miny, minmaxh.height, error); // 1.16 - 1.17
            else if(version >= 1344) chunkreader = new ChunkDataInterpreter115(colormap, globaltag, minmaxh.miny, minmaxh.height, error); // 1.15
            else chunkreader = new ChunkDataInterpreter112(colormap, globaltag, minmaxh.miny, minmaxh.height, error); // 1.15

            if(chunkreader == null) throw new Exception();
            else return chunkreader;
            //}
            //catch (Exception e){
            //    throw e;
            //    //throw new Exception("chunk version is strange");
            //}
        }

    }
}
