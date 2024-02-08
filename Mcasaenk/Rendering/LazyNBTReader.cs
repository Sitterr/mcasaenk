using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk.Rendering {

    public class LazyNBTReader {
        private Stream stream;
        public LazyNBTReader(Stream stream) {
            this.stream = stream;
        }

        public TagHeader ReadHeader(bool namewanted = true) {
            TagHeader header = new TagHeader();
            header.type = (TagType)stream.ReadByte();
            if(header.type == TagType.End) return header;
            header.name = ReadUTF8(!namewanted);
            return header;
        }
        public void ReadPayload(TagType tagType) {
            switch(tagType) {
                case TagType.Byte:
                    Seek(sizeof(byte)); break;
                case TagType.Short:
                    Seek(sizeof(short)); break;
                case TagType.Int:
                    Seek(sizeof(int)); break;
                case TagType.Long:
                    Seek(sizeof(long)); break;
                case TagType.Float:
                    Seek(sizeof(float)); break;
                case TagType.Double:
                    Seek(sizeof(double)); break;
                case TagType.String:
                    ReadUTF8(); break;
                case TagType.ByteArray: {
                    int len = ReadInt();
                    Seek(len * sizeof(byte));
                    break;
                }
                case TagType.IntArray: {
                    int len = ReadInt();
                    Seek(len * sizeof(int));
                    break;
                }
                case TagType.LongArray: {
                    int len = ReadInt();
                    Seek(len * sizeof(long));
                    break;
                }
                case TagType.List: {
                    var childtype = (TagType)ReadByte();
                    var count = ReadInt();

                    for(int i = 0; i < count; i++) {
                        ReadPayload(childtype);
                    }

                    break;                
                }
                case TagType.Compound: {
                    while(true) {
                        var header = ReadHeader(false);
                        if(header.type == TagType.End)
                            break;

                        ReadPayload(header.type);
                    }

                    break;
                }
            }
        }

        public void ForreachCompound(Func<TagHeader, bool> ondo) {
            int i = 0;
            while(true) {
                 var childheader = ReadHeader();
                if(childheader.type == TagType.End)
                    break;
                bool handled = ondo(childheader);
                if(!handled) ReadPayload(childheader.type);
                i++;
            }
        }

        public void ForreachList(Action<TagType, int> ondo) {
            var childtype = (TagType)ReadByte();
            var count = ReadInt();

            for(int i = 0; i < count; i++) {
                ondo(childtype, i);
            }
        }

        public string ReadUTF8(bool garbage = false) {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            ReadBuffer(buffer);
            var uint16 = BitConverter.ToUInt16(buffer);
            var len = uint16.SwapEndian();
            if(len == 0) return null;

            if(garbage) {
                Seek(len);
                return null;
            } else {
                Span<byte> utf8 = stackalloc byte[len];
                ReadBuffer(utf8, len);
                return Encoding.UTF8.GetString(utf8);
            }
        }
        public byte ReadByte() {
            return (byte)stream.ReadByte();
        }
        public short ReadShort() {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            ReadBuffer(buffer);
            var value = BitConverter.ToInt16(buffer).SwapEndian();
            return value;
        }
        public int ReadInt() {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadBuffer(buffer);
            var value = BitConverter.ToInt32(buffer).SwapEndian();
            return value;
        }
        public long ReadLong() {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ReadBuffer(buffer);
            var value = BitConverter.ToInt32(buffer).SwapEndian();
            return value;
        }
        public float ReadFloat() {
            Span<byte> buffer = stackalloc byte[sizeof(float)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToSingle(buffer);
        }
        public double ReadDouble() {
            Span<byte> buffer = stackalloc byte[sizeof(double)];
            ReadBuffer(buffer);
            buffer.Reverse();
            return BitConverter.ToSingle(buffer);
        }
        public void ReadByteArray(byte[] array, int len) {
            if(array == null) {
                Seek(len * sizeof(byte));
            } else {
                ReadBuffer(array, len * sizeof(byte));
            }

        }
        public void ReadIntArray(int[] array, int len) {
            if(array == null) {
                Seek(len * sizeof(int));
            } else {
                ReadBuffer(MemoryMarshal.Cast<int, byte>(array), len * sizeof(int));
                for(int i = 0; i < len; i++) {
                    array[i] = array[i].SwapEndian();
                }
            }
        }
        public void ReadLongArray(long[] array, int len) {
            if(array == null) {
                Seek(len * sizeof(long));
            } else {
                ReadBuffer(MemoryMarshal.Cast<long, byte>(array), len * sizeof(long));
                for(int i = 0; i < len; i++) {
                    array[i] = array[i].SwapEndian();
                }
            }
        }



        void ReadBuffer(Span<byte> buffer, int len = -1) {
            if(len != -1) { 
                buffer = buffer.Slice(0, len);
            }
            len = buffer.Length;
            var totalBytes = 0;
            while(totalBytes < len) {
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

    public struct TagHeader {
        public string name;
        public TagType type;
    }

    public enum TagType : byte {
        End = 0x00,

        Byte = 0x01,

        Short = 0x02,

        Int = 0x03,

        Long = 0x04,

        Float = 0x05,

        Double = 0x06,

        ByteArray = 0x07,

        String = 0x08,

        List = 0x09,

        Compound = 0x0a,

        IntArray = 0x0b,

        LongArray = 0x0c,
    }
}
