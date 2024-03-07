using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Mcasaenk {
    public static class Settings {
        public static int MAXZOOM = 5, MINZOOM = -5;


        public static bool REGIONGRID = false, CHUNKGRID = false, THINREGIONGRIDONLY = false;
        

        public static int MAXCONCURRENCY = 4, CHUNKRENDERMAXCONCURRENCY = 16;


        public static ColorMappingMode COLOR_MAPPING_MODE = ColorMappingMode.Mean;


        public static bool WATER = true;
        public static bool BIOMES = true, WATERBIOMES = false;


        public static bool SHADE3D = false;
        public static int SHADE3DMOODYNESS = (int)(0.80 * -100);

        public static bool STATIC_SHADE = true;
        public static float STATIC_SHADE_POWER { 
            get {
                if(SHADE3D) return 3.0f;
                else return 8.0f;
            } 
        }

        public static double ADEG = 15, BDEG = 20;
            

        public static bool USE_HEIGHTMAPS_GEN = false;
    }

    public enum ColorMappingMode { Mean, Map }
}
