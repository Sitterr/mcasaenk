using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json.Serialization;
using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using Mcasaenk.UI;

namespace Mcasaenk {
    public enum Direction {
        [Description("north")]
        North = 0,
        [Description("south")]
        South = 1,
        [Description("east")]
        East = 2,
        [Description("west")]
        West = 3,
    }
    public enum ChunkGridType {
        [Description("none")]
        None = 0,
        [Description("straight")]
        Straight = 1,
    }
    public enum RegionGridType {
        [Description("none")]
        None = 0,
        [Description("straight")]
        Straight = 1,
        [Description("dashed")]
        dashed = 2,
    }
    public enum BackgroundType {
        [Description("monotone")]
        None = 0,
        [Description("checker")]
        Checker = 1,
    }
    public enum MapGridType {
        [Description("none")]
        None = 0,
        [Description("1:1")]
        zoom0 = 1,
        [Description("1:2")]
        zoom1 = 2,
        [Description("1:4")]
        zoom2 = 3,
        [Description("1:8")]
        zoom3 = 4,
    }

    public enum ColorApproximationAlgorithm {
        [Description("rgb")]
        RGB_Euclidean = 0,
        [Description("lab")]
        LAB_Euclidean = 1,
        [Description("cie94")]
        LAB_CIE94 = 2,
    }

    public enum ShadeType {
        [Description("standard")]
        OG = 0,
        [Description("in-game map")]
        jmap = 1,
    }

    public enum JsmapWaterMode {
        [Description("vanilla")]
        vanilla = 0,
        [Description("translucient")]
        translucient = 1,
    }

    public enum RenderMode {
        [Description("legacy")]
        LEGACY = 0,
        [Description("opengl")]
        OPENGL = 1,
    }


    public enum FilterMode { None, Air, Depth, LightAir, LightWater, Shade3d, HeightmapAir, HeightmapWater, REGEX }

    public class SettingsHub(Action<string> onAutoChange, Action<string> onLightChange, Action<List<string>> onHardChange) : INotifyPropertyChanged {

        private readonly List<StandardizedSettings> settings = new List<StandardizedSettings>();
        public void RegisterSettings(StandardizedSettings settings) {
            if(settings == null) return;
            this.settings.Add(settings);
            settings.SettingsHub = this;
        }
        public void UnlistSettings(StandardizedSettings settings) {
            this.settings.Remove(settings);
            settings.SettingsHub = null;
        }


        List<string> frozenChanges = new List<string>();
        public bool frozen { get; private set; } = true;
        public void Freeze() {
            frozen = true;
            frozenChanges.Clear();
        }
        public void FinishFreeze(bool execute) {
            frozen = false;
            if(execute) onHardChange(frozenChanges.ToList());
        }

        public void SetFromBack() {
            Freeze();

            foreach(var sett in settings) sett.SetFromBack();

            FinishFreeze(true);
        }
        public void Reset() {
            foreach(var sett in settings) sett.Reset();
        }


        public void OnAutoChange(string propertyName) {
            OnPropertyChanged(nameof(CHANGED_BACK));
            onAutoChange(propertyName);
        }
        public void OnLightChange(string propertyName) {
            if(frozen == false) onLightChange(propertyName);
        }
        public void OnHardChange(string propertyName) {
            OnPropertyChanged(nameof(CHANGED_BACK));
            if(frozen) frozenChanges.Add(propertyName);
            else onHardChange([propertyName]);
        }





        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public bool CHANGED_BACK {
            get => settings.Any(s => s.ChangedBack());
        }
    }

    public abstract class StandardizedSettings : INotifyPropertyChanged {
        [JsonIgnore]
        public SettingsHub SettingsHub { get; set; }


        public abstract void SetFromBack();
        public abstract void Reset();
        public abstract bool ChangedBack();

