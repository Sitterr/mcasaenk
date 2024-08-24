using Mcasaenk.Colormaping;
using Mcasaenk.Nbt;
using Mcasaenk.Rendering;
using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.Resources;
using Mcasaenk.Shade3d;
using Mcasaenk.UI;
using Mcasaenk.UI.Canvas;
using Mcasaenk.WorldInfo;
using Microsoft.Win32;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Mcasaenk
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public string APPFOLDER = Path.Combine(Directory.GetCurrentDirectory(), "mcasaenk");
        //Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

        public const string VERSION = "1.0.3";

        public readonly string ID = "__" + Global.rand.NextString(5);

        [STAThread]
        protected override void OnStartup(StartupEventArgs e) {
            Debug.WriteLine("int main()?");
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            Thread.CurrentThread.Name = "app/ui?";
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            if(!Directory.Exists(APPFOLDER)) Directory.CreateDirectory(APPFOLDER);

            // settings
            {
                        
                var settFile = Path.Combine(APPFOLDER, "settings.json");
                if(File.Exists(settFile)) Settings = JsonSerializer.Deserialize<Mcasaenk.Settings>(File.ReadAllText(settFile));
                else Settings = Settings.DEF();


                Settings.SetActions(
                    (changed) => {
                        TileMap?.RedrawAll();

                        if(changed == nameof(Settings.DEFBIOME)) Colormap.Biome.UpdateDef();
                        if(changed == nameof(Settings.UseMapPalette)) {
                            Window.rad.ShowSlot3(this.Settings.USEMAPPALETTE);
                        }
                    },
                    (_changed) => {
                        if(_changed.Count == 0) return;
                        var changed = new List<string>(_changed);
                        Settings.Freeze();

                        _openedSave.Reset();
                        SetWorld(changed.Contains(nameof(Settings.DIMENSION)));

                        if(changed.Contains(nameof(Settings.COLOR_MAPPING_MODE))) {
                            SetColormap();
                        } else if(changed.Contains(nameof(Settings.SKIP_UNKNOWN_BLOCKS))) {
                            Colormap.Block.SetDef(Settings.SKIP_UNKNOWN_BLOCKS ? Colormap.INVBLOCK : Colormap.NONEBLOCK);
                        }

                        Settings.FinishFreeze(false);
                    });
            }

            {
                if(File.Exists(Path.Combine(APPFOLDER, "vanilla_resource_pack.zip")) == false) {
                    File.WriteAllBytes(Path.Combine(APPFOLDER, "vanilla_resource_pack.zip"), ResourceMapping.vanilla_res_pack);
                }
            }

            // colormap
            {
                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "default")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_texture);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "default"));
                }

                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "java map")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_java);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "java map"));
                }

                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "bedrock map")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_bedrock);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "bedrock map"));
                }


                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "betaplus")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_betaplus);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "betaplus"));
                }
            }

            Global.ViewModel = new ViewModel();
        }

        protected override void OnExit(ExitEventArgs e) {
            MouseHook.Stop();
            if(!Directory.Exists(APPFOLDER)) Directory.CreateDirectory(APPFOLDER);

            string json = JsonSerializer.Serialize(this.Settings, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(Path.Combine(APPFOLDER, "settings.json"), json);

            if(Path.Exists(Path.Combine(APPFOLDER, ID))) Directory.Delete(Path.Combine(APPFOLDER, ID), true);

            base.OnExit(e);
            Environment.Exit(Environment.ExitCode);
        }




        public double RAND;
        public MainWindow Window { get => (MainWindow)this.MainWindow; }
        public TileMap TileMap { get; set; }
        public Settings Settings { get; set; }
        public Colormap Colormap { get; set; }

        private Save _openedSave;
        public Save OpenedSave { // hard reset
            get {
                return _openedSave;
            }

            set {
                Settings.Freeze();

                _openedSave = value;
                if(value != null) {
                    SetWorld(true);
                    if(_openedSave.levelDatInfo.mods.Length > 0) {
                        Settings.COLOR_MAPPING_MODE = "default";
                    }
                    if(Path.Exists(Settings.COLOR_MAPPING_MODE) == false) {
                        Settings.COLOR_MAPPING_MODE = "default";
                    }
                    SetColormap();
                    
                }

                Settings.FinishFreeze(false);
            }
        }

        void SetColormap() {
            if(OpenedSave == null) return;

            Colormap = new Colormap(RawColormap.Load(Settings.ColormapToPath(Settings.COLOR_MAPPING_MODE)), OpenedSave.levelDatInfo.version_id, OpenedSave.datapackInfo);
            Shade3DFilter.ReInit(Colormap);

            foreach(var tint in Colormap.GetTints()) {
                tint.Settings()?.SetActions(() => {
                    TileMap?.RedrawAll();
                });
            }

            Window.OnColormapChange();
        }
        void SetWorld(bool dimchange) {
            RAND = Global.rand.NextDouble();

            {
                if(_openedSave.GetDimension(Global.Settings.DIMENSION) == null) {
                    Global.Settings.DIMENSION = "minecraft:overworld";
                }
                var h = _openedSave.GetDimension(Global.Settings.DIMENSION).GetHeight();
                Settings.MINY = h.miny;
                Settings.MAXABSHEIGHT = h.height;
                Settings.Y_OFFICIAL = dimchange ? h.defheight : Settings.Y_OFFICIAL;
            }

            ShadeConstants.GLB = new ShadeConstants(Settings.MAXABSHEIGHT, Settings.ADEG, Settings.BDEG);

            TileMap = _openedSave.GetDimension(Global.Settings.DIMENSION).tileMap;
            TileMap.SetSettings();

            Window.OnHardReset();
            Window.canvasControl.OnTilemapChanged(dimchange);

            GC.Collect(2, GCCollectionMode.Forced);

            Settings.OnAutoChange(nameof(Settings.MINY));
            Settings.OnAutoChange(nameof(Settings.MAXY));
            Settings.OnAutoChange(nameof(Settings.MAXABSHEIGHT));
            Settings.OnAutoChange(nameof(Settings.ABSY));
        }

        public void Reset() {
            SetWorld(false);
        }
    }
}
