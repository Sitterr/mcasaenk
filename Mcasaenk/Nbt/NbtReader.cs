using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using System.Windows.Media.Animation;

namespace Mcasaenk.Nbt {
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
        LongArray = 0x0c
    }
    public interface Tag : IDisposable, IResettable {
        protected const int INSTANCEPOOLCOUNT = 10_000;

        public void Dispose();

        public bool TryReset();
    }
    public class NumTag<T> : Tag {
        private static ObjectPool<NumTag<T>> objpool = new DefaultObjectPool<NumTag<T>>(new DefaultPooledObjectPolicy<NumTag<T>>(), Tag.INSTANCEPOOLCOUNT);
        public static NumTag<T> Get(T value) {
            var obj = objpool.Get();
            obj.value = value;
            return obj;
        }
        public void Dispose() {
            objpool.Return(this);
        }

        public bool TryReset() {
            return true;
        }

        private T value;
        public NumTag() { }

        public static implicit operator T(NumTag<T> tag) => tag.value;
    }
    public class ArrTag<T> : Tag {
        private static ArrayPool<T> arrpool = ArrayPool<T>.Shared;
        private static ObjectPool<ArrTag<T>> objpool = new DefaultObjectPool<ArrTag<T>>(new DefaultPooledObjectPolicy<ArrTag<T>>(), Tag.INSTANCEPOOLCOUNT);
        public static ArrTag<T> Get(int len) {
            var obj = objpool.Get();
            obj.arr = arrpool.Rent(len);
            obj.len = len;
            return obj;
        }
        public void Dispose() {
            arrpool.Return(arr);
            objpool.Return(this);
        }

        public bool TryReset() {
            return true;
        }

        private T[] arr;
        private int len;
        public ArrTag() { }

        public unsafe int Length {
            get => len;
        }
        public T this[int index] {
            get => arr[index];
            set => arr[index] = value;
        }

        public static implicit operator Span<T>(ArrTag<T> tag) => tag.arr.AsSpan().Slice(0, tag.len);
    }
    public class CompoundTag : Tag {
        private static ObjectPool<CompoundTag> objpool = new DefaultObjectPool<CompoundTag>(new DefaultPooledObjectPolicy<CompoundTag>(), Tag.INSTANCEPOOLCOUNT);
        public static CompoundTag Get() {
            var obj = objpool.Get();
            return obj;
        }
        public void Dispose() {
            foreach(var val in dict.Values) {
                val.Dispose();
            }
            objpool.Return(this);
        }
        public bool TryReset() {
            this.dict.Clear();
            return true;
        }


        private readonly Dictionary<int, Tag> dict;
        public CompoundTag() {
            this.dict = new Dictionary<int, Tag>(50);
        }

        public void Add(int nbthash, Tag tag) { dict.Add(nbthash, tag); }

        public TTag Get<TTag>(string name) where TTag : Tag => (TTag)dict[name.GetHashCode()];
        public Tag this[string name] {
            get {
                if(dict.TryGetValue(name.GetHashCode(), out Tag val)) return val;
                return null;
            }
        }
    }
    public class ListTag : Tag {
        private static ObjectPool<ListTag> objpool = new DefaultObjectPool<ListTag>(new DefaultPooledObjectPolicy<ListTag>(), Tag.INSTANCEPOOLCOUNT);
        public static ListTag Get(TagType childType) {
            var obj = objpool.Get();
            obj.childType = childType;
            return obj;
        }
        public void Dispose() {
            for(int i = 0; i < list.Count; i++) {
                list[i].Dispose();
            }
            objpool.Return(this);
        }
        public bool TryReset() {
            this.list.Clear();
            return true;
        }


        private readonly List<Tag> list;
        private TagType childType;
        public ListTag() {
            this.list = new List<Tag>(50);
        }

        public int Length {
            get => list.Count;
        }

        public void AddTag(Tag tag) {
            this.list.Add(tag);
        }

        public static implicit operator List<Tag>(ListTag tag) => tag.list;
    }


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
            }
            catch (Exception e){
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
                    var ctag = CompoundTag.Get();
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
}
