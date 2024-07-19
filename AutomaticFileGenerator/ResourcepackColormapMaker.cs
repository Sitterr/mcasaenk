using Mcasaenk;
using Mcasaenk.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Mcasaenk.Global;

namespace Utils.ColormapMaker {
    public struct Options {
        public double minQ = 0;
        public bool for_Q0_try_with_sides = true;
        public Options() { }
    }
    enum BlockCreationMethod { AboveQ, BelowQ, Sides, Texture, None }
    public static class ResourcepackColormapMaker {

        public static void Make(string outputpath, string[] resourcepacks, Options options) {
            var reversepacks = resourcepacks.Reverse().ToArray();
            options.minQ = Math.Round(options.minQ, 3);
            SaveInterface output;
            if(outputpath.EndsWith(".zip")) output = new ZipSave(outputpath);
            else if(outputpath.Contains(".") == false) output = new FileSave(outputpath, true);
            else return;


            Dictionary<string, JsonModel> models = new Dictionary<string, JsonModel>();

            List<(string block, uint color, bool vanillatinted, BlockCreationMethod creationMethod)> blocks = new List<(string block, uint color, bool vanillatinted, BlockCreationMethod creationMethod)>();
            // blocks
            {
                var state_to_model = BlockStatesToModel();

                foreach(var f in state_to_model) {
                    string blockname = f.Key;

                    Dictionary<string, (Bitmap image, string loc)> textures = new();
                    var el = ReadModel(f.Value);
                    if(el == null) continue;
                    while(el.elements == null) {
                        if(el.textures != null) {
                            foreach(var t in el.textures) {
                                if(t.Value.StartsWith("#")) {
                                    if(textures.TryGetValue(t.Value.Substring(1), out var tex)) {
                                        textures[t.Key] = tex;
                                    }
                                } else {
                                    string loc = toLocation(t.Value, "textures", "png");
                                    if(rightLocation<Bitmap>(loc, out var bitmap)) {
                                        textures[t.Key] = (bitmap, t.Value);
                                    }
                                }
                            }
                        }
                        if(el.parent == null) break;
                        el = ReadModel(toLocation(el.parent, "models", "json"));
                        if(el == null) break;
                    }
                    if(el == null) continue;
                    if(el.parent == null && el.elements == null) {
                        if(textures.Count == 0) {
                            Print($"{blockname} has no data, no textures even and is thus being skipped", ConsoleColor.DarkRed);
                            blocks.Add((blockname, 0xFFFFFFFF, default, BlockCreationMethod.None));
                        } else {
                            ConsoleColor color = ConsoleColor.DarkYellow;
                            Print($"{blockname} has no data. However at least one texture *{textures.First().Value.loc}* has been found", color);

                            var answer = ColorOfTexture(textures.First().Value.image);
                            blocks.Add((blockname, answer, VanillaTints.IsNormallyTinted(blockname), BlockCreationMethod.Texture));
                        }

                    } else {
                        if(el.textures != null) {
                            foreach(var t in el.textures) {
                                if(t.Value.StartsWith("#")) {
                                    textures[t.Key] = textures[t.Value.Substring(1)];
                                } else {
                                    string loc = toLocation(t.Value, "textures", "png");
                                    if(rightLocation<Bitmap>(toLocation(t.Value, "textures", "png"), out var bitmap)) {
                                        textures[t.Key] = (bitmap, t.Value);
                                    }
                                }
                            }
                        }
                        var answer = JsonModel.ReadTopDown(blockname, el.elements, textures);

                        if(answer.q == 0 && options.for_Q0_try_with_sides == false) {
                            Print($"{blockname} has no topdown and is thus being skipped", ConsoleColor.DarkRed);
                        } else {
                            if(answer.q == 0) {
                                var sideanswer = JsonModel.ReadSide(blockname, el.elements, textures);
                                if(sideanswer.q > 0) {
                                    Print($"{blockname} has no topdown, but uses sides", ConsoleColor.DarkGreen);
                                    blocks.Add((blockname, sideanswer.sidecolor, sideanswer.tintindex != -1, BlockCreationMethod.Sides));
                                }
                            } else {
                                Print($"{blockname} has topdown with q={answer.q}", ConsoleColor.DarkBlue);
                                blocks.Add((blockname, answer.topdowncolor, answer.tintedindex != -1, answer.q >= options.minQ ? BlockCreationMethod.AboveQ : BlockCreationMethod.BelowQ));
                            }
                        }

                    }

                }
            }

            List<string> blocklines = new() {
                "// automatically created",
                "// Q means what part of the block has an up texture. For example a solid block would have Q = 1, while a dandelion would have Q = 0, as from exactly above it cannot be seen",
                "", ""
            };
            blocks = blocks.OrderBy(bl => bl.creationMethod).ToList();
            for(int i = 0; i < blocks.Count; i++) {
                string line = $"{blocks[i].block.simplifyminecraftname()}={blocks[i].color.ToString("X").Substring(2)}";
                if(i > 0) {
                    if(blocks[i - 1].creationMethod != blocks[i].creationMethod) {
                        blocklines.Add("");
                        blocklines.Add("");
                        blocklines.Add(blocks[i].creationMethod switch {
                            BlockCreationMethod.AboveQ => $"// the following blocks have Q above the {options.minQ} threshold",
                            BlockCreationMethod.BelowQ => $"// the following blocks have Q below the {options.minQ} threshold and are being skipped",
                            BlockCreationMethod.Sides => $"// the following blocks have Q = 0, but can use workaround with sides",
                            BlockCreationMethod.Texture => $"// from the following blocks I only detected a texture",
                            BlockCreationMethod.None => $"// the following blocks dont have a model or a texture",
                        });
                    }
                } else blocklines.Add($"// the following blocks have Q above the {options.minQ} threshold");

                if(blocks[i].creationMethod == BlockCreationMethod.None || blocks[i].creationMethod == BlockCreationMethod.BelowQ || (blocks[i].creationMethod == BlockCreationMethod.Sides && (options.for_Q0_try_with_sides == false || options.minQ > 0))) line = "//" + line;

                blocklines.Add(line);
            }
            output.SaveLines("__colormap__", blocklines);

            // tints
            {
                {
                    Print(); Print(); Print();
                    Print("---------- TINTED BLOCKS ----------", ConsoleColor.Gray);
                    foreach(var bl in blocks.Where(l => l.vanillatinted)) {
                        Print(bl.block, ConsoleColor.DarkGray);
                    }
                    Print("-----------------------------------", ConsoleColor.Gray);
                    Print(); Print(); Print();
                }

                List<(string name, string format, Bitmap source, uint color)> tints = new();
                var blocktint = new Dictionary<string, int>();

                // vanilla tints
                foreach(var vtint in VanillaTints.tints) {
                    Bitmap source = vtint.name switch {
                        "grass" => Resources.grass,
                        "foliage" => Resources.foliage,
                        _ => null,
                    };
                    tints.Add((vtint.name, vtint.format, source, vtint.deftint));
                }
                int vtintsi = tints.Count;
                foreach(var bl in blocks.Where(l => l.vanillatinted)) {
                    int tint = -1;
                    for(int i = 0; i < VanillaTints.tints.Count; i++) {
                        if(VanillaTints.tints[i].blocks.Contains(bl.block)) {
                            tint = i;
                            break;
                        }
                    }
                    blocktint.Add(bl.block, tint);
                }

                // optifine tints
                foreach(var pack in reversepacks) {
                    var colormapdir = Path.Combine(pack, "assets", "minecraft", "optifine", "colormap");
                    if(!Path.Exists(colormapdir)) continue;

                    // custom
                    {
                        if(Path.Exists(Path.Combine(colormapdir, "custom"))) {
                            foreach(var file in Directory.GetFiles(Path.Combine(colormapdir, "custom"), "*.properties")) {
                                var r = Tint.ReadTint(file);

                                tints.Add((r.name, r.format, new Bitmap(Path.GetFullPath(r.source, Path.Combine(colormapdir, "custom"))), r.color));
                                foreach(var bl in r.blocks) {
                                    blocktint[bl] = tints.Count - 1;
                                }
                            }
                        }

                        if(Path.Exists(Path.Combine(colormapdir, "blocks"))) {
                            foreach(var file in Directory.GetFiles(Path.Combine(colormapdir, "blocks"), "*.properties")) {
                                var r = Tint.ReadTint(file);

                                tints.Add((r.name, r.format, new Bitmap(Path.GetFullPath(r.source, Path.Combine(colormapdir, "custom"))), r.color));
                                foreach(var bl in r.blocks) {
                                    blocktint[bl] = tints.Count - 1;
                                }
                            }
                        }
                    }


                    // static
                    {
                        {
                            if(File.Exists(Path.Combine(colormapdir, "swampgrass.properties")) || File.Exists(Path.Combine(colormapdir, "swampgrass.png"))) {
                                Print("Detected optifine tint swampgrass, which cannot be converted!", ConsoleColor.Red);
                            }
                        }
                        {
                            if(File.Exists(Path.Combine(colormapdir, "swampfoliage.properties")) || File.Exists(Path.Combine(colormapdir, "swampfoliage.png"))) {
                                Print("Detected optifine tint swampfoliage, which cannot be converted!", ConsoleColor.Red);
                            }
                        }

                        {
                            string path = Path.Combine(pack, "assets", "minecraft", "optifine", "color.properties");
                            if(File.Exists(path)) {
                                var lines = File.ReadAllLines(path).Select(l => l.Split("=").Select(p => p.Trim()).ToArray());
                                foreach(var line in lines) {
                                    if(line[0] == "lilypad") {
                                        tints.Add(("lily_pad", "fixed", null, 0xFF000000 | Convert.ToUInt32(line[1], 16)));
                                        blocktint["minecraft:lily_pad"] = tints.Count - 1;
                                    }
                                }
                            }
                        }

                        {
                            (string name, string format, string[] blocks, string source, uint color) tint = default;
                            if(File.Exists(Path.Combine(colormapdir, "pine.properties"))) {
                                tint = Tint.ReadTint(Path.Combine(colormapdir, "pine.properties"));
                            } else if(Path.Exists(Path.Combine(colormapdir, "pine.png"))) {
                                tint = ("pine", "vanilla", default, Path.Combine(colormapdir, "pine.png"), default);
                            }

                            if(tint != default) {
                                tints.Add((tint.name, tint.format, new Bitmap(Path.GetFullPath(tint.source, colormapdir)), tint.color));
                                blocktint["minecraft:spruce_leaves"] = tints.Count - 1;
                            }
                        }

                        {
                            (string name, string format, string[] blocks, string source, uint color) tint = default;
                            if(File.Exists(Path.Combine(colormapdir, "birch.properties"))) {
                                tint = Tint.ReadTint(Path.Combine(colormapdir, "birch.properties"));
                            } else if(Path.Exists(Path.Combine(colormapdir, "birch.png"))) {
                                tint = ("birch", "vanilla", default, Path.Combine(colormapdir, "birch.png"), default);
                            }

                            if(tint != default) {
                                tints.Add((tint.name, tint.format, new Bitmap(Path.GetFullPath(tint.source, colormapdir)), tint.color));
                                blocktint["minecraft:birch_leaves"] = tints.Count - 1;
                            }
                        }

                        {
                            (string name, string format, string[] blocks, string source, uint color) tint = default;
                            if(File.Exists(Path.Combine(colormapdir, "water.properties"))) {
                                tint = Tint.ReadTint(Path.Combine(colormapdir, "water.properties"));
                            } else if(Path.Exists(Path.Combine(colormapdir, "water.png"))) {
                                tint = ("water", "vanilla", default, Path.Combine(colormapdir, "water.png"), default);
                            }

                            if(tint != default) {
                                tints.Add((tint.name, tint.format, new Bitmap(Path.GetFullPath(tint.source, colormapdir)), tint.color));
                                blocktint["minecraft:water"] = tints.Count - 1;
                            }
                        }

                        {
                            (string name, string format, string[] blocks, string source, uint color) tint = default;
                            if(Path.Exists(Path.Combine(colormapdir, "redstone.png"))) {
                                tint = ("redstone_wire", "fixed", default, default, ColorOfTexture(new Bitmap(Path.Combine(colormapdir, "redstone.png"))));
                            }

                            if(tint != default) {
                                tints.Add((tint.name, tint.format, null, tint.color));
                                blocktint["minecraft:redstone_wire"] = tints.Count - 1;
                            }
                        }

                        {
                            (string name, string format, string[] blocks, string source, uint color) tint = default;
                            if(Path.Exists(Path.Combine(colormapdir, "pumpkinstem.png"))) {
                                tint = ("pumpkin", "fixed", default, default, ColorOfTexture(new Bitmap(Path.Combine(colormapdir, "pumpkinstem.png"))));
                            }

                            if(tint != default) {
                                tints.Add((tint.name, tint.format, null, tint.color));
                                blocktint["minecraft:pumpkin_stem"] = tints.Count - 1;
                                blocktint["minecraft:attached_pumpkin_stem"] = tints.Count - 1;
                            }
                        }

                        {
                            (string name, string format, string[] blocks, string source, uint color) tint = default;
                            if(Path.Exists(Path.Combine(colormapdir, "melonstem.png"))) {
                                tint = ("melon", "fixed", default, default, ColorOfTexture(new Bitmap(Path.Combine(colormapdir, "melonstem.png"))));
                            }

                            if(tint != default) {
                                tints.Add((tint.name, tint.format, null, tint.color));
                                blocktint["minecraft:melon_stem"] = tints.Count - 1;
                                blocktint["minecraft:attached_melon_stem"] = tints.Count - 1;
                            }
                        }
                    }
                }

                var blocktintf = blocktint.Where(b => blocks.Any(bl => bl.block == b.Key)).ToList();
                for(int i = 0; i < tints.Count; i++) {
                    var blocksforthistint = blocktintf.Where(bl => bl.Value == i).Select(bl => bl.Key).ToArray();
                    if(blocksforthistint.Length > 0) {
                        SaveTint(tints[i], blocksforthistint, i >= vtintsi);
                    }
                }
            }




















            JsonModel ReadModel(string dloc) {
                if(models.ContainsKey(dloc)) return models[dloc];
                if(rightLocation<JsonModel>(dloc, out var json)) {
                    try {
                        var el = json;
                        models[dloc] = el;
                        return el;
                    }
                    catch {
                        return null;
                    }
                }
                return null;
            }


            string toLocation(string name, string object_type, string suffix) {
                string mnamespace = "minecraft", path = name;
                if(name.Contains(":")) {
                    mnamespace = name.Split(':')[0];
                    path = path.Split(':')[1];
                }

                return $"assets/{mnamespace}/{object_type}/{path}.{suffix}";
            }
            bool rightLocation<T>(string loc, out T file) {
                file = default;
                foreach(var pack in reversepacks) {
                    string ret = Path.Combine(pack, loc);
                    if(File.Exists(ret)) {
                        if(Path.GetExtension(ret) == ".json") {
                            try {
                                file = JsonSerializer.Deserialize<T>(File.ReadAllText(ret), new JsonSerializerOptions() { IncludeFields = true });
                                return true;
                            }
                            catch {
                                return false;
                            }
                        } else if(typeof(T) == typeof(Bitmap)) {
                            file = (T)(object)new Bitmap(ret);
                            return true;
                        }
                    }
                }
                return false;
            }


            void SaveTint((string name, string format, Bitmap source, uint color) tint, string[] blocks, bool optifine = false) {
                if(optifine) tint.name += "_optifine";
                List<string> lines = new List<string>();

                lines.Add($"format={tint.format}");
                if(!(tint.format == "fixed" && blocks.Length == 1 && blocks[0].minecraftname() == tint.name.minecraftname())) lines.Add($"blocks={string.Join(" ", blocks.Select(bl => bl.simplifyminecraftname()))}");
                if(tint.source != null) lines.Add($"source={tint.name}.png");
                if(tint.format == "fixed") lines.Add($"color={tint.color.ToString("X").Substring(2)}");

                if(tint.source != null) output.SaveImage(tint.name + ".png", tint.source);
                output.SaveLines(tint.name + ".properties", lines);

                if(optifine == false) Print($"Saving {tint.format} tint {tint.name}!", ConsoleColor.DarkGreen);
                else Print($"Saving {tint.format} optifine tint {tint.name}!", ConsoleColor.DarkBlue);
            }


            Dictionary<string, string> BlockStatesToModel() {
                Dictionary<string, string> dict = new();
                foreach(var resourcepack in reversepacks) {
                    foreach(var mnamespace in

                        Directory.GetDirectories(Path.Combine(resourcepack, "assets"))) {
                        if(Path.Exists(Path.Combine(mnamespace, "blockstates"))) {
                            foreach(var file in Directory.GetFiles(Path.Combine(mnamespace, "blockstates"))) {
                                if(Path.GetExtension(file) != ".json") continue;
                                string name = new DirectoryInfo(mnamespace).Name + ":" + Path.GetFileNameWithoutExtension(file);
                                string model = ReadModelFromBlockState(File.ReadAllText(file));
                                if(model != null && model != "") dict[name] = model;
                            }
                        }
                    }
                }
                return dict;
            }
            string ReadModelFromBlockState(string filecontent) {
                try {
                    var jsonDocument = JsonDocument.Parse(filecontent);
                    var root = jsonDocument.RootElement;

                    string modelname = "";
                    if(root.TryGetProperty("variants", out var variants)) {
                        if(!variants.TryGetProperty("", out var variant)) {
                            foreach(var a in variants.EnumerateObject()) {
                                if(a.Name.Contains("upper") || a.Name.Contains("top")) {
                                    variant = a.Value;
                                    break;
                                }
                            }
                            if(variant.ValueKind == JsonValueKind.Undefined) variant = variants.EnumerateObject().First().Value;
                        }
                        modelname = variant.getObjectOrFirstElement("model").GetString();
                    } else if(root.TryGetProperty("multipart", out var multipart)) {
                        var firstEl = multipart.EnumerateArray().First();
                        modelname = firstEl.GetProperty("apply").getObjectOrFirstElement("model").GetString();
                    }

                    return toLocation(modelname, "models", "json");
                }
                catch {
                    return "";
                }
            }


            uint ColorOfTexture(Bitmap image) {
                int r = 0, g = 0, b = 0, br = 0;

                for(int i = 0; i < image.Width; i++) {
                    for(int j = 0; j < image.Height; j++) {
                        var pixel = image.GetPixel(i, j);
                        if(pixel.A == 0) continue;

                        r += pixel.R;
                        g += pixel.G;
                        b += pixel.B;
                        br++;
                    }
                }

                return Color.FromArgb(r / br, g / br, b / br).ToUInt();
            }


            void Print(string message = "", ConsoleColor color = ConsoleColor.DarkGray) {
                Console.ForegroundColor = color;
                Console.WriteLine(message, color);
                Console.ResetColor();
            }

            if(output is IDisposable disposableOutput) disposableOutput.Dispose();
        }
    }




