﻿using Mcasaenk.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk.WorldInfo {

    public class ModsInfo : IDisposable {
        public readonly List<(PackMetadata meta, ZipRead read)> mods;
        public ModsInfo(LevelDatInfo levelDat) {
            mods = new List<(PackMetadata meta, ZipRead read)>();
            if(levelDat.mods.Length > 0) {
                foreach(var exmod in Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods"))) {
                    if(Path.GetExtension(exmod) != ".jar") continue;
                    ZipRead read = new ZipRead(exmod);
                    if(PackMetadata.ReadModMeta(read, out var meta) == false) continue;
                    if(levelDat.mods.Contains(meta.id) == false) continue;

                    mods.Add((meta, read));
                }
            }
        }

        public void Dispose() {
            foreach(var mod in mods) mod.read.Dispose();
        }
    }
}