using Microsoft.Extensions.ObjectPool;
using System;
using System.Buffers;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public TagType TagType();

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

        public TagType TagType() {
            if(typeof(T) == typeof(int)) return Nbt.TagType.Int;
            if(typeof(T) == typeof(short)) return Nbt.TagType.Short;
            if(typeof(T) == typeof(sbyte)) return Nbt.TagType.Byte;
            if(typeof(T) == typeof(long)) return Nbt.TagType.Long;
            if(typeof(T) == typeof(float)) return Nbt.TagType.Float;
            if(typeof(T) == typeof(double)) return Nbt.TagType.Double;
            if(typeof(T) == typeof(string)) return Nbt.TagType.String;
            return Nbt.TagType.End;
        }

        private T value;
        public NumTag() { }

        public static implicit operator T(NumTag<T> tag) => tag == null ? default : tag.value;
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

        public TagType TagType() {
            if(typeof(T) == typeof(int)) return Nbt.TagType.IntArray;
            if(typeof(T) == typeof(byte)) return Nbt.TagType.ByteArray;
            if(typeof(T) == typeof(long)) return Nbt.TagType.LongArray;
            return Nbt.TagType.End;
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
    public class CompoundTag_Optimal : Tag {
        private static ObjectPool<CompoundTag_Optimal> objpool = new DefaultObjectPool<CompoundTag_Optimal>(new DefaultPooledObjectPolicy<CompoundTag_Optimal>(), Tag.INSTANCEPOOLCOUNT);
        public static CompoundTag_Optimal Get() {
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

        public TagType TagType() => Nbt.TagType.Compound;


        private readonly Dictionary<int, Tag> dict;
        public CompoundTag_Optimal() {
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

    public class CompoundTag_Allgemein : Tag {
        public void Dispose() {
            foreach(var val in dict.Values) {
                val.Dispose();
            }
        }
        public bool TryReset() => true;

        public TagType TagType() => Nbt.TagType.Compound;

        private readonly Dictionary<string, Tag> dict;
        public CompoundTag_Allgemein() {
            this.dict = new Dictionary<string, Tag>();
        }

        public void Add(string name, Tag tag) { dict.Add(name, tag); }

        public static implicit operator Dictionary<string, Tag>(CompoundTag_Allgemein tag) => tag.dict;
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

        public TagType TagType() => Nbt.TagType.List;
        public TagType ChildTagType() => childType;


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
}
