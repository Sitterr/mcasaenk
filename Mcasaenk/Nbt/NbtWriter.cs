using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Nbt {
    public class NbtWriter {
        private readonly Stream stream;

        public NbtWriter(Stream stream, Tag tag, string name = "") { 
            this.stream = stream;

            stream.WriteByte((byte)tag.TagType());

            stream.Write(BitConverter.GetBytes(((ushort)name.Length).SwapEndian()));
            stream.Write(Encoding.UTF8.GetBytes(name));

            Write(tag);
        }

        private void Write(Tag tag) {
            if(tag is NumTag<sbyte> sbytetag) {
                stream.WriteByte((byte)(sbyte)sbytetag);
            } else if(tag is NumTag<short> shorttag) {
                stream.Write(BitConverter.GetBytes(((short)shorttag).SwapEndian()));
            } else if(tag is NumTag<int> inttag) {
                stream.Write(BitConverter.GetBytes(((int)inttag).SwapEndian()));
            } else if(tag is NumTag<long> longtag) {
                stream.Write(BitConverter.GetBytes(((long)longtag).SwapEndian()));
            } else if(tag is NumTag<float> floattag) {
                stream.Write(BitConverter.GetBytes(floattag).Reverse().ToArray());
            } else if(tag is NumTag<double> doubletag) {
                stream.Write(BitConverter.GetBytes(doubletag).Reverse().ToArray());
            } else if(tag is NumTag<string> stringtag) {
                var val = (string)stringtag;
                stream.Write(BitConverter.GetBytes(((ushort)val.Length).SwapEndian()));
                stream.Write(Encoding.UTF8.GetBytes(val));
            } else if(tag is ArrTag<byte> bytearrtag) {
                var val = (Span<byte>)bytearrtag;
                stream.Write(BitConverter.GetBytes((int)bytearrtag.Length.SwapEndian()));
                stream.Write(val);
            } else if(tag is ArrTag<int> intarrtag) {
                var val = (Span<int>)intarrtag;
                stream.Write(BitConverter.GetBytes((int)intarrtag.Length.SwapEndian()));
                foreach(var i in val) {
                    stream.Write(BitConverter.GetBytes(i.SwapEndian()));
                }
            } else if(tag is ArrTag<long> longarrtag) {
                var val = (Span<long>)longarrtag;
                stream.Write(BitConverter.GetBytes((int)longarrtag.Length.SwapEndian()));
                foreach(var i in val) {
                    stream.Write(BitConverter.GetBytes(i.SwapEndian()));
                }
            } else if(tag is CompoundTag_Allgemein compounttag) {
                foreach(var child in (Dictionary<string, Tag>)compounttag) {
                    // type
                    stream.WriteByte((byte)child.Value.TagType());

                    // name
                    stream.Write(BitConverter.GetBytes(((ushort)child.Key.Length).SwapEndian()));
                    stream.Write(Encoding.UTF8.GetBytes(child.Key));

                    Write(child.Value);
                }
                stream.WriteByte((byte)TagType.End);
            } else if(tag is ListTag listtag) {
                var val = (List<Tag>)listtag;

                stream.WriteByte((byte)listtag.ChildTagType());
                stream.Write(BitConverter.GetBytes((int)val.Count.SwapEndian()));

                foreach(var child in val) {
                    Write(child);
                }
            }
        }



    }
}
