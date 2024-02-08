using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpNBT;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Mcasaenk.Rendering {
    public static class RegionReader {
        struct ChunkInfo {
            public int offset;
            public int size;
            public int orig;
        }

        public static unsafe ChunkRenderData117[] ReadAnvilFileWithUnmanaged(string path) {
            ChunkRenderData117[] chunks = new ChunkRenderData117[1024];
            Span<ChunkInfo> chunkinfos = stackalloc ChunkInfo[1024];
            Task[] tasks = new Task[1024];

            IntPtr memIntPtr;
            int len;
            using(FileStream _baseStream = new FileStream(path, FileMode.Open, FileAccess.Read)) { // read whole file
                len = (int)_baseStream.Length;
                memIntPtr = Marshal.AllocHGlobal(len);
                var bytes = new Span<byte>((byte*)memIntPtr.ToPointer(), (int)_baseStream.Length);
                _baseStream.Read(bytes);
            }

            byte* curr = (byte*)memIntPtr.ToPointer();

            byte[] headerSection = new byte[4];
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
                curr += (chunkinfos[i].offset - (lastoffset + lastsize)) * 4096;
                lastoffset = chunkinfos[i].offset;
                lastsize = chunkinfos[i].size;

                if(chunkinfos[i].size == 0 && chunkinfos[i].offset == 0) continue;
                if(chunkinfos[i].size == 0) continue;

                int size = chunkinfos[i].size * 4096;
                byte* pointer = curr;

                int orig = chunkinfos[i].orig;
                //tasks[i] = new Task(() => {
                    chunks[orig] = ChunkWork_LazyRenderData(pointer);
                //});
                //tasks[i].Start(TaskScheduler.Default);

            curr += size;
            }
            for(int i = 0; i < 1024; i++) { // autocomplete task for empty chunks
                if(tasks[i] == null) {
                    tasks[i] = Task.CompletedTask;
                }
            }

            Task.WaitAll(tasks);
            Marshal.FreeHGlobal(memIntPtr);

            return chunks;
        }
        private static unsafe ChunkRenderData117 ChunkWork_LazyRenderData(byte* pointer) {
            int actualsize = pointer[0] << 24 | pointer[1] << 16 | pointer[2] << 8 | pointer[3];
            if(actualsize == 0) return null;

            using var decompressedStream = new ZLibStream(new UnmanagedMemoryStream(pointer + 5, actualsize - 1), CompressionMode.Decompress);

            var lazyreader = new LazyNBTReader(decompressedStream);
            return new ChunkRenderData117(lazyreader);
        }
    }
}
