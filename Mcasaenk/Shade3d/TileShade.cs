using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Mcasaenk.Shade3d {
    public class TileShade {
        private Tile tile;
        private readonly int shadeStride;
        public TileShade(Tile tile) {
            this.tile = tile;

            //this.shadeStride = ShadeConstants.;
        }

        private short[] terrainHeights; // 512x512
        private bool[] shadeValues; // 512x512x20
        

        public bool ShouldUpdate() {
            for(int i = 0; i < shadeValues.Length; i += shadeStride) {
                int j;
                for(j = 0; j < shadeStride; j++) {
                    if(shadeValues[j] == false) break;
                }
                if(j == shadeStride) return true;
            }

            return false;
        }
    }
}
