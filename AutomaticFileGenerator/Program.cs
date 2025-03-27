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
        //const string vanillapack = "D:\\resource packs\\1.21.5";
        //File.WriteAllText("D:\\map3\\vanilladata.txt", AssetsUtils.CreateVanillaDataAsset("C:\\Users\\nikol\\AppData\\Roaming\\.minecraft\\versions\\1.21.5\\data"));

        //File.WriteAllLines("D:\\map3\\javablocks.txt", AssetsUtils.GetVanillaBlockNames(vanillapack));


        //MapColormapMaker.FromBedrockMap("D:\\1.21.5\\bedrockmap.zip", File.ReadAllLines("D:\\map3\\javablocks.txt"), File.ReadAllText("D:\\map3\\tintblocks.txt"), FileRead.ReadFromFile("D:\\map3\\map1.png"), [FileRead.ReadFromFile("D:\\map3\\map2_.png"), FileRead.ReadFromFile("D:\\map3\\map3_.png"), FileRead.ReadFromFile("D:\\map3\\map4_.png")]);
        //MapColormapMaker.FromJavaMap("D:\\1.21.5\\javamap.zip", File.ReadAllLines("D:\\map3\\javablocks.txt"), FileRead.ReadFromFile("D:\\map3\\javamap.png"));

        //RawColormap.Save(ResourcepackColormapMaker.Make([new FileRead(vanillapack)], new Options() { }), "D:\\1.21.5\\texture.zip");
    }
}

