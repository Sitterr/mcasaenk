﻿using Mcasaenk.Nbt;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Buffers;
using System.Reflection;

namespace Mcasaenk.Rendering.ChunkRenderData {
    public unsafe class ChunkInterpreterStartingPoint {

        public static IChunkInterpreter Read(Stream pointer) {
            if(pointer == null) return null;
            using var zlip = new ZLibStream(pointer, CompressionMode.Decompress);
            using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared);

            var nbtreader = new NbtReader(decompressedStream);
            bool error = nbtreader.TryRead(out var _g);
            //try {
                var globaltag = (CompoundTag)_g;

                int version = (NumTag<int>)globaltag["DataVersion"];
                IChunkInterpreter chunkreader = null;
                if(version > 0) {

                    if(version >= 2566 && version <= 2586) chunkreader = new ChunkDataInterpreter117(globaltag, Global.Settings.MINY_INT(0), Global.Settings.MAXY_INT(255), error); // 1.16
                    else if(version >= 2724 && version <= 2730) chunkreader = new ChunkDataInterpreter117(globaltag, Global.Settings.MINY_INT(-64), Global.Settings.MAXY_INT(319), error); // 1.17
                    else if(version >= 2825) chunkreader = new ChunkDataInterpreter118(globaltag, Global.Settings.MINY_INT(-64), Global.Settings.MAXY_INT(319), error); // 1.18 - 1.20
                    
                } 
                
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
