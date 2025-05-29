using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace Mcasaenk.Nbt {

    public abstract class McaReader : IDisposable {
        protected readonly string path;
        protected bool disposed = false;

        public McaReader(string path) {
            this.path = path;
        }
        ~McaReader() => Dispose();

        public abstract Stream[] ReadChunkOffsets();
        public abstract void Dispose();
    }

    public unsafe class UnmanagedMcaReader : McaReader {
        private readonly nint memIntPtr;

        public UnmanagedMcaReader(string path) : base(path) {
            int len;
            using(FileStream _baseStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { // read whole file into unmanaged
                len = (int)_baseStream.Length;
                if(len > 0) {
                    memIntPtr = Marshal.AllocHGlobal(len);
                    var bytes = new Span<byte>((byte*)memIntPtr.ToPointer(), len);
                    _baseStream.Read(bytes);
                }
            }
        }
        public override void Dispose() {
            if(!disposed) {
                Marshal.FreeHGlobal(memIntPtr);
                disposed = true;
            }
        }


        struct ChunkInfo {
            public int offset;
            public int size;
            public int orig;
        }
        public override Stream[] ReadChunkOffsets() {
            Stream[] streams = new Stream[1024];
            if(memIntPtr.ToPointer() == null) return streams;

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
            chunkinfos.Sort((a, b) => { return a.offset.CompareTo(b.offset); });

            curr += 4096; // update header

            int lastoffset = 0;
            int lastsize = 0;
            for(int i = 0; i < 1024; i++) {
                if(chunkinfos[i].offset == -2 || chunkinfos[i].size == 0) continue;

                curr += (chunkinfos[i].offset - (lastoffset + lastsize)) * 4096;
                lastoffset = chunkinfos[i].offset;
                lastsize = chunkinfos[i].size;

                if(chunkinfos[i].size == 0) continue;

                int actualsize = curr[0] << 24 | curr[1] << 16 | curr[2] << 8 | curr[3];
                streams[chunkinfos[i].orig] = new UnmanagedMemoryStream(curr + 5, actualsize);

                int size = chunkinfos[i].size * 4096;
                curr += size;
            }

            return streams;
        }

    }



    public unsafe class ManagedMcaReader : McaReader {
        private byte[] bytes;

        public ManagedMcaReader(string path) : base(path) {
            int len;
            using(FileStream _baseStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { // read whole file into unmanaged
                len = (int)_baseStream.Length;
                if(len > 0) {
                    bytes = ArrayPool<byte>.Shared.Rent(len);
                    _baseStream.Read(bytes);
                }
            }
        }
        public override void Dispose() {
            if(!disposed) {
                ArrayPool<byte>.Shared.Return(bytes);
                bytes = null;
                disposed = true;
            }
        }


        struct ChunkInfo {
            public int offset;
            public int size;
            public int orig;
        }
        public override Stream[] ReadChunkOffsets() {
            Stream[] streams = new Stream[1024];
            if(bytes == null) return streams;

            Span<ChunkInfo> chunkinfos = stackalloc ChunkInfo[1024];
            int curr = 0;

            Span<byte> headerSection = stackalloc byte[4];
            for(int i = 0; i < 1024; i++) {
                for(int s = 0; s < 4; s++) {
                    headerSection[s] = bytes[curr];
                    curr++;
                }

                chunkinfos[i].offset = (headerSection[0] << 16 | headerSection[1] << 8 | headerSection[2]) - 2;
                chunkinfos[i].size = headerSection[3];
                chunkinfos[i].orig = i;
            }
            chunkinfos.Sort((a, b) => { return a.offset.CompareTo(b.offset); });

            curr += 4096; // update header

            int lastoffset = 0;
            int lastsize = 0;
            for(int i = 0; i < 1024; i++) {
                if(chunkinfos[i].offset == -2 || chunkinfos[i].size == 0) continue;

                curr += (chunkinfos[i].offset - (lastoffset + lastsize)) * 4096;
                lastoffset = chunkinfos[i].offset;
                lastsize = chunkinfos[i].size;

                if(chunkinfos[i].size == 0) continue;

                int actualsize = bytes[curr] << 24 | bytes[curr + 1] << 16 | bytes[curr + 2] << 8 | bytes[curr + 3];
                streams[chunkinfos[i].orig] = new ReadOnlyMemoryStream(bytes.AsMemory().Slice(curr + 5, actualsize));

                int size = chunkinfos[i].size * 4096;
                curr += size;
            }

            return streams;
        }

    }
}
