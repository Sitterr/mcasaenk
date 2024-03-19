using Mcasaenk.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Mcasaenk {
    public static class Settings {
        public static int MAXZOOM = 5, MINZOOM = -5;


        public static bool REGIONGRID = false, CHUNKGRID = false, ALWAYSTHINREGIONGRID = false;

#if DEBUG
        public static int MAXCONCURRENCY = 1, CHUNKRENDERMAXCONCURRENCY = 1;
#else
        public static int MAXCONCURRENCY = 8, CHUNKRENDERMAXCONCURRENCY = 16;
#endif

        public static ColorMappingMode COLOR_MAPPING_MODE = ColorMappingMode.Mean;


        public static bool WATERDEPTH = true;
        public static bool BIOMES = true, WATERBIOMES = false;

        public static double SUN_LIGHT = 0.99;

        public static double CONTRAST = 0.50;

        public static bool SHADE3D = true;

        public static bool STATIC_SHADE = true;

        public static double ADEG = 110, BDEG = 20;

        public static int BIOME_BLEND = 5;

        public static FilterMode _AIR_FILTER = FilterMode.HeightmapAir;
        public static FilterMode _WATER_FILTER = FilterMode.HeightmapWater;
        public static FilterMode _SHADE3D_FILTER = FilterMode.Shade3d; // muss be >= airfilter & waterfilter




        #region derivatives
        public static Filter.filter AIR_FILTER { get => FromEnum.Filter(_AIR_FILTER); }
        public static Filter.filter WATER_FILTER { get => FromEnum.Filter(_WATER_FILTER); }
        public static Filter.filter SHADE3D_FILTER { get => FromEnum.Filter(_SHADE3D_FILTER); }
        #endregion
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
    public enum ColorMappingMode { Mean, Map }
    public enum FilterMode { None, Air, Water, LightAir, LightWater, Shade3d, HeightmapAir, HeightmapWater, REGEX }
}
