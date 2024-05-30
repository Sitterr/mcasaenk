using Mcasaenk.Nbt;
using Mcasaenk.Rendering;
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


        [STAThread]
        protected override void OnStartup(StartupEventArgs e) {
            Debug.WriteLine("int main()?");
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            if(!Directory.Exists(APPFOLDER)) Directory.CreateDirectory(APPFOLDER);

            // settings
            {        
                var settFile = Path.Combine(APPFOLDER, "settings.json");
                if(File.Exists(settFile)) Settings = JsonSerializer.Deserialize<Mcasaenk.Settings>(File.ReadAllText(settFile));
                else Settings = Settings.DEF();

                Settings.SetActions(
                    () => {
                        TileMap?.RedrawAll();
                    },
                    () => {
                        if(Global.App.OpenedSave is DimensionSave ds) Global.App.OpenedSave = new DimensionSave(ds.path);
                        else if(Global.App.OpenedSave is Save) Global.App.OpenedSave = new Save(Global.App.OpenedSave.path);
                    });
            }

            // colormap
            {
                BiomeRegistry.Initialize();

                if(Colormap.IsColormap(Path.Combine(APPFOLDER, "colormaps", "mean")) == false) {
                    if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "mean"))) 
                        Directory.Delete(Path.Combine(APPFOLDER, "colormaps", "mean"));
                }
                if(Colormap.IsColormap(Path.Combine(APPFOLDER, "colormaps", "java map")) == false) {
                    if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "java map")))
                        Directory.Delete(Path.Combine(APPFOLDER, "colormaps", "java map"));
                }

                Directory.CreateDirectory(Path.Combine(APPFOLDER, "colormaps", "java map"));
                Directory.CreateDirectory(Path.Combine(APPFOLDER, "colormaps", "mean"));

                string path;

                path = Path.Combine(APPFOLDER, "colormaps", "java map", "colormap.json");
                if(!File.Exists(path)) File.WriteAllBytes(path, ResourceMapping.javamap_colormap);

                path = Path.Combine(APPFOLDER, "colormaps", "mean", "colormap.json");
                if(!File.Exists(path)) File.WriteAllBytes(path, ResourceMapping.mean_colormap);

                path = Path.Combine(APPFOLDER, "colormaps", "mean", "foliage.png");
                if(!File.Exists(path)) File.WriteAllBytes(path, ResourceMapping.mean_foliage);

                path = Path.Combine(APPFOLDER, "colormaps", "mean", "grass.png");
                if(!File.Exists(path)) File.WriteAllBytes(path, ResourceMapping.mean_grass);

                path = Path.Combine(APPFOLDER, "colormaps", "mean", "water.png");
                if(!File.Exists(path)) File.WriteAllBytes(path, ResourceMapping.mean_water);
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





        public MainWindow Window { get => (MainWindow)this.MainWindow; }
        public TileMap TileMap { get; set; }
        public Settings Settings { get; set; }
        public Colormap Colormap { get; set; }

        private Save _openedSave;
        public Save OpenedSave {
            get {
                return _openedSave;
            }

            set {
                if(value != null) if(!Colormap.IsColormap(Path.Combine(APPFOLDER, "colormaps", Settings.COLOR_MAPPING_MODE))) return;

                _openedSave = value;

                if(value != null) {
                    TileMap = _openedSave.GetDimension(Global.Settings.DIMENSION).tileMap;
                    TileMap.SetSettings();

                    Colormap = new Colormap(Path.Combine(APPFOLDER, "colormaps", Settings.COLOR_MAPPING_MODE));

                    foreach(var tint in Colormap.GetTints()) {
                        tint.settings = DynamicTintSettings.DEF();
                        tint.settings.SetActions(() => {
                            TileMap?.RedrawAll();
                        });
                    }
                }

                Window.OnHardReset();
                Window.canvasControl.OnTilemapChanged();

                GC.Collect(2, GCCollectionMode.Forced);
            }
        }
    }

}