        public void OnAutoChange(string propertyName) {
            if(SettingsHub == null) return;

            OnPropertyChanged(propertyName);
            SettingsHub.OnAutoChange(propertyName);
        }
        public void OnLightChange(string propertyName) {
            if(SettingsHub == null) return;

            OnPropertyChanged(propertyName);
            SettingsHub.OnLightChange(propertyName);
        }
        public void OnHardChange(string propertyName) {
            if(SettingsHub == null) return;

            OnPropertyChanged(propertyName);
            SettingsHub.OnHardChange(propertyName);
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Settings : StandardizedSettings {
        public override void SetFromBack() {
            if(Y_OFFICIAL != Y) Y_OFFICIAL = Y;
            if(ADEG != ADeg) ADEG = ADeg;
            if(BDEG != BDeg) BDEG = BDeg;
            if(SHADE3D != Shade3d) SHADE3D = Shade3d;
            if(MAXCONCURRENCY != RegionConcurrency) MAXCONCURRENCY = RegionConcurrency;
            if(CHUNKRENDERMAXCONCURRENCY != ChunkConcurrency) CHUNKRENDERMAXCONCURRENCY = ChunkConcurrency;
            if(DRAWMAXCONCURRENCY != DrawConcurrency) DRAWMAXCONCURRENCY = DrawConcurrency;
            if(TRANSPARENTLAYERS != TransparentLayers) TRANSPARENTLAYERS = TransparentLayers;
            if(COLOR_MAPPING_MODE != ColorMapping) COLOR_MAPPING_MODE = ColorMapping;
            if(SHADETYPE != ShadeType) SHADETYPE = ShadeType;
            if(PREFERHEIGHTMAPS != PreferHeightmap) PREFERHEIGHTMAPS = PreferHeightmap;
            if(SKIP_UNKNOWN_BLOCKS != SkipUnknown) SKIP_UNKNOWN_BLOCKS = SkipUnknown;
            if(BLOCKINFO != BlockInfo) BLOCKINFO = BlockInfo;
            if(RENDERMODE != RenderMode) RENDERMODE = RenderMode;
        }
        public override void Reset() {
            Y = Y_OFFICIAL;
            ADeg = ADEG;
            BDeg = BDEG;
            Shade3d = SHADE3D;
            RegionConcurrency = MAXCONCURRENCY;
            ChunkConcurrency = CHUNKRENDERMAXCONCURRENCY;
            DrawConcurrency = DRAWMAXCONCURRENCY;
            TransparentLayers = TRANSPARENTLAYERS;
            ColorMapping = COLOR_MAPPING_MODE;
            ShadeType = SHADETYPE;
            PreferHeightmap = PREFERHEIGHTMAPS;
            SkipUnknown = SKIP_UNKNOWN_BLOCKS;
            BlockInfo = BLOCKINFO;
            RenderMode = RENDERMODE;
        }
        public override bool ChangedBack() =>
                   Y_OFFICIAL != Y ||
                   ADEG != ADeg ||
                   BDEG != BDeg ||
                   SHADE3D != Shade3d ||
                   MAXCONCURRENCY != RegionConcurrency ||
                   CHUNKRENDERMAXCONCURRENCY != ChunkConcurrency ||
                   TRANSPARENTLAYERS != TransparentLayers ||
                   DRAWMAXCONCURRENCY != DrawConcurrency ||
                   COLOR_MAPPING_MODE != ColorMapping ||
                   SHADETYPE != ShadeType ||
                   PREFERHEIGHTMAPS != PreferHeightmap ||
                   SKIP_UNKNOWN_BLOCKS != SkipUnknown ||
                   BLOCKINFO != BlockInfo ||
                   RENDERMODE != RenderMode
            ;

        public static Settings DEF() => new Settings() {
            MAXZOOM = 4, MINZOOM = -4,
            ENABLE_COLORMAP_EDITING = false,
            CHUNKGRID = ChunkGridType.None, REGIONGRID = RegionGridType.None, BACKGROUND = BackgroundType.Checker, MAPGRID = MapGridType.None,
            MAPAPPROXIMATIONALGO = ColorApproximationAlgorithm.LAB_CIE94, MAXCONCURRENCY = 8, CHUNKRENDERMAXCONCURRENCY = 16, DRAWMAXCONCURRENCY = 8, TRANSPARENTLAYERS = 2,
            RENDERMODE = RenderMode.OPENGL,
            FOOTER = true, OVERLAYS = true, UNLOADED = true,
            MCDIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "saves"),
            PREDEFINEDRES = [
                new Resolution() { Name = "Full HD", type = ResolutionType.stat, X = 1920, Y = 1080 },
                new Resolution() { Name = "WQHD", type = ResolutionType.stat, X = 2560, Y = 1440 },
                new Resolution() { Name = "4K UHD", type = ResolutionType.stat, X = 3840, Y = 2160 },
            ],
            PREFERHEIGHTMAPS = true, SKIP_UNKNOWN_BLOCKS = true, BLOCKINFO = false,

            COLOR_MAPPING_MODE = "default",
            SHADETYPE = ShadeType.OG,

            SUN_LIGHT = 15, BLOCK_LIGHT = 0,

            CONTRAST = 0.50,

            SHADE3D = true, STATIC_SHADE = true, NOSHADE_SHADE3D = false,

            WATER_TRANSPARENCY = 0.50, WATER_SMART_SHADE = true,
            OCEAN_DEPTH_BLENDING = 1,
            Jmap_WATER_MODE = JsmapWaterMode.vanilla, Jmap_REVEALED_WATER = 1, Jmap_MAP_DIRECTION = Direction.North,

            ADEG = 120, BDEG = 15,
        };

        private string dimension;
        [JsonIgnore]
        public string DIMENSION {
            get => dimension;
            set {
                if(dimension != value) {
                    dimension = value;
                    OnHardChange(nameof(DIMENSION));
                }
            }
        }

        private short y, y_back;
        [JsonIgnore]
        public short Y {
            get => y_back;
            set {
                value = Math.Clamp(value, MINY, MAXY);
                if(y_back == value) return;

                y_back = value;
                OnAutoChange(nameof(Y));
                if(Global.App.OpenedSave == null) {
                    y = value;
                    OnAutoChange(nameof(Y_OFFICIAL));
                    OnAutoChange(nameof(ABSY));
                }
            }
        }
        [JsonIgnore]
        public short Y_OFFICIAL {
            get => y; set {
                value = Math.Clamp(value, MINY, MAXY);
                y = value; Y = value;
                OnHardChange(nameof(Y_OFFICIAL)); OnAutoChange(nameof(ABSY));
            }
        }
        [JsonIgnore]
        public short MINY { get; set; } = -64;
        [JsonIgnore]
        public short MAXABSHEIGHT { get; set; } = 384;
        [JsonIgnore]
        public short ABSY { get => (short)(Y_OFFICIAL - MINY); }
        [JsonIgnore]
        public short MAXY { get => (short)(MAXABSHEIGHT + MINY - 1); }


        private double contrast;
        [JsonIgnore]
        public double Contrast {
            get => contrast;
            set {
                if(contrast == value) return;

                contrast = value;
                this.OnLightChange(nameof(Contrast));
            }
        }
        public double CONTRAST { get => Contrast; set => Contrast = value; }


        private double waterTransparency;
        [JsonIgnore]
        public double WaterTransparency {
            get => waterTransparency;
            set {
                if(waterTransparency == value) return;

                waterTransparency = value;
                this.OnLightChange(nameof(WaterTransparency));
            }
        }
        public double WATER_TRANSPARENCY { get => WaterTransparency; set => WaterTransparency = value; }


        private int oceanBlending;
        [JsonIgnore]
        public int OceanDepthBlending {
            get => oceanBlending;
            set {
                if(oceanBlending == value) return;

                oceanBlending = value;
                this.OnLightChange(nameof(OceanDepthBlending));
            }
        }
        public int OCEAN_DEPTH_BLENDING { get => OceanDepthBlending; set => OceanDepthBlending = value; }


        private double jmaprevealedWater;
        [JsonIgnore]
        public double Jmap_RevealedWater {
            get => jmaprevealedWater;
            set {
                if(jmaprevealedWater == value) return;

                jmaprevealedWater = value;
                this.OnLightChange(nameof(Jmap_RevealedWater));
            }
        }
        public double Jmap_REVEALED_WATER { get => Jmap_RevealedWater; set => Jmap_RevealedWater = value; }


        private JsmapWaterMode jmapwatermode;
        [JsonIgnore]
        public JsmapWaterMode Jmap_WaterMode {
            get => jmapwatermode;
            set {
                if(jmapwatermode == value) return;

                jmapwatermode = value;
                this.OnLightChange(nameof(Jmap_WaterMode));
            }
        }
        public JsmapWaterMode Jmap_WATER_MODE { get => Jmap_WaterMode; set => Jmap_WaterMode = value; }


        private Direction jmapmapdirection;
        [JsonIgnore]
        public Direction Jmap_MapDirection {
            get => jmapmapdirection;
            set {
                if(jmapmapdirection == value) return;

                jmapmapdirection = value;
                this.OnLightChange(nameof(Jmap_MapDirection));
            }
        }
        public Direction Jmap_MAP_DIRECTION { get => Jmap_MapDirection; set => Jmap_MapDirection = value; }


        private int sunlight;
        [JsonIgnore]
        public int SunLight {
            get => sunlight;
            set {
                if(sunlight == value) return;

                sunlight = value;
                OnLightChange(nameof(SunLight));
            }
        }
        public int SUN_LIGHT { get => SunLight; set => SunLight = value; }


        private int blocklight;
        [JsonIgnore]
        public int BlockLight {
            get => blocklight;
            set {
                if(blocklight == value) return;

                blocklight = value;
                OnLightChange(nameof(BlockLight));
            }
        }
        public int BLOCK_LIGHT { get => BlockLight; set => BlockLight = value; }


        private ShadeType shadetype, shadetype_back;
        [JsonIgnore]
        public ShadeType ShadeType {
            get => shadetype_back;
            set {
                if(shadetype_back == value) return;

                shadetype_back = value;
                OnAutoChange(nameof(ShadeType));
                if(Global.App.OpenedSave == null) {
                    shadetype = value;
                    OnAutoChange(nameof(SHADETYPE));
                }
            }
        }
        public ShadeType SHADETYPE { get => shadetype; set { shadetype = value; ShadeType = value; OnHardChange(nameof(SHADETYPE)); } }


        private bool staticShade;
        [JsonIgnore]
        public bool StaticShade {
            get => staticShade;
            set {
                if(staticShade == value) return;

                staticShade = value;
                OnLightChange(nameof(StaticShade));
            }
        }
        public bool STATIC_SHADE { get => StaticShade; set => StaticShade = value; }


        private bool shade3d, shade3d_back;
        [JsonIgnore]
        public bool Shade3d {
            get => shade3d_back;
            set {
                if(shade3d_back == value) return;

                shade3d_back = value;
                OnAutoChange(nameof(Shade3d));
                if(Global.App.OpenedSave == null) {
                    shade3d = value;
                    OnAutoChange(nameof(SHADE3D));
                }
            }
        }
        public bool SHADE3D { get => shade3d; set { shade3d = value; Shade3d = value; OnHardChange(nameof(SHADE3D)); } }


        private double adeg, adeg_back;
        [JsonIgnore]
        public double ADeg {
            get => adeg_back;
            set {
                if(adeg_back == value) return;

                adeg_back = value;

                if(SHADE3D == false) {
                    adeg = value;
                    ShadeConstants.GLB = new ShadeConstants(ADEG);
                    OnAutoChange(nameof(ADeg));
                    OnLightChange(nameof(ADEG));
                } else {
                    OnAutoChange(nameof(ADeg));
                }

                if(Global.App.OpenedSave == null) {
                    adeg = value;
                    OnAutoChange(nameof(ADEG));
                }
            }
        }
        public double ADEG { get => adeg; set { adeg = value; ADeg = value; OnHardChange(nameof(ADEG)); } }


        private double bdeg, bdeg_back;
        [JsonIgnore]
        public double BDeg {
            get => bdeg_back;
            set {
                if(bdeg_back == value) return;

                bdeg_back = value;
                OnAutoChange(nameof(BDeg));
                if(Global.App.OpenedSave == null) {
                    bdeg = value;
                    OnAutoChange(nameof(BDEG));
                }
            }
        }
        public double BDEG { get => bdeg; set { bdeg = value; BDeg = value; OnHardChange(nameof(BDEG)); } }


        private bool waterSmartShade;
        [JsonIgnore]
        public bool WaterSmartShade {
            get => waterSmartShade;
            set {
                if(waterSmartShade == value) return;

                waterSmartShade = value;
                OnLightChange(nameof(WaterSmartShade));
            }
        }
        public bool WATER_SMART_SHADE { get => WaterSmartShade; set => WaterSmartShade = value; }



        private string colorMapping, colorMapping_back;
        [JsonIgnore]
        public string ColorMapping {
            get => colorMapping_back;
            set {
                if(colorMapping_back == value) return;

                colorMapping_back = value;
                OnAutoChange(nameof(ColorMapping));
                if(Global.App.OpenedSave == null) {
                    colorMapping = value;
                    OnAutoChange(nameof(COLOR_MAPPING_MODE));
                }
            }
        }
        public string COLOR_MAPPING_MODE { get => colorMapping; set { colorMapping = value; ColorMapping = value; OnHardChange(nameof(COLOR_MAPPING_MODE)); } }

        public static string ColormapToPath(string colormapping) {
            string path;
            if(Path.Exists(Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps", colormapping))) path = Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps", colormapping);
            else if(Path.Exists(Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps", colormapping + ".zip"))) path = Path.Combine(Global.App.APPFOLDER, Global.App.ID, "colormaps", colormapping + ".zip");
            else if(Path.Exists(Path.Combine(Global.App.APPFOLDER, "colormaps", colormapping))) path = Path.Combine(Global.App.APPFOLDER, "colormaps", colormapping);
            else if(Path.Exists(Path.Combine(Global.App.APPFOLDER, "colormaps", colormapping + ".zip"))) path = Path.Combine(Global.App.APPFOLDER, "colormaps", colormapping + ".zip");
            else path = "default";
            return path;
        }



        private bool footer;
        [JsonIgnore]
        public bool Footer {
            get => footer;
            set {
                if(footer == value) return;

                footer = value;
                OnAutoChange(nameof(Footer));
            }
        }
        public bool FOOTER { get => Footer; set => Footer = value; }


        private bool overlays;
        [JsonIgnore]
        public bool Overlays {
            get => overlays;
            set {
                if(overlays == value) return;

                overlays = value;
                OnAutoChange(nameof(Overlays));
            }
        }
        public bool OVERLAYS { get => Overlays; set => Overlays = value; }


        private bool unloaded;
        [JsonIgnore]
        public bool Unloaded {
            get => unloaded;
            set {
                if(unloaded == value) return;

                unloaded = value;
                OnAutoChange(nameof(Unloaded));
            }
        }
        public bool UNLOADED { get => Unloaded; set => Unloaded = value; }


        private ChunkGridType chunkgrid;
        [JsonIgnore]
        public ChunkGridType ChunkGrid {
            get => chunkgrid;
            set {
                if(chunkgrid == value) return;

                chunkgrid = value;
                OnAutoChange(nameof(ChunkGrid));
            }
        }
        public ChunkGridType CHUNKGRID { get => ChunkGrid; set => ChunkGrid = value; }


        private RegionGridType regiongrid;
        [JsonIgnore]
        public RegionGridType RegionGrid {
            get => regiongrid;
            set {
                if(regiongrid == value) return;

                regiongrid = value;
                OnAutoChange(nameof(RegionGrid));
            }
        }
        public RegionGridType REGIONGRID { get => RegionGrid; set => RegionGrid = value; }


        private BackgroundType background;
        [JsonIgnore]
        public BackgroundType Background {
            get => background;
            set {
                if(background == value) return;

                background = value;
                OnAutoChange(nameof(Background));
            }
        }
        public BackgroundType BACKGROUND { get => Background; set => Background = value; }


        private MapGridType mapgrid;
        [JsonIgnore]
        public MapGridType MapGrid {
            get => mapgrid;
            set {
                if(mapgrid == value) return;

                mapgrid = value;
                OnAutoChange(nameof(MapGrid));
            }
        }
        public MapGridType MAPGRID { get => MapGrid; set => MapGrid = value; }

        private ColorApproximationAlgorithm mappalettemethod;
        [JsonIgnore]
        public ColorApproximationAlgorithm MapApproximationAlgo {
            get => mappalettemethod;
            set {
                if(mappalettemethod == value) return;

                mappalettemethod = value;
                OnLightChange(nameof(MapApproximationAlgo));
            }
        }
        public ColorApproximationAlgorithm MAPAPPROXIMATIONALGO { get => MapApproximationAlgo; set => MapApproximationAlgo = value; }

        private int regionConcurrency, regionConcurrency_back;
        [JsonIgnore]
        public int RegionConcurrency {
            get => regionConcurrency_back;
            set {
                if(regionConcurrency_back == value) return;

                regionConcurrency_back = value;
                OnAutoChange(nameof(RegionConcurrency));
                if(Global.App.OpenedSave == null) {
                    regionConcurrency = value;
                    OnAutoChange(nameof(MAXCONCURRENCY));
                }
            }
        }
        public int MAXCONCURRENCY { get => regionConcurrency; set { regionConcurrency = value; RegionConcurrency = value; OnHardChange(nameof(MAXCONCURRENCY)); } }


        private int chunkConcurrency, chunkConcurrency_back;
        [JsonIgnore]
        public int ChunkConcurrency {
            get => chunkConcurrency_back;
            set {
                if(chunkConcurrency_back == value) return;

                chunkConcurrency_back = value;
                OnAutoChange(nameof(ChunkConcurrency));
                if(Global.App.OpenedSave == null) {
                    chunkConcurrency = value;
                    OnAutoChange(nameof(CHUNKRENDERMAXCONCURRENCY));
                }
            }
        }
        public int CHUNKRENDERMAXCONCURRENCY { get => chunkConcurrency; set { chunkConcurrency = value; ChunkConcurrency = value; OnHardChange(nameof(CHUNKRENDERMAXCONCURRENCY)); } }


        private int drawConcurrency, drawConcurrency_back;
        [JsonIgnore]
        public int DrawConcurrency {
            get => drawConcurrency_back;
            set {
                if(drawConcurrency_back == value) return;

                drawConcurrency_back = value;
                OnAutoChange(nameof(DrawConcurrency));
                if(Global.App.OpenedSave == null) {
                    drawConcurrency = value;
                    OnAutoChange(nameof(DRAWMAXCONCURRENCY));
                }
            }
        }
        public int DRAWMAXCONCURRENCY { get => drawConcurrency; set { drawConcurrency = value; DrawConcurrency = value; OnHardChange(nameof(DRAWMAXCONCURRENCY)); } }


        private int transparentLayers, transparentLayers_back;
        [JsonIgnore]
        public int TransparentLayers {
            get => transparentLayers_back;
            set {
                if(transparentLayers_back == value) return;

                transparentLayers_back = value;
                OnAutoChange(nameof(TransparentLayers));
                if(Global.App.OpenedSave == null) {
                    transparentLayers = value;
                    OnAutoChange(nameof(TRANSPARENTLAYERS));
                }
            }
        }
        public int TRANSPARENTLAYERS { get => transparentLayers; set { transparentLayers = value; TransparentLayers = value; OnHardChange(nameof(TRANSPARENTLAYERS)); if(TransparentLayers == 0) this.WATER_TRANSPARENCY = 0; } }


        private int minZoom;
        [JsonIgnore]
        public int MinZoom {
            get => minZoom;
            set {
                if(minZoom == value) return;

                minZoom = value;
                OnAutoChange(nameof(MinZoom));
            }
        }
        public int MINZOOM { get => MinZoom; set => MinZoom = value; }


        private int maxZoom;
        [JsonIgnore]
        public int MaxZoom {
            get => maxZoom;
            set {
                if(maxZoom == value) return;

                maxZoom = value;
                OnAutoChange(nameof(MaxZoom));
            }
        }
        public int MAXZOOM { get => MaxZoom; set => MaxZoom = value; }


        private string mcDir;
        [JsonIgnore]
        public string McDir {
            get => mcDir;
            set {
                if(mcDir == value) return;

                mcDir = value;
                OnAutoChange(nameof(McDir));
            }
        }
        public string MCDIR { get => McDir; set => McDir = value; }


        private bool preferheightmap, preferheightmap_back;
        [JsonIgnore]
        public bool PreferHeightmap {
            get => preferheightmap_back;
            set {
                if(preferheightmap_back == value) return;

                preferheightmap_back = value;
                OnAutoChange(nameof(PreferHeightmap));
                if(Global.App.OpenedSave == null) {
                    preferheightmap = value;
                    OnAutoChange(nameof(PREFERHEIGHTMAPS));
                }
            }
        }
        public bool PREFERHEIGHTMAPS { get => preferheightmap; set { preferheightmap = value; PreferHeightmap = value; OnHardChange(nameof(PREFERHEIGHTMAPS)); } }


        private bool skipunknown, skipunknown_back;
        [JsonIgnore]
        public bool SkipUnknown {
            get => skipunknown_back;
            set {
                if(skipunknown_back == value) return;

                skipunknown_back = value;
                OnAutoChange(nameof(SkipUnknown));
                if(Global.App.OpenedSave == null) {
                    skipunknown = value;
                    OnAutoChange(nameof(SKIP_UNKNOWN_BLOCKS));
                }
            }
        }
        public bool SKIP_UNKNOWN_BLOCKS { get => skipunknown; set { skipunknown = value; SkipUnknown = value; OnHardChange(nameof(SKIP_UNKNOWN_BLOCKS)); } }



        private bool blockinfo, blockinfo_back;
        [JsonIgnore]
        public bool BlockInfo {
            get => blockinfo_back;
            set {
                if(blockinfo_back == value) return;

                blockinfo_back = value;

                if(Global.App.OpenedSave == null || RENDERMODE == RenderMode.LEGACY) {
                    blockinfo = value;
                    OnAutoChange(nameof(BlockInfo));
                    OnAutoChange(nameof(BLOCKINFO));
                } else {
                    OnAutoChange(nameof(BlockInfo));
                }
            }
        }
        public bool BLOCKINFO { get => blockinfo; set { blockinfo = value; BlockInfo = value; OnHardChange(nameof(BLOCKINFO)); } }


        private bool enablecmediting;
        public bool ENABLE_COLORMAP_EDITING {
            get => enablecmediting;
            set {
                if(enablecmediting == value) return;

                enablecmediting = value;
                OnAutoChange(nameof(ENABLE_COLORMAP_EDITING));
            }
        }


        private Resolution[] predefined_reses;
        [JsonIgnore]
        public Resolution[] PredifinedReses {
            get => predefined_reses;
            set {
                if(predefined_reses == value) return;

                predefined_reses = value;
                OnAutoChange(nameof(PredifinedReses));
                if(this == Global.App.Settings) Global.App.Window?.rad.PreDefined(value);
            }
        }
        public Resolution[] PREDEFINEDRES { get => PredifinedReses; set => PredifinedReses = value; }



        private ushort defbiome;
        [JsonIgnore]
        public ushort DEFBIOME {
            get => defbiome;
            set {
                if(defbiome == value) return;
                defbiome = value;
                if(Global.App.Colormap != null) {
                    if(Global.App.Colormap.TintManager.ELEMENTS.Any(t => t is DynamicTint dtint && dtint.On == false)) {
                        OnLightChange(nameof(DEFBIOME));
                    } else {
                        OnAutoChange(nameof(DEFBIOME));
                    }
                }
            }
        }

        private bool noshade_shade3d, noshade_shade3d_back;
        [JsonIgnore]
        public bool NoShade_Shade3d {
            get => noshade_shade3d_back;
            set {
                if(noshade_shade3d_back == value) return;

                noshade_shade3d_back = value;
                OnAutoChange(nameof(NoShade_Shade3d));
                if(Global.App.OpenedSave == null) {
                    noshade_shade3d = value;
                    OnAutoChange(nameof(NOSHADE_SHADE3D));
                }
            }
        }
        public bool NOSHADE_SHADE3D { get => noshade_shade3d; set { noshade_shade3d = value; NoShade_Shade3d = value; OnHardChange(nameof(NOSHADE_SHADE3D)); } }




        private RenderMode renderMode, renderMode_back;
        [JsonIgnore]
        public RenderMode RenderMode {
            get => renderMode_back;
            set {
                if(renderMode_back == value) return;

                renderMode_back = value;
                OnAutoChange(nameof(RenderMode));
                if(Global.App.OpenedSave == null && false) {
                    renderMode = value;
                    OnAutoChange(nameof(RENDERMODE));
                }
            }
        }
        public RenderMode RENDERMODE { get => renderMode; set { renderMode = value; RenderMode = value; OnHardChange(nameof(RENDERMODE)); } }




        private bool usemapPalette;
        [JsonIgnore]
        public bool _UseMapPalette {
            get => usemapPalette;
            set {
                if(usemapPalette == value) return;

                usemapPalette = value;
                this.OnLightChange(nameof(_UseMapPalette));
            }
        }




    }












    public static class EnumHelper {
        public static string Description(this Enum value) {
            var attributes = value.GetType().GetField(value.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false);
            if(attributes.Any())
                return (attributes.First() as DescriptionAttribute).Description;

            // If no description is found, the least we can do is replace underscores with spaces
            // You can add your own custom default formatting logic here
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            return ti.ToTitleCase(ti.ToLower(value.ToString().Replace("_", " ")));
        }

        public static IEnumerable<ValueDescription> GetAllValuesAndDescriptions(Type t) {
            if(!t.IsEnum)
                throw new ArgumentException($"{nameof(t)} must be an enum type");

            return Enum.GetValues(t).Cast<Enum>().Select((e) => new ValueDescription() { Value = e, Description = e.Description() }).ToList();
        }
    }
    public class ValueDescription {
        public object Value { get; set; }
        public object Description { get; set; }
    }
}
