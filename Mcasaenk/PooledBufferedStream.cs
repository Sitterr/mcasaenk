using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk {
    public class PooledBufferedStream : Stream {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override void Flush() {
            throw new NotImplementedException();
        }
        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }
        public override void SetLength(long value) {
            throw new NotImplementedException();
        }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }



        private byte[] _buffer;
        private Stream stream;
        private ArrayPool<byte> pool;
        private int pos, len;
        public PooledBufferedStream(Stream stream, ArrayPool<byte> pool, int bufferSize = 8096) {
            this.stream = stream;
            this.pool = pool;
            _buffer = pool.Rent(bufferSize);
            pos = 0;
            len = _buffer.Length;

            FillBuffer();
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan().Slice(offset, count));
        public override int Read(Span<byte> buffer) {
            //if(buffer.Length > len) return stream.Read(buffer);

            int count = Math.Min(buffer.Length, len - pos);
            _buffer.AsSpan(pos, count).CopyTo(buffer);
            pos += count;

            if(pos == len) {
                FillBuffer();
            }

            return count;
        }

        public override int ReadByte() {
            byte val = _buffer[pos];
            pos++;

            if(pos == len) {
                FillBuffer();
            }

            return val;
        }

        void FillBuffer() {
            int count, position = 0;
            do {
                count = stream.Read(_buffer, position, len - position);
                position += count;
            } while(count > 0);
            pos = 0;
        }

        protected override void Dispose(bool disposing) {
            //if(disposing) {
                pool.Return(_buffer);
            //}
            base.Dispose(disposing);
        }
    }
}
