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
            get {
                return shouldUpdate;
                //foreach(var editor in editors) if(editor.ShouldRedraw) return true;
                //return false;
            }
            set {
                shouldUpdate = shouldUpdate || value;

                //if(value == false) {
                //    foreach(var editor in editors) {
                //        if(editor.IsActive) {
                //            editor.ShouldRedraw = false;
                //        }
                //    }
                //}
            }
        }
        public void Redrawn() { 
            shouldUpdate = false;
            foreach(var editor in editors) {
                editor.ShouldRedraw = false;
            }
            CheckDestruct();
        }

        public TileGenData(params GenDataEditor[] editors) {
            this.editors = editors;
        }

        bool safed = false;
        public void Safe(GenData genData) {
            this.genData = genData;
            safed = true;
        }
        public void FinishedSafe() {
            if(safed) IsActive = true;
        }

        public bool IsActive { get; private set; }

        public void CheckDestruct() {
            if(editors.All(e => e.IsActive == false) && ShouldUpdate == false) {
                genData = null;
                safed = false;
                IsActive = false;
            }
        }
    }



    public abstract class GenDataEditor {
        protected Tile tile;
        protected GenData genData { get => tile.contgen.genData; }
        public GenDataEditor(Tile tile) {
            this.tile = tile;
        }

        private bool isActive;
        public bool IsActive {
            get {
                return isActive;
            }
            set {
                lock(locker) {
                    isActive = value;
                    if(value == false) {
                        Destruct();
                        ShouldRedraw = false;
                        tile.contgen.CheckDestruct();
                    }
                }
            }
        }

        public object locker = new object();
        private bool shouldRedraw;
        public bool ShouldRedraw {
            get {
                return shouldRedraw;
            }
            set {
                //lock(locker) {
                    shouldRedraw = value;
                    tile.contgen.ShouldUpdate = value;
                //}
            }
        }

        protected void CheckDestruct() { 
            if(ShouldDestruct()) IsActive = false;
        }

        protected abstract bool ShouldDestruct();
        protected abstract void Destruct();

        public abstract bool Recalc();
    }
}
