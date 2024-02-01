using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpNBT;

namespace Mcasaenk {
    public static class RegionReader {
        public static CompoundTag[] ReadAllChunks(string path) {
            CompoundTag[] chunks = new CompoundTag[1024];
            byte[] buffer = File.ReadAllBytes(path);
            Parallel.For(0, 1024, (i) => {
                int offset = (buffer[i * 4] << 16 | buffer[i * 4 + 1] << 8 | buffer[i * 4 + 2]) * 4096;
                int vaguesize = buffer[i * 4 + 3] * 4096;
                if(offset == 0 || vaguesize == 0) {
                    return;
                }

                int actualsize = buffer[offset] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3];

                var reader = new TagReader(new ZLibStream(new MemoryStream(buffer, (int)offset + 5, (int)actualsize - 1), CompressionMode.Decompress), FormatOptions.Java);
                chunks[i] = reader.ReadTag<CompoundTag>();
            });
            return chunks;
        }
    }
}