    class JsonModel {
        public string parent;
        public Dictionary<string, string> textures;
        public JsonElement[] elements = null;

        public class JsonElement {
            public double[] from;
            public double[] to;
            public JsonRotation rotation;
            public bool shade = true;
            public Dictionary<string, JsonFace> faces;

            public class JsonRotation {
                public double[] origin;
                public string axis;
                public double angle;
            }

            public class JsonFace {
                public double[] uv = [0, 0, int.MaxValue, int.MaxValue];
                public string texture;
                public int tintindex = -1;
            }
        }

        public static (uint topdowncolor, double q, int tintedindex) ReadTopDown(string block, JsonElement[] elements, Dictionary<string, (Bitmap image, string loc)> textures) {
            (uint color, int y, int tintindex)[,] topdown = new (uint color, int y, int tintindex)[16, 16];
            for(int i = 0; i < 16 * 16; i++) topdown[i / 16, i % 16] = (0, -1, -1);

            foreach(var element in elements) {
                foreach(var face in element.faces) {
                    int y = -1;
                    if(face.Key == "up") {
                        y = Math.Max((int)element.from[1], (int)element.to[1]);
                    } else if(face.Key == "down") {
                        y = Math.Min((int)element.from[1], (int)element.to[1]);
                    } else continue;

                    int minx = Math.Clamp(Math.Min((int)element.from[0], (int)element.to[0]), 0, 15);
                    int maxx = Math.Clamp(Math.Max((int)element.from[0], (int)element.to[0]), 0, 15);
                    int minz = Math.Clamp(Math.Min((int)element.from[2], (int)element.to[2]), 0, 15);
                    int maxz = Math.Clamp(Math.Max((int)element.from[2], (int)element.to[2]), 0, 15);

                    int[] uv;
                    if(face.Value.uv == null) uv = [minx, minz, maxx, maxz];
                    else uv = face.Value.uv.Select(g => (int)g).ToArray();
                    if(uv[0] > uv[2]) {
                        int f = uv[0];
                        uv[0] = uv[2];
                        uv[2] = f;
                    }
                    if(uv[0] == uv[2]) uv[2]++;
                    if(uv[1] > uv[3]) {
                        int f = uv[1];
                        uv[1] = uv[3];
                        uv[3] = f;
                    }
                    if(uv[1] == uv[3]) uv[3]++;
                    
                    for(int x = minx; x <= maxx; x++) {
                        for(int z = minz; z <= maxz; z++) {
                            if(y >= topdown[x, z].y) {
                                string tname = face.Value.texture;
                                if(textures.ContainsKey(tname) == false) tname = tname.Substring(1);
                                if(textures.TryGetValue(tname, out var image) == false) continue;
                                var img = image.image;
                                int s = Math.Min(img.Width, img.Height);
                                var pixel = img.GetPixel(s / 16 * Math.Min(uv[0] + (x - minx), uv[2] - 1), s / 16 * Math.Min(uv[1] + (z - minz), uv[3] - 1));
                                if(pixel.A == 0) continue;
                                uint uintpixel = pixel.ToUInt();
                                topdown[x, z].color = uintpixel;
                                topdown[x, z].y = y;
                                topdown[x, z].tintindex = face.Value.tintindex;
                            }
                        }
                    }
                }
            }

            int ft = 100;
            bool frst = true;
            for(int x = 0; x < 16; x++) {
                for(int z = 0; z < 16; z++) {
                    if(topdown[x, z].y > -1) {
                        if(frst) {
                            ft = topdown[x, z].tintindex;
                            frst = false;
                        } else {
                            if(topdown[x, z].tintindex != ft) {
                                for(int i = 0; i < 16 * 16; i++) {
                                    topdown[i / 16, i % 16].color = VanillaTints.InstaTint(block, topdown[i / 16, i % 16].tintindex, topdown[i / 16, i % 16].color);
                                }

                                z = 100; x = 100;
                                break;
                            }
                        }
                    }
                }
            }

            int r = 0, g = 0, b = 0, br = 0;
            for(int i = 0; i < 16 * 16; i++) {
                var argb = topdown[i / 16, i % 16].color.ToARGB();
                if(argb.a == 0) continue;

                r += argb.r;
                g += argb.g;
                b += argb.b;
                br++;
            }
            if(br == 0) return (0, 0, -1);

            return (Color.FromArgb(r / br, g / br, b / br).ToUInt(), Math.Round(br / 256d, 2), ft);
        }

