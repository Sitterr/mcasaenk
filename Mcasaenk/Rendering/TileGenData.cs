using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Rendering {
    public class TileGenData {
        public GenData genData;
        private GenDataEditor[] editors;
        private bool shouldUpdate;
        public bool ShouldUpdate {
            get => shouldUpdate; 
            set { 
                shouldUpdate = value;
                if(value == false) { 
                    foreach(var editor in editors) editor.SetUpdateFalse();
                }
            } 
        }

        public TileGenData(GenData genData, GenDataEditor[] editors) {
            this.genData = genData;
            this.editors = editors;
        }


        public void Delete(GenDataEditor editor) {
            editors = editors.Where(e => e != editor).ToArray();
        }
    }



    public abstract class GenDataEditor {
        protected TileGenData tileGenData;
        public GenDataEditor(TileGenData tileGenData) { 
            this.tileGenData = tileGenData;
            //tileGenData.Delete(this);
        }
        public abstract void SetUpdateFalse();
        protected abstract bool CheckForDestruct();
    }
}
