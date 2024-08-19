using Mcasaenk.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Mcasaenk.Global;

namespace Mcasaenk.Colormaping {
    public struct Options {
        public double minQ = 0;
        public Options() { }
    }
    public enum BlockCreationMethod { Unknown, AboveQ, Sides, Texture, None }
    public class CreationDetails {
        public bool shouldTint;
        public BlockCreationMethod creationMethod = BlockCreationMethod.Unknown;
    }
    public static class ResourcepackColormapMaker {

        // the resource packs order is reverse to the minecraft way, the first has the least priority
        public static RawColormap Make(ReadInterface[] resourcepacks, Options options) {
            ReadInterface[] reversepacks = resourcepacks.Reverse().ToArray();
            RawColormap colormap = new RawColormap();

            options.minQ = Math.Round(options.minQ, 3);

            Dictionary<string, JsonModel> models = new Dictionary<string, JsonModel>();

            // blocks
            {
                var state_to_model = BlockStatesToModel();

                foreach(var f in state_to_model) {
                    string blockname = f.Key;

                    Dictionary<string, (WPFBitmap image, string loc)> textures = new();
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
                                    if(rightLocation<WPFBitmap>(toLocation(t.Value, "textures", "png"), out var bitmap)) {
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
                            colormap.blocks.Add(blockname, new RawBlock() { color = WPFColor.Transparent, details = new CreationDetails() { shouldTint = false, creationMethod = BlockCreationMethod.None } });
                        } else {
                            ConsoleColor color = ConsoleColor.DarkYellow;

                            var answer = ColorOfTexture(textures.First().Value.image);
                            colormap.blocks.Add(blockname, new RawBlock() { color = answer, details = new CreationDetails() { shouldTint = VanillaTints.IsNormallyTinted(blockname), creationMethod = answer.A > 0 ? BlockCreationMethod.Texture : BlockCreationMethod.None } });
                        }

                    } else {
                        if(el.textures != null) {
                            foreach(var t in el.textures) {
                                if(t.Value.StartsWith("#")) {
                                    textures[t.Key] = textures[t.Value.Substring(1)];
                                } else {
                                    if(rightLocation<WPFBitmap>(toLocation(t.Value, "textures", "png"), out var bitmap)) {
                                        textures[t.Key] = (bitmap, t.Value);
                                    }
                                }
                            }
                        }
                        var answer = JsonModel.ReadTopDown(blockname, el.elements, textures);


                        if(answer.q >= options.minQ) {
                            if(answer.q > 0) {
                                colormap.blocks.Add(blockname, new RawBlock() { color = answer.topdowncolor, details = new CreationDetails() { shouldTint = answer.tintedindex != -1, creationMethod = BlockCreationMethod.AboveQ } });
                            } else {
                                var sideanswer = JsonModel.ReadSide(blockname, el.elements, textures);
                                if(sideanswer.q > 0) {
                                    colormap.blocks.Add(blockname, new RawBlock() { color = sideanswer.sidecolor, details = new CreationDetails() { shouldTint = sideanswer.tintindex != -1, creationMethod = BlockCreationMethod.Sides } });
                                }
                            }
                        }


                    }

                }
            }


