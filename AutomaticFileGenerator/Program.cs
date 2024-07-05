using Mcasaenk;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Drawing;
using Utils;
class Program {
    // !!!!!!!!!!!! console not showing https://github.com/dotnet/project-system/issues/6613 !!!!!!!!!!!!!!!!!!!!!!!!!!
    public static void Main(String[] args) {       
        const string vanillapack = "D:\\java unziped 1.21\\1.21\\1.21";
        //File.WriteAllLines("D:\\map\\javablocks.txt", AssetsUtils.GetVanillaBlockNames(vanillapack));

        //var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText("D:\\java unziped 1.21\\1.21\\1.21\\data\\minecraft\\worldgen\\biome\\badlands.json"));
       // json.TryGetValue("carvers", out var el);

        //ColormapMaker.FromBedrockMap("D:\\bedrockmap.zip", vanillapack, new Bitmap("D:\\map\\bedrock_img1.png"), [new Bitmap("D:\\map\\bedrock_img2.png"), new Bitmap("D:\\map\\bedrock_img3.png"), new Bitmap("D:\\map\\bedrock_img4.png")]);
        //ColormapMaker.FromJavaMap("D:\\javamap.zip", vanillapack, new Bitmap("D:\\map\\java_img1.png"));
        //ColormapMaker.FromResourcePacks("D:\\greenfield.zip", [vanillapack, "D:\\Greenfield.Texture.Pack.1.17"], 0);
    }
}


