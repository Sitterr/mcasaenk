using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Rendering {

    public class GenData { // 72bit
        public ushort[] _blockIds;
        private ushort[] _blockIds_OG, _blocksIds_temp;

        public ushort[] _biomeIds;
        private ushort[] _biomeIds_OG, _biomeIds_temp;

        public short[] _heights;
        private short[] _heights_OG, _heights_temp;

        public short[] _terrainHeights;
        private short[] _terrainHeights_OG, _terrainHeights_temp;

        public bool[] _isShade;
        private bool[] _isShade_OG, _isShade_temp;

        public byte[] _blocklights;
        private byte[] _blocklights_OG, _blockslights_temp;

        private ushort depthblock;
        public GenData(RawData rawData, ushort depthblock) { 
            this._blockIds = this._blockIds_OG = rawData.blockIds;
            this._biomeIds = this._biomeIds_OG = rawData.biomeIds;
            this._heights = this._heights_OG = rawData.heights;
            this._terrainHeights = this._terrainHeights_OG = rawData.terrainHeights;
            this._isShade = this._isShade_OG = new bool[512 * 512];
            this._blocklights = this._blocklights_OG = rawData.blockLights;

            this.depthblock = depthblock;
        }
        public void SetTemporal_TerrainHeights() {
            _terrainHeights = _terrainHeights_temp = ArrayPool<short>.Shared.Rent(512 * 512);
            for(int i=0;i<512*512;i++) _terrainHeights_temp[i] = _terrainHeights_OG[i];
        }
        public void ClearTemporal() {
            if(_terrainHeights_temp != null) {
                _terrainHeights = _terrainHeights_OG;
                ArrayPool<short>.Shared.Return(_terrainHeights_temp);
                _terrainHeights_temp = null;
            }
        }

        public bool depth(int i) => heights(i) != terrainHeights(i);

        public ushort terrainBlock(int i) => _blockIds[i];

        public ushort block(int i) => depth(i) ? depthblock : terrainBlock(i);

        public ushort biomeIds(int i) => _biomeIds[i];

        public short heights(int i) => _heights[i];

        public short terrainHeights(int i) => _terrainHeights[i];

        public byte blockLights(int i) => _blocklights[i];


        public bool isShade(int i) => _isShade[i];
        public void Set_isShade(int i, bool value) { _isShade[i] = value; }



        int empty = 0;
        public bool ContainsEmpty() {
            if(empty == 0) {
                empty = 2;
                for(int i = 0; i < 512 * 512; i++) {
                    if(block(i) == default || block(i) == Colormap.INVBLOCK) {
                        empty = 1; 
                        break;
                    }
                }
            }
            if(empty == 2) return false;
            else return true;
        }
    }

    public class RawData {
        public ushort[] blockIds;
        public ushort[] biomeIds;
        public short[] heights;
        public short[] terrainHeights;
        public bool[] shadeFrame;
        public bool[] shadeValues;
        public byte[] shadeValuesLen;
        public byte[] blockLights;

        public RawData() { 
            blockIds = new ushort[512 * 512];
            biomeIds = new ushort[512 * 512];
            heights = new short[512 * 512];
            terrainHeights = new short[512 * 512];
            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.SHADE3D) {
                shadeFrame = new bool[(ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512)];
                shadeValues = new bool[512 * 512 * ShadeConstants.GLB.blockReachLenMax];
                shadeValuesLen = new byte[512 * 512];
            }
            blockLights = new byte[512 * 512];
        }
    }
}
