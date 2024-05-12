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
                        if(Global.App.OpenedSave != null) Global.App.OpenedSave = new Save(Global.App.OpenedSave.path);
                    });
            }
        }

        protected override void OnExit(ExitEventArgs e) {
            if(!Directory.Exists(APPFOLDER)) Directory.CreateDirectory(APPFOLDER);

            string json = JsonSerializer.Serialize(this.Settings);
            File.WriteAllText(Path.Combine(APPFOLDER, "settings.json"), json);

            base.OnExit(e);
            Environment.Exit(Environment.ExitCode);
        }





        public MainWindow Window { get => (MainWindow)this.MainWindow; }
        public TileMap TileMap { get; set; }

        public Settings Settings { get; set;}

        private Save _openedSave;
        public Save OpenedSave {
            get {
                return _openedSave;
            }

            set {
                _openedSave = value;

                TileMap = _openedSave.overworld.tileMap;
                TileMap.SetSettings();

                Window.canvasControl.OnTilemapChanged();

                GC.Collect(2, GCCollectionMode.Forced);
            }
        }
    }

}
