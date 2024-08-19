using Mcasaenk;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json;
using Utils;
using System.Windows.Media.Imaging;
class Program {
    // !!!!!!!!!!!! console not showing https://github.com/dotnet/project-system/issues/6613 !!!!!!!!!!!!!!!!!!!!!!!!!!
    public static void Main(String[] args) {       
        const string vanillapack = "D:\\resource packs\\java unziped 1.21\\1.21\\1.21";
        //File.WriteAllText("D:\\abc.txt", AssetsUtils.CreateVanillaDataAsset(Path.Combine(vanillapack, "data")));

        //File.WriteAllLines("D:\\map\\javablocks.txt", AssetsUtils.GetVanillaBlockNames(vanillapack));

        //var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText("D:\\java unziped 1.21\\1.21\\1.21\\data\\minecraft\\worldgen\\biome\\badlands.json"));
        // json.TryGetValue("carvers", out var el);

        MapColormapMaker.FromBedrockMap("D:\\bedrockmap.zip", vanillapack, FileRead.ReadFromFile("D:\\map\\bedrock_img1.png"), [FileRead.ReadFromFile("D:\\map\\bedrock_img2.png"), FileRead.ReadFromFile("D:\\map\\bedrock_img3.png"), FileRead.ReadFromFile("D:\\map\\bedrock_img4.png")]);
        //MapColormapMaker.FromJavaMap("D:\\javamap.zip", vanillapack, FileRead.ReadFromFile("D:\\map\\java_img1.png"));
        //MapColormapMaker.FromResourcePacks("D:\\greenfield.zip", [vanillapack, "D:\\Greenfield.Texture.Pack.1.17"], 0);

        //ResourcepackColormapMaker.Make("D:\\test", [vanillapack, "C:\\Users\\nikol\\AppData\\Roaming\\.minecraft\\resourcepacks\\Greenfield.Texture.Pack.1.17.zip"],
        //    new Options() {
        //        minQ = 0.00,
        //        for_Q0_try_with_sides = true,
        //    }
        //);
    }
}

