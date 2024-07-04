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
                    () => {
                        TileMap?.RedrawAll();
                    },
                    () => {
                        if(Global.App.OpenedSave != null) {
                            Global.App.OpenedSave = new Save(Global.App.OpenedSave.path, Global.App.OpenedSave.levelDatInfo, Global.App.OpenedSave.datapackInfo);
                        }
                    });

                
            }

            // colormap
            {
                if(Directory.Exists(Path.Combine(APPFOLDER, "colormaps", "texture")) == false) {
                    using var stream = new MemoryStream(ResourceMapping.colormap_texture);
                    ZipFile.ExtractToDirectory(stream, Path.Combine(APPFOLDER, "colormaps", "texture"));
                }

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
                if(value != null) if(!Colormap.IsColormap(Path.Combine(APPFOLDER, "colormaps", Settings.COLOR_MAPPING_MODE))) return;

                _openedSave = value;

                if(value != null) {
                    TileMap = _openedSave.GetDimension(Global.Settings.DIMENSION).tileMap;
                    Settings.OnNewSave();
                    TileMap.SetSettings();

                    Colormap = new Colormap(Path.Combine(APPFOLDER, "colormaps", Settings.COLOR_MAPPING_MODE), OpenedSave.datapackInfo);
                    Shade3DFilter.ReInit(Colormap);

                    foreach(var tint in Colormap.GetTints()) {
                        tint.Settings()?.SetActions(() => {
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
