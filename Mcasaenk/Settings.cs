using Mcasaenk.Rendering;
using Mcasaenk.Shade3d;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
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

    public enum ColorMappingMode {
        [Description("mean")]
        Mean,
        [Description("java map")]
        Map,
    }

    public enum WaterMode {
        [Description("translucient")]
        Exponential,
        [Description("java map")]
        Map,
    }


    public enum FilterMode { None, Air, Water, LightAir, LightWater, Shade3d, HeightmapAir, HeightmapWater, REGEX }


    public class Settings : INotifyPropertyChanged {
        public void SetFromBack() {
            frozen = true;

            ADEG = adeg_back;
            BDEG = bdeg_back;
            SHADE3D = shade3d_back;
            MAXCONCURRENCY = regionConcurrency_back;
            CHUNKRENDERMAXCONCURRENCY = chunkConcurrency_back;
            COLOR_MAPPING_MODE = colorMapping_back;

            frozen = false;
            OnHardChange("");
        }
        public void Reset() {
            ADeg = ADEG;
            BDeg = BDEG;
            Shade3d = SHADE3D;
            RegionConcurrency = MAXCONCURRENCY;
            ChunkConcurrency = CHUNKRENDERMAXCONCURRENCY;
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
            MAXZOOM = 5, MINZOOM = -5,
            CHUNKGRID = ChunkGridType.None, REGIONGRID = RegionGridType.None, Background = BackgroundType.None,
            MAXCONCURRENCY = 8, CHUNKRENDERMAXCONCURRENCY = 16,
            FOOTER = true, OVERLAYS = true, UNLOADED = true,

            COLOR_MAPPING_MODE = ColorMappingMode.Mean,
            WATER_MODE = WaterMode.Exponential,
            WATEROPACITY = 0.50,

            LANDBIOMES = false, WATERBIOMES = true,
            LAND_BLEND = 17, WATER_BLEND = 17,

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








        private int landBlend;
        public int LandBlend {
            get => landBlend;
            set {
                if(landBlend == value) return;

                landBlend = value;
                OnLightChange(nameof(LandBlend));
            }
        }
        public int LAND_BLEND { get => LandBlend; set => LandBlend = value; }


        private int waterBlend;
        public int WaterBlend {
            get => waterBlend;
            set {
                if(waterBlend == value) return;

                waterBlend = value;
                OnLightChange(nameof(WaterBlend));
            }
        }
        public int WATER_BLEND { get => WaterBlend; set => WaterBlend = value; }


        private double contrast;
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
        public double SunLight {
            get => sunlight;
            set {
                if(sunlight == value) return;

                sunlight = value;
                OnLightChange(nameof(SunLight));
            }
        }
        public double SUN_LIGHT { get => SunLight; set => SunLight = value; }


        private double waterOpacity;
        public double WaterOpacity {
            get => waterOpacity;
            set {
                if(waterOpacity == value) return;

                waterOpacity = value;
                OnLightChange(nameof(WaterOpacity));
            }
        }
        public double WATEROPACITY { get => WaterOpacity; set => WaterOpacity = value; }


        private bool landBiomes;
        public bool LandBiomes {
            get => landBiomes;
            set {
                if(landBiomes == value) return;

                landBiomes = value;
                OnLightChange(nameof(LandBiomes));
            }
        }
        public bool LANDBIOMES { get => LandBiomes; set => LandBiomes = value; }


        private bool waterBiomes;
        public bool WaterBiomes {
            get => waterBiomes;
            set {
                if(waterBiomes == value) return;

                waterBiomes = value;
                OnLightChange(nameof(WaterBiomes));
            }
        }
        public bool WATERBIOMES { get => WaterBiomes; set => WaterBiomes = value; }


        private bool staticShade;
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
        public bool Shade3d {
            get => shade3d_back;
            set {
                if(shade3d_back == value) return;

                shade3d_back = value;
                OnAutoChange(nameof(Shade3d));
            }
        }
        public bool SHADE3D { get => shade3d; set { shade3d = value; Shade3d = value; OnHardChange(nameof(SHADE3D)); } }


        private double adeg, adeg_back;
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
            }
        }
        public double ADEG { get => adeg; set { adeg = value; ADeg = value; OnHardChange(nameof(ADEG)); } }


        private double bdeg, bdeg_back;
        public double BDeg {
            get => bdeg_back;
            set {
                if(bdeg_back == value) return;

                bdeg_back = value;
                OnAutoChange(nameof(BDeg));
            }
        }
        public double BDEG { get => bdeg; set { bdeg = value; BDeg = value; OnHardChange(nameof(BDEG)); } }


        private ColorMappingMode colorMapping, colorMapping_back;
        public ColorMappingMode ColorMapping {
            get => colorMapping_back;
            set {
                if(colorMapping_back == value) return;

                colorMapping_back = value;
                OnAutoChange(nameof(ColorMapping));
            }
        }
        public ColorMappingMode COLOR_MAPPING_MODE { get => colorMapping; set { colorMapping = value; ColorMapping = value; OnHardChange(nameof(COLOR_MAPPING_MODE)); } }


        private WaterMode waterMode;
        public WaterMode WaterMode {
            get => waterMode;
            set {
                if(waterMode == value) return;

                waterMode = value;
                OnLightChange(nameof(WaterMode));
            }
        }
        public WaterMode WATER_MODE { get => WaterMode; set => WaterMode = value; }




        private bool footer;
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
        public int RegionConcurrency {
            get => regionConcurrency_back;
            set {
                if(regionConcurrency_back == value) return;

                regionConcurrency_back = value;
                OnAutoChange(nameof(RegionConcurrency));
            }
        }
        public int MAXCONCURRENCY { get => regionConcurrency; set { regionConcurrency = value; RegionConcurrency = value; OnHardChange(nameof(MAXCONCURRENCY)); } }


        private int chunkConcurrency,chunkConcurrency_back;
        public int ChunkConcurrency {
            get => chunkConcurrency_back;
            set {
                if(chunkConcurrency_back == value) return;

                chunkConcurrency_back = value;
                OnAutoChange(nameof(ChunkConcurrency));
            }
        }
        public int CHUNKRENDERMAXCONCURRENCY { get => chunkConcurrency; set { chunkConcurrency = value; ChunkConcurrency = value; OnHardChange(nameof(CHUNKRENDERMAXCONCURRENCY)); } }


        private int minZoom;
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
        public int MaxZoom {
            get => maxZoom;
            set {
                if(maxZoom == value) return;

                maxZoom = value;
                OnAutoChange(nameof(MaxZoom));
            }
        }
        public int MAXZOOM { get => MaxZoom; set => MaxZoom = value; }







        #region depr
        public bool WATERDEPTH { get => true; set { } }
        #endregion


        #region static
        public static FilterMode _AIR_FILTER = FilterMode.HeightmapAir;
        public static FilterMode _WATER_FILTER = FilterMode.HeightmapWater;
        public static FilterMode _SHADE3D_FILTER = FilterMode.Shade3d; // muss be >= airfilter & waterfilter
        public static Filter.filter AIR_FILTER { get => FromEnum.Filter(_AIR_FILTER); }
        public static Filter.filter WATER_FILTER { get => FromEnum.Filter(_WATER_FILTER); }
        public static Filter.filter SHADE3D_FILTER { get => FromEnum.Filter(_SHADE3D_FILTER); }
        #endregion



        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }












    public static class FromEnum {
        public static Filter.filter Filter(FilterMode filter) {
            return filter switch {
                FilterMode.None => Rendering.Filter.NullFilter,
                FilterMode.LightAir => AirFilter.Def,
                FilterMode.LightWater => WaterFilter.Def,
                FilterMode.Air => AirFilter.List,
                FilterMode.Water => WaterFilter.List,
                FilterMode.Shade3d => Shade3DFilter.List,
                FilterMode.HeightmapAir => HeightmapFilter.FilterAir,
                FilterMode.HeightmapWater => HeightmapFilter.FilterWater,
            };
        }

        public static IColorMapping Mapping(ColorMappingMode mapping) {
            return mapping switch {
                ColorMappingMode.Map => new MapColorMapping(),
                ColorMappingMode.Mean => new MeanColorMapping(),
            };
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
