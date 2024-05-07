using Mcasaenk.Shade3d;
using Mcasaenk.UI;
using Mcasaenk.UI.Canvas;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Text;
using System.Windows;

namespace Mcasaenk {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        [STAThread]
        protected override void OnStartup(StartupEventArgs e) {
            Debug.WriteLine("int main()?");
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            Settings = Settings.DEF();
            Settings.SetActions(
                () => {
                    if(ShadeConstants.GLB.Adeg != Settings.ADEG || ShadeConstants.GLB.Bdeg != Settings.BDEG)
                        ShadeConstants.GLB = new ShadeConstants(Global.App.Settings.ADEG, Global.App.Settings.BDEG);

                    TileMap?.RedrawAll();
                }, 
                () => {
                    if(ShadeConstants.GLB.Adeg != Settings.ADEG || ShadeConstants.GLB.Bdeg != Settings.BDEG)
                        ShadeConstants.GLB = new ShadeConstants(Global.App.Settings.ADEG, Global.App.Settings.BDEG);

                    if(Global.App.OpenedSave != null) Global.App.OpenedSave = new Save(Global.App.OpenedSave.path);
                });
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