            // tints
            {
                var blocktint = new Dictionary<string, int>();

                // vanilla tints
                string defcolormappath = Path.Combine("assets", "minecraft", "textures", "colormap");
                foreach(var vtint in VanillaTints.tints) {

                    WPFBitmap source = null;
                    foreach(var pack in reversepacks) {
                        string pngname = vtint.name switch {
                            "grass" => "grass.png",
                            "foliage" => "foliage.png",
                            _ => "",
                        };
                        string ppp = Path.Combine(defcolormappath, pngname);
                        if(pack.ExistsFile(ppp) && ppp.EndsWith(".png")) {
                            source = pack.ReadBitmap(ppp);
                            break;
                        }
                    }

                    colormap.tints.Add(new RawTint() { name = vtint.name, format = vtint.format, image = source, color = vtint.color });
                }
                int vtintsi = colormap.tints.Count;
                foreach(var bl in colormap.blocks.Where(l => l.Value.details.shouldTint)) {
                    int tint = -1;
                    for(int i = 0; i < VanillaTints.tints.Count; i++) {
                        if(VanillaTints.tints[i].blocks.Contains(bl.Key)) {
                            tint = i;
                            break;
                        }
                    }
                    blocktint.Add(bl.Key, tint);
                }

                // optifine tints
                foreach(var pack in reversepacks) {
                    var colormapdir = Path.Combine("assets", "minecraft", "optifine", "colormap");
                    if(!pack.ExistsFile(colormapdir)) continue;

                    // custom
                    {
                        if(pack.ExistsFile(Path.Combine(colormapdir, "custom"))) {
                            foreach(var file in Directory.GetFiles(Path.Combine(colormapdir, "custom"), "*.properties")) {
                                var r = Tint.ReadTint(pack, file);

                                colormap.tints.Add(r);
                                foreach(var bl in r.blocks) {
                                    blocktint[bl] = colormap.tints.Count - 1;
                                }
                            }
                        }

                        if(pack.ExistsFile(Path.Combine(colormapdir, "blocks"))) {
                            foreach(var file in Directory.GetFiles(Path.Combine(colormapdir, "blocks"), "*.properties")) {
                                var r = Tint.ReadTint(pack, file, Path.Combine(colormapdir, "custom"));

                                colormap.tints.Add(r);
                                foreach(var bl in r.blocks) {
                                    blocktint[bl] = colormap.tints.Count - 1;
                                }
                            }
                        }
                    }


                    // static
                    {
                        {
                            if(pack.ExistsFile(Path.Combine(colormapdir, "swampgrass.properties")) || pack.ExistsFile(Path.Combine(colormapdir, "swampgrass.png"))) {
                            }
                        }
                        {
                            if(pack.ExistsFile(Path.Combine(colormapdir, "swampfoliage.properties")) || pack.ExistsFile(Path.Combine(colormapdir, "swampfoliage.png"))) {
                            }
                        }

                        {
                            string path = Path.Combine("assets", "minecraft", "optifine", "color.properties");
                            if(pack.ExistsFile(path)) {
                                var lines = pack.ReadAllLines(path).Select(l => l.Split("=").Select(p => p.Trim()).ToArray());
                                foreach(var line in lines) {
                                    if(line[0] == "lilypad") {
                                        colormap.tints.Add(new RawTint() { name = "lily_pad", format = "fixed", blocks = null, color = WPFColor.FromHex(line[1]) });
                                        blocktint["minecraft:lily_pad"] = colormap.tints.Count - 1;
                                    }
                                }
                            }
                        }

                        {
                            RawTint tint = null;
                            if(pack.ExistsFile(Path.Combine(colormapdir, "pine.properties"))) {
                                tint = Tint.ReadTint(pack, Path.Combine(colormapdir, "pine.properties"));
                            } else if(pack.ExistsFile(Path.Combine(colormapdir, "pine.png"))) {
                                tint = new RawTint() { name = "pine", format = "vanilla", blocks = null, image = pack.ReadBitmap(Path.Combine(colormapdir, "pine.png")), color = WPFColor.White };
                            }

                            if(tint != null) {
                                colormap.tints.Add(tint);
                                blocktint["minecraft:spruce_leaves"] = colormap.tints.Count - 1;
                            }
                        }

                        {
                            RawTint tint = null;
                            if(pack.ExistsFile(Path.Combine(colormapdir, "birch.properties"))) {
                                tint = Tint.ReadTint(pack, Path.Combine(colormapdir, "birch.properties"));
                            } else if(pack.ExistsFile(Path.Combine(colormapdir, "birch.png"))) {
                                tint = new RawTint() { name = "birch", format = "vanilla", blocks = null, image = pack.ReadBitmap(Path.Combine(colormapdir, "birch.png")), color = WPFColor.White };
                            }

                            if(tint != null) {
                                colormap.tints.Add(tint);
                                blocktint["minecraft:birch_leaves"] = colormap.tints.Count - 1;
                            }
                        }

                        {
                            RawTint tint = null;
                            if(pack.ExistsFile(Path.Combine(colormapdir, "water.properties"))) {
                                tint = Tint.ReadTint(pack, Path.Combine(colormapdir, "water.properties"));
                            } else if(pack.ExistsFile(Path.Combine(colormapdir, "water.png"))) {
                                tint = new RawTint() { name = "water", format = "vanilla", blocks = null, image = pack.ReadBitmap(Path.Combine(colormapdir, "water.png")), color = WPFColor.White };
                            }

                            if(tint != null) {
                                colormap.tints.Add(tint);
                                blocktint["minecraft:water"] = colormap.tints.Count - 1;
                            }
                        }

                        {
                            RawTint tint = null;
                            if(pack.ExistsFile(Path.Combine(colormapdir, "redstone.png"))) {
                                tint = new RawTint() { name = "redstone_wire", format = "fixed", blocks = null, image = null, color = ColorOfTexture(pack.ReadBitmap(Path.Combine(colormapdir, "redstone.png"))) };
                            }

                            if(tint != null) {
                                colormap.tints.Add(tint);
                                blocktint["minecraft:redstone_wire"] = colormap.tints.Count - 1;
                            }
                        }

                        {
                            RawTint tint = null;
                            if(pack.ExistsFile(Path.Combine(colormapdir, "pumpkinstem.png"))) {
                                tint = new RawTint() { name = "pumpkin", format = "fixed", blocks = null, image = null, color = ColorOfTexture(pack.ReadBitmap(Path.Combine(colormapdir, "pumpkinstem.png"))) };
                            }

                            if(tint != null) {
                                colormap.tints.Add(tint);
                                blocktint["minecraft:pumpkin_stem"] = colormap.tints.Count - 1;
                                blocktint["minecraft:attached_pumpkin_stem"] = colormap.tints.Count - 1;
                            }
                        }

                        {
                            RawTint tint = null;
                            if(pack.ExistsFile(Path.Combine(colormapdir, "melonstem.png"))) {
                                tint = new RawTint() { name = "melon", format = "fixed", blocks = null, image = null, color = ColorOfTexture(pack.ReadBitmap(Path.Combine(colormapdir, "melonstem.png"))) };
                            }

                            if(tint != null) {
                                colormap.tints.Add(tint);
                                blocktint["minecraft:melon_stem"] = colormap.tints.Count - 1;
                                blocktint["minecraft:attached_melon_stem"] = colormap.tints.Count - 1;
                            }
                        }
                    }
                }

                var blocktintf = blocktint.Where(b => colormap.blocks.Any(bl => bl.Key == b.Key)).ToList();
                for(int i = 0; i < colormap.tints.Count; i++) {
                    var blocksforthistint = blocktintf.Where(bl => bl.Value == i).Select(bl => bl.Key).ToList();
                    colormap.tints[i].blocks = blocksforthistint;
                    if(i >= vtintsi) {
                        colormap.tints[i].name += "_optifine";
                    }
                }
                colormap.tints = colormap.tints.Where(t => t.blocks.Count > 0).ToList();
            }



