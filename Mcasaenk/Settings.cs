using Mcasaenk.Rendering;
using Mcasaenk.Shade3d;
using Mcasaenk.UI;
using Mcasaenk.UI.Canvas;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace Mcasaenk
{
    public enum ChunkGridType {
        [Description("none")]
        None,
        [Description("straight")]
        Straight,
    }
    public enum RegionGridType {
        [Description("none")]
        None,
        [Description("straight")]
        Straight,
        [Description("dashed")]
        dashed,
    }
    public enum BackgroundType {
        [Description("monotone")]
        None,
    }


    public enum FilterMode { None, Air, Depth, LightAir, LightWater, Shade3d, HeightmapAir, HeightmapWater, REGEX }


    public class Settings : INotifyPropertyChanged {
        public void SetFromBack() {
            frozen = true;

            RENDERHEIGHT = Y;
            ADEG = ADeg;
            BDEG = BDeg;
            SHADE3D = Shade3d;
            MAXCONCURRENCY = RegionConcurrency;
            CHUNKRENDERMAXCONCURRENCY = ChunkConcurrency;
            DRAWMAXCONCURRENCY = DrawConcurrency;
            COLOR_MAPPING_MODE = ColorMapping;

            frozen = false;
            OnHardChange("");
        }
        public void Reset() {
            Y = RENDERHEIGHT;
            ADeg = ADEG;
            BDeg = BDEG;
            Shade3d = SHADE3D;
            RegionConcurrency = MAXCONCURRENCY;
            ChunkConcurrency = CHUNKRENDERMAXCONCURRENCY;
            DrawConcurrency = DRAWMAXCONCURRENCY;
            ColorMapping = COLOR_MAPPING_MODE;
        }


        bool frozen = true;
        public void OnAutoChange(string propertyName) {
            OnPropertyChanged(propertyName);
        }

        public void OnLightChange(string propertyName) {
            if(frozen == false) onLightChange();
            if(propertyName != "") OnPropertyChanged(propertyName);
        }

        public void OnHardChange(string propertyName) {
            if(frozen == false) onHardChange();
            if(propertyName != "") OnPropertyChanged(propertyName);
        }


        public Settings() { }

        public static Settings DEF() => new Settings() {
            DIMENSION = Dimension.Type.Overworld,
            Y = 319,

            MAXZOOM = 5, MINZOOM = -5,
            CHUNKGRID = ChunkGridType.None, REGIONGRID = RegionGridType.None, Background = BackgroundType.None,
            MAXCONCURRENCY = 8, CHUNKRENDERMAXCONCURRENCY = 16, DRAWMAXCONCURRENCY = 8,
            FOOTER = true, OVERLAYS = true, UNLOADED = true,
            MCDIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "saves"),
            PREDEFINEDRES = [
                new Resolution() { Name = "Full HD", X = 1920, Y = 1080 },
                new Resolution() { Name = "WQHD", X = 2560, Y = 1440 },
                new Resolution() { Name = "4K UHD", X = 3840, Y = 2160 },
            ],

            COLOR_MAPPING_MODE = "mean",

            SUN_LIGHT = 1, CONTRAST = 0.50,

            SHADE3D = true, STATIC_SHADE = true,

            ADEG = 20, BDEG = 15,
        };

        private Action onLightChange, onHardChange;
        public void SetActions(Action onLightChange, Action onHardChange) {
            this.onLightChange = onLightChange;
            this.onHardChange = onHardChange;
            frozen = false;
        }

        private Dimension.Type dimension;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Dimension.Type DIMENSION {
            get => dimension;
            set {
                if(dimension == value) return;

                dimension = value;
                OnHardChange(nameof(DIMENSION));
            }
        }


        private short y, y_back;
        [JsonIgnore]
        public short Y {
            get => y_back;
            set {
                if(y_back == value) return;

                y_back = value;
                OnAutoChange(nameof(Y));
                if(Global.App.OpenedSave == null) {
                    y = value;
                    OnAutoChange(nameof(RENDERHEIGHT));
                }
            }
        }
        public short RENDERHEIGHT { get => y; set { y = value; Y = value; OnHardChange(nameof(RENDERHEIGHT)); } }


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


        private double sunlight;
        [JsonIgnore]
        public double SunLight {
            get => sunlight;
            set {
                if(sunlight == value) return;

                sunlight = value;
                OnLightChange(nameof(SunLight));
            }
        }
        public double SUN_LIGHT { get => SunLight; set => SunLight = value; }


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


        private int chunkConcurrency,chunkConcurrency_back;
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



        private Resolution[] predefined_reses;
        [JsonIgnore]
        public Resolution[] PredifinedReses {
            get => predefined_reses;
            set {
                if(predefined_reses == value) return;

                predefined_reses = value;
                OnAutoChange(nameof(PredifinedReses));
                Global.App.Window?.rad.PreDefined(value);
            }
        }
        public Resolution[] PREDEFINEDRES { get => PredifinedReses; set => PredifinedReses = value; }

        #region depr
        public bool WATERDEPTH { get => true; set { } }
        #endregion


        #region static
        public static Filter.filter AIR_FILTER { 
            get {
                if(Global.App.Settings.Y == 319 && Global.Settings.DIMENSION != Dimension.Type.End) return HeightmapFilter.FilterAir;
                else return AirFilter.List;
            }
        }
        public static Filter.filter DEPTH_FILTER {
            get {
                if(Global.App.Settings.Y == 319 && Global.App.Colormap.depth.block == Colormap.BLOCK_WATER && Global.Settings.DIMENSION != Dimension.Type.End) return HeightmapFilter.FilterWater;
                else return DepthFilter.List;
            }
        }
        public static Filter.filter SHADE3D_FILTER { get => Shade3DFilter.List; }
        #endregion




        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
