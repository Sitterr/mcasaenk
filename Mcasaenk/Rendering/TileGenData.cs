using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Rendering {
 
    public abstract class GenDataEditor {
        protected Tile tile;
        public GenDataEditor(Tile tile) {
            this.tile = tile;
        }

        public bool IsActive { get; set; }


        protected object locker = new object();

        protected void CheckDestruct() {
            if(ShouldDestruct()) {
                IsActive = false;
                Destruct();
            }
        }

        protected abstract bool ShouldDestruct();
        protected abstract void Destruct();
    }
}
