using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mcasaenk.UI.Canvas;

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

    public static class NBTBlueprints {
        public static CompoundTag_Allgemein CreateMapScreenshot(Span<uint> pixels, WorldPosition frame, int version, ColorApproximationAlgorithm coloralgo) {
            CompoundTag_Allgemein root = new CompoundTag_Allgemein();
            var data = new CompoundTag_Allgemein();
            if(version >= 1484) root.Add("DataVersion", NumTag<int>.Get(version));
            root.Add("data", data);
            {
                data.Add("scale", NumTag<sbyte>.Get((sbyte)Math.Log2((int)(1 / frame.zoom))));
                data.Add("dimension", NumTag<sbyte>.Get(0));
                data.Add("trackingPosition", NumTag<sbyte>.Get(1));
                data.Add("unlimitedTracking", NumTag<sbyte>.Get(1));
                data.Add("xCenter", NumTag<int>.Get((int)(frame.Start.X + frame.Width / 2)));
                data.Add("zCenter", NumTag<int>.Get((int)(frame.Start.Y + frame.Height / 2)));
                if(version < 1519) {
                    data.Add("height", NumTag<short>.Get(128));
                    data.Add("width", NumTag<short>.Get(128));
                } else {
                    data.Add("banners", ListTag.Get(TagType.Compound));
                    data.Add("frames", ListTag.Get(TagType.Compound));
                }

                var bytetag = ArrTag<byte>.Get(16384);
                for(int i = 0; i < 16384; i++) {
                    bytetag[i] = JavaMapColors.Nearest(coloralgo, WPFColor.FromUInt(pixels[i]), version).id;
                }
                data.Add("colors", bytetag);
            }

            return root;
        }
    }
}
