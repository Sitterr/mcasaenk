using Mcasaenk;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json;
using Utils;
class Program { 
    public static void Main(String[] args) {

        ColormapMaker.FromBedrockMap("D:\\map\\bedrock map", "D:\\map\\bedrock.png", "D:\\map\\javablocks.txt", "D:\\map\\javabiomes.txt", "D:\\map\\tintblocks.txt", ["D:\\map\\b1.png", "D:\\map\\b2.png", "D:\\map\\b3.png"]);

        //ColormapMaker.FromJavaMap("D:\\map\\java map", "D:\\map\\javamap.png", "D:\\map\\javablocks.txt");

        // Console.Write(AssetsUtils.GenerateMeanBlockColors("D:\\java unziped 1.21\\1.21\\assets"));
    }
}


