using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime;
using System.Text.Json;
using System.Windows;
using Mcasaenk.Colormaping;
using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using Mcasaenk.Shade3d;
using Mcasaenk.UI;

namespace Mcasaenk {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public string APPFOLDER = Path.Combine(Directory.GetCurrentDirectory(), "mcasaenk");
        //Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

        public const string VERSION = "1.3.0", MINECRAFTVERSION = "1.21.5";

        public readonly string ID = "__" + Global.rand.NextString(5);


        private void OnAutoChange(string changed) {
            if(changed.Contains(nameof(Settings.ENABLE_COLORMAP_EDITING))) {
                Global.App.Window?.leftSettingsMenu.SetUpColormapSettings(Global.App.Colormap);
            }
        }
        private void OnLightChange(string changed) {
            Global.App.Window.canvas.DrawMassRedo();
            if(changed == nameof(Settings.DEFBIOME)) Colormap.Biome.UpdateDef();

            if(changed == "On" || changed == nameof(Settings.DEFBIOME) || changed == "TemperatureVariation") {
                Colormap.TintManager.UpdateTexture();
            }

        }
        private void OnHardChange(List<string> changed) {
            if(changed.Count == 0) return;
            SettingsHub.Freeze();

            if(changed.Contains("RENDERMODE")) {
                Window.SetCanvas(Global.Settings.RENDERMODE);
            }

            Global.App.Window.canvas.DrawMassRedo();
            _openedSave?.Reset();
            SetWorld(changed.Contains(nameof(Settings.DIMENSION)));

            if(changed.Contains(nameof(Settings.COLOR_MAPPING_MODE))) {
                SetColormap();
            } else if(changed.Contains(nameof(Settings.SKIP_UNKNOWN_BLOCKS))) {
                Colormap?.Block.SetDef(Settings.SKIP_UNKNOWN_BLOCKS ? Colormap.INVBLOCK : Colormap.NONEBLOCK);
            }

            Colormap?.UpdateHeightmapCompatability();
            Colormap?.TintManager.Freeze();
            Colormap?.FilterManager.Freeze();



            if(changed.Contains("ABSORBTION")) {
                Colormap?.BlocksManager.UpdateTexture();
            }



            SettingsHub.FinishFreeze(false);
        }



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

                SettingsHub = new SettingsHub(OnAutoChange, OnLightChange, OnHardChange);
                SettingsHub.RegisterSettings(Settings);
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

        public GenDataTileMap TileMap;
        public SettingsHub SettingsHub { get; set; }
        public Settings Settings { get; set; }

        private Colormap colormap;
        public Colormap Colormap {
            get => colormap;
            set {
                colormap?.Dispose();
                colormap = value;
            }
        }

        private Save _openedSave;
        public Save OpenedSave { // hard reset
            get {
                return _openedSave;
            }

            set {
                SettingsHub.Freeze();

                var oldworld = _openedSave;
                _openedSave = value;
                if(value != null) {
                    SetWorld(true);


                    if(_openedSave.levelDatInfo.mods.Length > 0) {
                        Settings.ColorMapping = "default";
                    }
                    if(Path.Exists(Settings.ColormapToPath(Settings.COLOR_MAPPING_MODE)) == false) {
                        Settings.ColorMapping = "default";
                    }
                    if(Colormap == null || Settings.ColormapToPath(Settings.COLOR_MAPPING_MODE) != Settings.ColormapToPath(Settings.ColorMapping) || !oldworld.datapackInfo.SameAs(value.datapackInfo)) {
                        Settings.COLOR_MAPPING_MODE = Settings.ColorMapping;
                        SetColormap();
                    }

                    Colormap?.UpdateHeightmapCompatability();
                }

                SettingsHub.FinishFreeze(false);
            }
        }




        void SetColormap() {
            if(OpenedSave == null) return;


            if(Colormap != null) {
                SettingsHub.UnlistSettings(Colormap.TintManager);
                foreach(var tint in Colormap.TintManager.ELEMENTS) {
                    SettingsHub.UnlistSettings(tint);
                }

                SettingsHub.UnlistSettings(Colormap.FilterManager);
                foreach(var filter in Colormap.FilterManager.ELEMENTS) {
                    SettingsHub.UnlistSettings(filter);
                }
            }

            Colormap = new Colormap(RawColormap.Load(Settings.ColormapToPath(Settings.COLOR_MAPPING_MODE)), OpenedSave.levelDatInfo.version_id, OpenedSave.datapackInfo);

            SettingsHub.RegisterSettings(Colormap.TintManager);
            foreach(var tint in Colormap.TintManager.ELEMENTS) {
                SettingsHub.RegisterSettings(tint);
            }

            SettingsHub.RegisterSettings(Colormap.FilterManager);
            foreach(var filter in Colormap.FilterManager.ELEMENTS) {
                SettingsHub.RegisterSettings(filter);
            }


            foreach(var tint in Colormap.TintManager.ELEMENTS) tint.SetFromBack();
            foreach(var filter in Colormap.FilterManager.ELEMENTS) filter.SetFromBack();
            Colormap.FilterManager.SetFromBack();
            Colormap.TintManager.SetFromBack();
            Colormap.BlocksManager.UpdateTexture();

            Window.OnColormapChange();
        }
        void SetWorld(bool dimchange) {
            if(_openedSave == null) return;
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

            Window.OnHardReset();
            Window.canvas.OnTilemapChange(TileMap, dimchange);

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
