using Mcasaenk;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Drawing;
using Utils;
class Program { 
    public static void Main(String[] args) {
        const string vanillapack = "D:\\java unziped 1.21\\1.21\\1.21";
        //File.WriteAllLines("D:\\map\\javablocks.txt", AssetsUtils.GetVanillaBlockNames(vanillapack));

        ColormapMaker.FromBedrockMap("D:\\bedrockmap.zip", vanillapack, new Bitmap("D:\\map\\bedrock_img1.png"), [new Bitmap("D:\\map\\bedrock_img2.png"), new Bitmap("D:\\map\\bedrock_img3.png"), new Bitmap("D:\\map\\bedrock_img4.png")]);
        //ColormapMaker.FromJavaMap("D:\\javamap.zip", vanillapack, new Bitmap("D:\\map\\java_img1.png"));
        //ColormapMaker.FromResourcePacks("D:\\texture.zip", [vanillapack], 0);
    }
}


