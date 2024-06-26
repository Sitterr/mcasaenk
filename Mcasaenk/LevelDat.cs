﻿using Mcasaenk.Nbt;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk {
    public class LevelDat {
        public DateOnly lastopened { get; private set; }
        public Difficulty difficulty { get; private set; }
        public Gamemode gamemode { get; private set; }
        public string name { get; private set; }
        public string version { get; private set; }
        public long seed { get; private set; }
        public bool hardcore { get; private set; }
        public string imagepath { get; private set; }
        public string foldername { get; private set; }

        public int px { get; private set; }
        public int py { get; private set; }
        public int pz { get; private set; }
        public int sx { get; private set; }
        public int sy { get; private set; }
        public int sz { get; private set; }

        private LevelDat(Tag _tag, string foldername, string imagepath, DateOnly lastopened) {
            this.foldername = foldername;
            this.imagepath = imagepath;
            this.lastopened = lastopened;

            var tag = (CompoundTag)_tag;
            var data = (CompoundTag)tag["Data"];
            {
                this.difficulty = (Difficulty)(sbyte)(NumTag<sbyte>)data["Difficulty"];
                this.name = (NumTag<string>)data["LevelName"];
                this.gamemode = (Gamemode)(int)(NumTag<int>)data["GameType"];
                var version = (CompoundTag)data["Version"];
                {
                    this.version = (NumTag<string>)version["Name"];
                }
                var worldGenSettings = (CompoundTag)data["WorldGenSettings"];
                if(worldGenSettings != null) {
                    this.seed = (NumTag<long>)worldGenSettings["seed"];
                } else {
                    this.seed = (NumTag<long>)data["RandomSeed"];
                }
                this.hardcore = ((NumTag<sbyte>)data["hardcore"]) > 0;
                this.sx = (NumTag<int>)data["SpawnX"];
                this.sy = (NumTag<int>)data["SpawnY"];
                this.sz = (NumTag<int>)data["SpawnZ"];
                var player = (CompoundTag)data["Player"];
                {
                    if(player["SpawnX"] != null) {
                        this.px = (NumTag<int>)player["SpawnX"];
                        this.py = (NumTag<int>)player["SpawnY"];
                        this.pz = (NumTag<int>)player["SpawnZ"];
                    } else {
                        this.px = this.py = this.pz = int.MaxValue;
                    }
                }
            }
        }
        private LevelDat() { }


        public static LevelDat ReadWorld(string path) {
            try {
                using var pointer = new MemoryStream(File.ReadAllBytes(Path.Combine(path, "level.dat")));

                if(pointer == null) return null;
                using var zlip = new GZipStream(pointer, CompressionMode.Decompress);
                using var decompressedStream = new PooledBufferedStream(zlip, ArrayPool<byte>.Shared);


                var nbtreader = new NbtReader(decompressedStream);
                bool error = nbtreader.TryRead(out var _g);

                var globaltag = (CompoundTag)_g;
                return new LevelDat(globaltag, Path.GetFileName(path), Path.Combine(path, "icon.png"), DateOnly.FromDateTime(File.GetLastAccessTime(Path.Combine(path, "level.dat"))));
            }
            catch {
                return null;
            }
        }

        public static LevelDat ReadRegionFolder() {
            return new LevelDat() { };
        }


    }
    public enum Difficulty : sbyte {
        [Description("peaceful")]
        Peaceful = 0,
        [Description("easy")]
        Easy = 1,
        [Description("normal")]
        Normal = 2,
        [Description("hard")]
        Hard = 3,
    }
    public enum Gamemode : int {
        [Description("survival")]
        Survival = 0,
        [Description("creative")]
        Creative = 1,
        [Description("adventure")]
        Adventure = 2,
        [Description("spectator")]
        Spectator = 3,
    }
}