            return colormap;

















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
                if(object_type == "models" && path.Contains("/") == false) {
                    path = "block/" + path;
                }

                return $"assets/{mnamespace}/{object_type}/{path}.{suffix}";
            }
            bool rightLocation<T>(string loc, out T file) {
                file = default;
                foreach(var pack in reversepacks) {
                    if(pack.ExistsFile(loc)) {
                        if(Path.GetExtension(loc) == ".json") {
                            try {
                                file = JsonSerializer.Deserialize<T>(pack.ReadAllText(loc), new JsonSerializerOptions() { IncludeFields = true });
                                return true;
                            }
                            catch {
                                return false;
                            }
                        } else if(typeof(T) == typeof(WPFBitmap)) {
                            file = (T)(object)pack.ReadBitmap(loc);
                            return true;
                        }
                    }
                }
                return false;
            }


            //void SaveTint((string name, string format, WPFBitmap source, uint color) tint, string[] blocks, bool optifine = false) {
            //    if(tint.format == "fixed" && tint.color == uint.MaxValue) return;
            //    if(optifine) tint.name += "_optifine";
            //    List<string> lines = new List<string>();

            //    lines.Add($"format={tint.format}");
            //    if(!(tint.format == "fixed" && blocks.Length == 1 && blocks[0].minecraftname() == tint.name.minecraftname())) lines.Add($"blocks={string.Join(" ", blocks.Select(bl => bl.simplifyminecraftname()))}");
            //    if(tint.source != null) lines.Add($"source={tint.name}.png");
            //    if(tint.format == "fixed") lines.Add($"color={tint.color.ToString("X").Substring(2)}");

