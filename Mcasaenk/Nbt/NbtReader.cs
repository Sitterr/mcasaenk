using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using CommunityToolkit.HighPerformance.Buffers;

namespace Mcasaenk.Nbt {
    public class NbtReader {
        private Stream stream;
        public NbtReader(Stream stream) {
            this.stream = stream;
        }

        public bool TryRead(out Tag tag) {
            try {
                var type = (TagType)ReadByte();
                var name = ReadUTF8(true);

                tag = ReadPayLoad(type);
            } catch(Exception e) {
                tag = null;
                throw e;
                return true;
            }

            return false;
        }

        private Tag ReadPayLoad(TagType ttype) {
            switch(ttype) {
                case TagType.Byte:
                    return NumTag<sbyte>.Get(ReadSByte());

                case TagType.Short:
                    return NumTag<short>.Get(ReadShort());

                case TagType.Int:
                    return NumTag<int>.Get(ReadInt());

                case TagType.Long:
                    return NumTag<long>.Get(ReadLong());

                case TagType.Float:
                    return NumTag<float>.Get(ReadFloat());

                case TagType.Double:
                    return NumTag<double>.Get(ReadDouble());

                case TagType.String:
                    return NumTag<string>.Get(ReadUTF8());

                case TagType.ByteArray: {
                        int len = ReadInt();
                        var tag = ArrTag<byte>.Get(len);
                        ReadBuffer(MemoryMarshal.Cast<byte, byte>(tag));
                        return tag;
                    }
                case TagType.IntArray: {
                        int len = ReadInt();
                        var tag = ArrTag<int>.Get(len);
                        ReadBuffer(MemoryMarshal.Cast<int, byte>(tag));
                        for(int i = 0; i < len; i++) {
                            tag[i] = tag[i].SwapEndian();
                        }
                        return tag;
                    }
                case TagType.LongArray: {
                        int len = ReadInt();
                        var tag = ArrTag<long>.Get(len);
                        ReadBuffer(MemoryMarshal.Cast<long, byte>(tag));
                        for(int i = 0; i < len; i++) {
                            tag[i] = tag[i].SwapEndian();
                        }
                        return tag;
                    }
                case TagType.Compound: {
                        var ctag = CompoundTag_Optimal.Get();
                        while(true) {
                            var childType = (TagType)ReadByte();
                            if(childType == TagType.End)
                                break;
                            var childName = ReadName();
                            ctag.Add(childName, ReadPayLoad(childType));
                        }
                        return ctag;
                    }
                case TagType.List: {
                        var childtype = (TagType)ReadByte();
                        int count = ReadInt();

                        var ltag = ListTag.Get(childtype);
                        for(int i = 0; i < count; i++) {
                            ltag.AddTag(ReadPayLoad(childtype));
                        }
                        return ltag;
                    }

                default: return null;
            }
        }

        private string ReadUTF8(bool garbage = false) {
            Span<byte> lenb = stackalloc byte[sizeof(ushort)];
            ReadBuffer(lenb);
            var uint16 = BitConverter.ToUInt16(lenb);
            int len = uint16.SwapEndian();
            if(len == 0) return null;

            if(garbage) {
                Seek(len);
                return null;
            } else {
                Span<byte> utf8buffer = stackalloc byte[len];
                ReadBuffer(utf8buffer);
                return StringPool.Shared.GetOrAdd(utf8buffer, Encoding.UTF8);
                //return Encoding.UTF8.GetString(utf8buffer);
            }
        }
        private int ReadName() {
            Span<byte> lenb = stackalloc byte[sizeof(ushort)];
            ReadBuffer(lenb);
            var uint16 = BitConverter.ToUInt16(lenb);
            int len = uint16.SwapEndian();
            if(len == 0) return "".GetHashCode();

            Span<byte> utf8buffer = stackalloc byte[len];
            ReadBuffer(utf8buffer);
            Span<char> utf16result = stackalloc char[len];
            Utf8.ToUtf16(utf8buffer, utf16result, out int bytesRead, out int charsWritten);
            return String.GetHashCode(utf16result.Slice(0, charsWritten));
        }
        private byte ReadByte() {
            return (byte)stream.ReadByte();
        }
        private sbyte ReadSByte() {
            return (sbyte)stream.ReadByte();
        }
        private short ReadShort() {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToInt16(buffer);
        }
        private int ReadInt() {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToInt32(buffer);
        }
        private long ReadLong() {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToInt64(buffer);
        }
        private float ReadFloat() {
            Span<byte> buffer = stackalloc byte[sizeof(float)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToSingle(buffer);
        }
        private double ReadDouble() {
            Span<byte> buffer = stackalloc byte[sizeof(double)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToDouble(buffer);
        }


        void ReadBuffer(Span<byte> buffer) {
            int totalBytes = 0;
            while(totalBytes < buffer.Length) {
                var readBytes = stream.Read(buffer.Slice(totalBytes));
                if(readBytes == 0)
                    throw new EndOfStreamException();
                totalBytes += readBytes;
            }
        }
        void Seek(int bytes) {
            ReadBuffer(stackalloc byte[bytes]);
        }
    }


    public static class Extentions {
        public static short SwapEndian(this short value) => unchecked((short)SwapEndian(unchecked((ushort)value)));
        public static ushort SwapEndian(this ushort value) {
            return (ushort)((value << 8) | (value >> 8));
        }

        public static int SwapEndian(this int value) => unchecked((int)SwapEndian(unchecked((uint)value)));
        public static uint SwapEndian(this uint value) {
            value = ((value << 8) & 0xFF00FF00) | ((value >> 8) & 0xFF00FF);
            return (value << 16) | (value >> 16);
        }

        public static long SwapEndian(this long value) => unchecked((long)SwapEndian(unchecked((ulong)value)));
        public static ulong SwapEndian(this ulong value) {
            value = ((value << 8) & 0xFF00FF00FF00FF00UL) | ((value >> 8) & 0x00FF00FF00FF00FFUL);
            value = ((value << 16) & 0xFFFF0000FFFF0000UL) | ((value >> 16) & 0x0000FFFF0000FFFFUL);
            return (value << 32) | (value >> 32);
        }
    }
}