        public static (uint sidecolor, double q, int tintindex) ReadSide(string block, JsonElement[] elements, Dictionary<string, (Bitmap image, string loc)> textures) {
            int tintedindex = -1;
            bool instatint = false, first = true;

            int r = 0, g = 0, b = 0, br = 0;
            foreach(var element in elements) {
                foreach(var face in element.faces) {
                    if((face.Key == "north" || face.Key == "south" || face.Key == "west" || face.Key == "east") == false) continue;

                    if(!first) {
                        if(tintedindex != face.Value.tintindex && !instatint) {
                            var res = VanillaTints.InstaTint(block, tintedindex, Color.FromArgb(r / br, g / br, b / br).ToUInt()).ToARGB();
                            r = res.r * br;
                            g = res.g * br;
                            b = res.b * br;
                            tintedindex = -1;
                            instatint = true;
                        }
                    } else {
                        tintedindex = face.Value.tintindex;
                    }

                    string tname = face.Value.texture;
                    if(textures.ContainsKey(tname) == false) tname = tname.Substring(1);

                    int py = 1;
                    int[] uv = face.Value.uv.Select(a => Math.Clamp((int)a, 0, 16)).ToArray();
                    if(face.Value.uv == null) uv = [0, 0, 16, 16];
                    if(uv[0] > uv[2]) {
                        int f = uv[0];
                        uv[0] = uv[2];
                        uv[2] = f;
                    }
                    if(uv[0] == uv[2]) uv[2]++;
                    if(uv[1] > uv[3]) {
                        py = -1;
                    }
                    if(uv[1] == uv[3]) uv[3]++;

                    int maxy = Math.Max(uv[1], uv[3]), miny = Math.Min(uv[1], uv[3]);
                    for(int tx = uv[0]; tx < uv[2]; tx++) {
                        for(int _ty = miny; _ty < maxy; _ty++) {
                            int ty = py == 1 ? _ty : maxy - _ty;
                            if(textures.TryGetValue(tname, out var image) == false) continue;
                            var img = image.image;
                            int s = Math.Min(img.Width, img.Height);
                            var pixel = img.GetPixel(s / 16 * tx, s / 16 * ty);
                            if(pixel.A == 0) continue;
                            var uintpixel = pixel.ToUInt();
                            if(instatint) uintpixel = VanillaTints.InstaTint(block, face.Value.tintindex, uintpixel);
                            var againpixel = uintpixel.ToARGB();

                            r += againpixel.r;
                            g += againpixel.g;
                            b += againpixel.b;
                            br++;
                            break;
                        }
                    }

                    first = false;
                }
            }


            if(br == 0) return (0, 0, -1);

            return (Color.FromArgb(r / br, g / br, b / br).ToUInt(), Math.Round(br / 256d, 2), tintedindex);
        }
    }


    static class VanillaTints {
        static uint grassTint;
        public readonly static List<(string name, string format, List<string> blocks, uint deftint)> tints;
        static VanillaTints() {
            tints = new List<(string name, string format, List<string> blocks, uint deftint)>();

            TxtFormatReader.ReadStandartFormat(Resources.tintblocks, (_, parts) => {
                if(parts[0] == "grass") grassTint = 0xFF000000 | Convert.ToUInt32(parts[3], 16);

                tints.Add((parts[0], parts[1], parts[2].Split(",").Select(w => w.minecraftname()).ToList(), 0xFF000000 | Convert.ToUInt32(parts[3], 16)));
            });
        }

        public static bool IsNormallyTinted(string block) {
            foreach(var tint in tints) {
                if(tint.blocks.Contains(block)) return true;
            }
            return false;
        }

        public static uint InstaTint(string block, int index, uint color) {
            if(index == -1) return color;
            if(block == "minecraft:pink_petals" && index == 1) return Global.ColorMult(color, grassTint);

            foreach(var tint in tints) {
                if(tint.blocks.Contains(block)) return Global.ColorMult(color, tint.deftint);
            }
            return color;
        }
    }
}
