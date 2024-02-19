using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Mcasaenk {
    public static class Settings {

        public static int MAXZOOM = 5, MINZOOM = -5;

        public static bool REGIONGRID = true, CHUNKGRID = false, THINREGIONGRIDONLY = true;

        
        public static int MAXCONCURRENCY = 4, CHUNKRENDERMAXCONCURRENCY = 16;

        
        public static bool SHADE3D = true;

        public static double ADEG = 135, BDEG = 10;

        public static int SHADE3DMOODYNESS = (int)(0.99 * -100);
    }
}
