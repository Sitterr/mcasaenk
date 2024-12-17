using Mcasaenk;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json;
using Utils;
using System.Windows.Media.Imaging;
using Mcasaenk.Colormaping;
class Program {
    // !!!!!!!!!!!! console not showing https://github.com/dotnet/project-system/issues/6613 !!!!!!!!!!!!!!!!!!!!!!!!!!
    public static void Main(String[] args) {       
        const string vanillapack = "D:\\resource packs\\1.21.4\\1.21.4";
        //File.WriteAllText("D:\\map2\\vanilladata.txt", AssetsUtils.CreateVanillaDataAsset(Path.Combine(vanillapack, "data")));

        //File.WriteAllLines("D:\\map2\\javablocks.txt", AssetsUtils.GetVanillaBlockNames(vanillapack));


        MapColormapMaker.FromBedrockMap("D:\\map2\\bedrockmap2.zip", vanillapack, FileRead.ReadFromFile("D:\\map2\\map1.png"), [FileRead.ReadFromFile("D:\\map2\\map2.png"), FileRead.ReadFromFile("D:\\map2\\map3.png"), FileRead.ReadFromFile("D:\\map2\\map4.png")]);
        //MapColormapMaker.FromJavaMap("D:\\map2\\javamap.zip", vanillapack, FileRead.ReadFromFile("D:\\map2\\map1.png"));

        //RawColormap.Save(ResourcepackColormapMaker.Make([new FileRead(vanillapack)],
        //    new Options() {
        //        minQ = 0.00,
        //    }
        //), "D:\\map2\\texture.zip");
        
    }
}

