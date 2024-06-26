using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utils {
    internal class AssetsUtils {

        public static List<(string id, string texture)> GetBlockNames(string vers_assets) {
            List<(string id, string model)> idtomodel = new();
            foreach(var file in Directory.GetFiles(Path.Combine(vers_assets, "minecraft", "blockstates"))) { // blockstates
                string jsonString = File.ReadAllText(file);
                var jsonDocument = JsonDocument.Parse(jsonString);
                var root = jsonDocument.RootElement;

                string modelname = "";
                if(root.TryGetProperty("variants", out var variants)) {
                    JsonElement variant;
                    if(!variants.TryGetProperty("", out variant)) {
                        variant = variants.EnumerateObject().First().Value;
                    }
                    modelname = variant.getObjectOrFirstElement("model").GetString();
                } else if(root.TryGetProperty("multipart", out var multipart)) {
                    var firstEl = multipart.EnumerateArray().First();
                    modelname = firstEl.GetProperty("apply").getObjectOrFirstElement("model").GetString();
                }

                idtomodel.Add((Path.GetFileNameWithoutExtension(file), namespaceToLocation(vers_assets, "models", modelname)));
            }

            List<(string id, string texture)> idtotexture = new();
            foreach(var pair in idtomodel) { // models
                string jsonString = File.ReadAllText(pair.model + ".json");
                var jsonDocument = JsonDocument.Parse(jsonString);
                var root = jsonDocument.RootElement;

                string texturename = "";

                if(root.TryGetProperty("textures", out var texturesEl)) {
                    if(texturesEl.TryGetProperty("texture", out var textr)) {
                        texturename = textr.GetString();
                    } else {

                        foreach(var type in texturesEl.EnumerateObject()) {
                            if(type.Name == "top") {
                                texturename = type.Value.GetString();
                                goto _endofloop;
                            }
                        }
                        texturename = texturesEl.EnumerateObject().First().Value.GetString();
                    }
                }


            _endofloop: { }

                idtotexture.Add((pair.id, namespaceToLocation(vers_assets, "textures", texturename)));
            }

            return idtotexture;
        }

        public static string GenerateMeanBlockColors(string vers_assets) {
            var idtotexture = GetBlockNames(vers_assets);

            const double q = 1.01;
            string @return = "";
            foreach(var pair in idtotexture) { // textures
                if(!File.Exists(pair.texture + ".png")) continue;

                var bytes = bitmapToByteArray(new Bitmap(pair.texture + ".png"));
                int pixelCount = bytes.Length / 4;

                int r = 0, g = 0, b = 0;

                int numberOfTransparent = 0;
                for(int i = 0; i < bytes.Length; i += 4) {
                    if(bytes[i + 3] == 0) numberOfTransparent++;
                    else {
                        r += bytes[i + 2];
                        g += bytes[i + 1];
                        b += bytes[i + 0];
                    }
                }

                if(numberOfTransparent > q * pixelCount) {
                    continue;
                } else {
                    r /= (pixelCount - numberOfTransparent);
                    g /= (pixelCount - numberOfTransparent);
                    b /= (pixelCount - numberOfTransparent);
                }


                int color = (r << 16) | (g << 8) | (b);

                @return += pair.id/* + ";" + color.ToString("x")*/ + '\n';
            }

            return @return;
        }


        static string namespaceToLocation(string vers_assets, string subfolder, string @namespace) {
            if(!@namespace.Contains(":")) @namespace = "minecraft:" + @namespace;
            Regex modelNameRegex = new Regex("(.+):(.+)\\/(.+)");

            var res = modelNameRegex.Match(@namespace);



            return Path.Combine(vers_assets, res.Groups[1].Value, subfolder, res.Groups[2].Value, res.Groups[3].Value);
        }
        static byte[] bitmapToByteArray(Bitmap bitmap) {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);

            bitmap.UnlockBits(bmpData);

            return rgbValues;
        }

    }
}

static class Extentions {
    public static JsonElement getObjectOrFirstElement(this JsonElement element, string objectName) {
        if(element.ValueKind == JsonValueKind.Array) {
            return element.EnumerateArray().First().GetProperty(objectName);
        }
        return element.GetProperty(objectName);
    }
}