            //    if(tint.source != null) output.SaveImage(tint.name + ".png", tint.source);
            //    output.SaveLines(tint.name + ".properties", lines);
            //}


            Dictionary<string, string> BlockStatesToModel() {
                Dictionary<string, string> dict = new();
                foreach(var resourcepack in reversepacks) {
                    foreach(Match match in new Regex("assets/(.+)/blockstates/(.+)\\.json", RegexOptions.Multiline).Matches(resourcepack.AllEntries())) {
                        string mnamespace = match.Groups[1].Value;
                        string filename = match.Groups[2].Value;

                        string name = mnamespace + ":" + filename;
                        string model = ReadModelFromBlockState(resourcepack.ReadAllText(match.Value));
                        if(model != null && model != "") dict[name] = model;
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


            WPFColor ColorOfTexture(WPFBitmap image) {
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
                if(br == 0) return WPFColor.Transparent;

                return WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br));
            }

            return colormap;
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

        public static (WPFColor topdowncolor, double q, int tintedindex) ReadTopDown(string block, JsonElement[] elements, Dictionary<string, (WPFBitmap image, string loc)> textures) {
            (WPFColor color, int y, int tintindex)[,] topdown = new (WPFColor color, int y, int tintindex)[16, 16];
            for(int i = 0; i < 16 * 16; i++) topdown[i / 16, i % 16] = (WPFColor.Transparent, -1, -1);

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
                                topdown[x, z].color = pixel;
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
                var argb = topdown[i / 16, i % 16].color;
                if(argb.A == 0) continue;

                r += argb.R;
                g += argb.G;
                b += argb.B;
                br++;
            }
            if(br == 0) return (WPFColor.Transparent, 0, -1);

            return (WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)), Math.Round(br / 256d, 2), ft);
        }

        public static (WPFColor sidecolor, double q, int tintindex) ReadSide(string block, JsonElement[] elements, Dictionary<string, (WPFBitmap image, string loc)> textures) {
            int tintedindex = -1;
            bool instatint = false, first = true;

            int r = 0, g = 0, b = 0, br = 0;
            foreach(var element in elements) {
                foreach(var face in element.faces) {
                    if((face.Key == "north" || face.Key == "south" || face.Key == "west" || face.Key == "east") == false) continue;

                    if(!first) {
                        if(tintedindex != face.Value.tintindex && !instatint && br > 0) {
                            var res = VanillaTints.InstaTint(block, tintedindex, WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)));
                            r = res.R * br;
                            g = res.G * br;
                            b = res.B * br;
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
                            if(instatint) pixel = VanillaTints.InstaTint(block, face.Value.tintindex, pixel);
                            r += pixel.R;
                            g += pixel.G;
                            b += pixel.B;
                            br++;
                            break;
                        }
                    }

                    first = false;
                }
            }


            if(br == 0) return (WPFColor.Transparent, 0, -1);

            return (WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)), Math.Round(br / 256d, 2), tintedindex);
        }
    }


    static class VanillaTints {
        static WPFColor grassTint;
        public readonly static List<RawTint> tints;
        static VanillaTints() {
            tints = new List<RawTint>();

            TxtFormatReader.ReadStandartFormat(ResourceMapping.tintblocks, (_, parts) => {
                if(parts[0] == "grass") grassTint = WPFColor.FromHex(parts[3]);

                tints.Add(new RawTint() { name = parts[0], format = parts[1], blocks = parts[2].Split(",").Select(w => w.minecraftname()).ToList(), color = WPFColor.FromHex(parts[3]) });
            });
        }

        public static bool IsNormallyTinted(string block) {
            foreach(var tint in tints) {
                if(tint.blocks.Contains(block)) return true;
            }
            return false;
        }

        public static WPFColor InstaTint(string block, int index, WPFColor color) {
            if(index == -1) return color;
            if(block == "minecraft:pink_petals" && index == 1) return ColorMult(color, grassTint);

            foreach(var tint in tints) {
                if(tint.blocks.Contains(block)) return ColorMult(color, tint.color);
            }
            return color;
        }
    }
}