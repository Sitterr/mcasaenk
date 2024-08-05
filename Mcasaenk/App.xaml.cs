using Mcasaenk.Nbt;
using Mcasaenk.Rendering;
using Mcasaenk.Rendering.ChunkRenderData;
using Mcasaenk.Resources;
using Mcasaenk.Shade3d;
using Mcasaenk.UI;
using Mcasaenk.UI.Canvas;
using Microsoft.Win32;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Mcasaenk {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public string APPFOLDER = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "mcasaenk");

        public const string VERSION = "0.9.0";

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

                        if(changed.Contains(nameof(Settings.COLOR_MAPPING_MODE))) {
                            SetColormap();
                        }

                        _openedSave = new Save(Global.App.OpenedSave.path, Global.App.OpenedSave.levelDatInfo, Global.App.OpenedSave.datapackInfo);
                        SetWorld();

                        Settings.FinishFreeze(false);
                    });
            }

            // colormap
            {
                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "default")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_texture);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "default"));
                }

                //if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "beta")) == false) {
                //    using var stream = new MemoryStream(ResourceMapping.colormap_beta);
                //    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "beta"));
                //}

                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "java map")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_java);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "java map"));
                }

                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "bedrock map")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_bedrock);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "bedrock map"));
                }
            }

            Global.ViewModel = new ViewModel();
        }

        protected override void OnExit(ExitEventArgs e) {
            if(!Directory.Exists(APPFOLDER)) Directory.CreateDirectory(APPFOLDER);

            string json = JsonSerializer.Serialize(this.Settings, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(Path.Combine(APPFOLDER, "settings.json"), json);

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
                    SetColormap();
                    SetWorld();

                    Settings.Y_OFFICIAL = Settings.MAXY;
                }

                Settings.FinishFreeze(false);
            }
        }

        void SetColormap() {
            if(OpenedSave == null) return;

            if(!Path.Exists(Path.Combine(APPFOLDER, "colormaps", Settings.COLOR_MAPPING_MODE))) Settings.COLOR_MAPPING_MODE = Settings.DEF().COLOR_MAPPING_MODE;

            Colormap = new Colormap(Path.Combine(APPFOLDER, "colormaps", Settings.COLOR_MAPPING_MODE), OpenedSave.levelDatInfo.version_id, OpenedSave.datapackInfo);
            Shade3DFilter.ReInit(Colormap);

            foreach(var tint in Colormap.GetTints()) {
                tint.Settings()?.SetActions(() => {
                    TileMap?.RedrawAll();
                });
            }

            Window.OnColormapChange();
        }
        void SetWorld() {
            RAND = Global.rand.NextDouble();

            {
                if(_openedSave.GetDimension(Global.Settings.DIMENSION) == null) {
                    Global.Settings.DIMENSION = Settings.DEF().DIMENSION;
                }
                var h = _openedSave.GetDimension(Global.Settings.DIMENSION).GetHeight();
                Settings.MINY = (short)h.miny;
                Settings.MAXABSHEIGHT = (short)h.height;
                Settings.Y_OFFICIAL = Settings.Y_OFFICIAL;
            }

            ShadeConstants.GLB = new ShadeConstants(Settings.MAXABSHEIGHT, Settings.ADEG, Settings.BDEG);

            TileMap = _openedSave.GetDimension(Global.Settings.DIMENSION).tileMap;
            TileMap.SetSettings();

            Window.OnHardReset();
            Window.canvasControl.OnTilemapChanged();

            GC.Collect(2, GCCollectionMode.Forced);

            Settings.OnAutoChange(nameof(Settings.MINY));
            Settings.OnAutoChange(nameof(Settings.MAXY));
            Settings.OnAutoChange(nameof(Settings.MAXABSHEIGHT));
            Settings.OnAutoChange(nameof(Settings.ABSY));
        }
    }
}
