using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Controls;

namespace Mcasaenk.Rendering {
    public unsafe class RegionReader : IDisposable {
        private readonly string path;
        private readonly IntPtr memIntPtr;
   
        public RegionReader(string path) { 
            this.path = path;
        
            int len;
            using(FileStream _baseStream = new FileStream(path, FileMode.Open, FileAccess.Read)) { // read whole file into unmanaged
                len = (int)_baseStream.Length;
                if(len > 0) {
                    memIntPtr = Marshal.AllocHGlobal(len);
                    var bytes = new Span<byte>((byte*)memIntPtr.ToPointer(), len);
                    _baseStream.Read(bytes);
                }
            }
        }
        public void Dispose() {
            Marshal.FreeHGlobal(memIntPtr);
            GC.SuppressFinalize(this);
        }


        struct ChunkInfo {
            public int offset;
            public int size;
            public int orig;
        }
        public byte*[] ReadChunkOffsets() {
            byte*[] ptrs = new byte*[1024];
            if(memIntPtr.ToPointer() == null) return ptrs;

            Span<ChunkInfo> chunkinfos = stackalloc ChunkInfo[1024];
            byte* curr = (byte*)memIntPtr.ToPointer();

            Span<byte> headerSection = stackalloc byte[4];
            for(int i = 0; i < 1024; i++) {
                for(int s = 0; s < 4; s++) {
                    headerSection[s] = *curr;
                    curr++;
                }

                chunkinfos[i].offset = (headerSection[0] << 16 | headerSection[1] << 8 | headerSection[2]) - 2;
                chunkinfos[i].size = headerSection[3];
                chunkinfos[i].orig = i;
            }
            MemoryExtensions.Sort(chunkinfos, (a, b) => { return a.offset.CompareTo(b.offset); });

            curr += 4096; // update header
       
            int lastoffset = 0;
            int lastsize = 0;
            for(int i = 0; i < 1024; i++) {
                if(chunkinfos[i].offset == -2 || chunkinfos[i].size == 0) continue;

                curr += (chunkinfos[i].offset - (lastoffset + lastsize)) * 4096;
                lastoffset = chunkinfos[i].offset;
                lastsize = chunkinfos[i].size;

                if(chunkinfos[i].size == 0) continue;

                ptrs[chunkinfos[i].orig] = curr;

                int size = chunkinfos[i].size * 4096;
                curr += size;
            }

            return ptrs;
        }




        public static ChunkRenderData117 LazyRenderData(GenerateTilePool pool, byte* pointer) {
            if(pointer == null) return null;
            int actualsize = pointer[0] << 24 | pointer[1] << 16 | pointer[2] << 8 | pointer[3];
            if(actualsize == 0) return null;

            using var decompressedStream = new ZLibStream(new UnmanagedMemoryStream(pointer + 5, actualsize - 1), CompressionMode.Decompress);

            var lazyreader = new LazyNBTReader(decompressedStream);
            return new ChunkRenderData117(pool, lazyreader);
        }
    }
}
