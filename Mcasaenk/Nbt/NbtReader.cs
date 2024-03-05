using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
    public abstract class Tag : IDisposable, IResettable {
        protected const int INSTANCEPOOLCOUNT = 10_000;

        public TagType type;
        public string name;

        protected void Set(TagType type, string name) {
            this.type = type;
            this.name = name;
        }

        public abstract void Dispose();

        public abstract bool TryReset();
    }
    public class NumTag<T> : Tag {
        private static ObjectPool<NumTag<T>> objpool = new DefaultObjectPool<NumTag<T>>(new DefaultPooledObjectPolicy<NumTag<T>>(), INSTANCEPOOLCOUNT);
        public static NumTag<T> Get(TagType type, string name, T value) {
            var obj = objpool.Get();
            obj.Set(type, name);
            obj.value = value;
            return obj;
        }
        public override void Dispose() {
            objpool.Return(this);
        }

        public override bool TryReset() {
            return true;
        }

        private T value;
        public NumTag() { }

        public static implicit operator T(NumTag<T> tag) => tag.value;
    }
    public class ArrTag<T> : Tag {
        private static ArrayPool<T> arrpool = ArrayPool<T>.Shared;
        private static ObjectPool<ArrTag<T>> objpool = new DefaultObjectPool<ArrTag<T>>(new DefaultPooledObjectPolicy<ArrTag<T>>(), INSTANCEPOOLCOUNT);
        public static ArrTag<T> Get(TagType type, string name, int len) {
            var obj = objpool.Get();
            obj.arr = arrpool.Rent(len);
            obj.Set(type, name);
            obj.len = len;
            return obj;
        }
        public override void Dispose() {
            arrpool.Return(arr);
            objpool.Return(this);
        }

        public override bool TryReset() {
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
        private static ObjectPool<CompoundTag> objpool = new DefaultObjectPool<CompoundTag>(new DefaultPooledObjectPolicy<CompoundTag>(), INSTANCEPOOLCOUNT);
        public static CompoundTag Get(string name) {
            var obj = objpool.Get();
            obj.Set(TagType.Compound, name);
            return obj;
        }
        public override void Dispose() {
            //foreach(var val in dict.Values) {
            //    val.Dispose();
            //}
            for(int i = 0; i < dict.Count; i++) { 
                dict.GetValueAtIndex(i).Dispose();
            }
            objpool.Return(this);
        }
        public override bool TryReset() {
            this.dict.Clear();
            return true;
        }


        private readonly SortedList<string, Tag> dict;
        public CompoundTag() {
            this.dict = new SortedList<string, Tag>(10);
        }

        public bool Contains(string name) => dict.ContainsKey(name);

        public void Add(Tag tag) { dict.Add(tag.name, tag); }

        public TTag Get<TTag>(string name) where TTag : Tag => (TTag)dict[name];
        public Tag this[string name] {
            get {
                if(dict.TryGetValue(name, out Tag val)) return val;
                return null;
            }
        }

        public static implicit operator SortedList<string, Tag>(CompoundTag tag) => tag.dict;
    }
    public class ListTag : Tag {
        private static ObjectPool<ListTag> objpool = new DefaultObjectPool<ListTag>(new DefaultPooledObjectPolicy<ListTag>(), INSTANCEPOOLCOUNT);
        public static ListTag Get(string name, TagType childType) {
            var obj = objpool.Get();
            obj.Set(TagType.List, name);
            obj.childType = childType;
            return obj;
        }
        public override void Dispose() {
            //foreach(var val in list) {
            //    val.Dispose();
            //}
            for(int i = 0; i < list.Count; i++) {
                list[i].Dispose();
            }
            objpool.Return(this);
        }
        public override bool TryReset() {
            this.list.Clear();
            return true;
        }


        private readonly List<Tag> list;
        private TagType childType;
        public ListTag() {
            this.list = new List<Tag>();
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
                var name = ReadUTF8();


                tag = ReadPayLoad(name, type);
            }
            catch {
                tag = null;
                return true;
            }

            return false;
        }

        private Tag ReadPayLoad(string name, TagType ttype) {
            switch(ttype) {
                case TagType.Byte:
                    return NumTag<sbyte>.Get(ttype, name, ReadSByte());
                
                case TagType.Short:
                    return NumTag<short>.Get(ttype, name, ReadShort());
                
                case TagType.Int:
                    return NumTag<int>.Get(ttype, name, ReadInt());
                
                case TagType.Long:
                    return NumTag<long>.Get(ttype, name, ReadLong());
                
                case TagType.Float:
                    return NumTag<float>.Get(ttype, name, ReadFloat());
                
                case TagType.Double:
                    return NumTag<double>.Get(ttype, name, ReadDouble());
                
                case TagType.ByteArray: {
                    var len = ReadInt();
                    var tag = ArrTag<byte>.Get(ttype, name, len);
                    ReadBuffer(MemoryMarshal.Cast<byte, byte>(tag));
                    return tag;
                }
                case TagType.IntArray: {
                    var len = ReadInt();
                    var tag = ArrTag<int>.Get(ttype, name, len);
                    ReadBuffer(MemoryMarshal.Cast<int, byte>(tag));
                    for(int i = 0; i < len; i++) {
                        tag[i] = tag[i].SwapEndian();
                    }
                    return tag;
                }
                case TagType.LongArray: {
                    var len = ReadInt();
                    var tag = ArrTag<long>.Get(ttype, name, len);
                    ReadBuffer(MemoryMarshal.Cast<long, byte>(tag));
                    for(int i = 0; i < len; i++) {
                        tag[i] = tag[i].SwapEndian();
                    }
                    return tag;
                }
                case TagType.String:
                    return NumTag<string>.Get(ttype, name, ReadUTF8());
                
                case TagType.Compound:
                    return ReadCompoundTag(name);
                
                case TagType.List:
                    return ReadListTag(name);
                

                default: return null;
            }
        }
        private ListTag ReadListTag(string name) {
            var childtype = (TagType)ReadByte();
            var count = ReadInt();

            var ltag = ListTag.Get(name, childtype);
            for(int i = 0; i < count; i++) {
                ltag.AddTag(ReadPayLoad(null, childtype));
            }

            return ltag;
        }
        private CompoundTag ReadCompoundTag(string name) {
            var ctag = CompoundTag.Get(name);
            while(true) {
                var childType = (TagType)ReadByte();
                if(childType == TagType.End)
                    break;
                var childName = ReadUTF8();
                ctag.Add(ReadPayLoad(childName, childType));
            }
            return ctag;
        }




        private string ReadUTF8(bool garbage = false) {
            Span<byte> lenb = stackalloc byte[sizeof(ushort)];
            ReadBuffer(lenb);
            var uint16 = BitConverter.ToUInt16(lenb);
            var len = uint16.SwapEndian();
            if(len == 0) return null;

            if(garbage) {
                Seek(len);
                return null;
            } else {
                Span<byte> buffer = new byte[len]; // stackalloc?
                ReadBuffer(buffer);
                //return StringPool.Shared.GetOrAdd(buffer, Encoding.UTF8);
                return Encoding.UTF8.GetString(buffer);
            }
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
            var value = BitConverter.ToInt16(buffer).SwapEndian();
            return value;
        }
        private int ReadInt() {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadBuffer(buffer);
            var value = BitConverter.ToInt32(buffer).SwapEndian();
            return value;
        }
        private long ReadLong() {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ReadBuffer(buffer);
            var value = BitConverter.ToInt32(buffer).SwapEndian();
            return value;
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
            return BitConverter.ToSingle(buffer);
        }


        void ReadBuffer(Span<byte> buffer) {
            var totalBytes = 0;
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
