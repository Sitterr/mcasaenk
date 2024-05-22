using Mcasaenk.Shade3d;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Rendering {
    public interface IGenData {
        public ushort terrainBlock(int i);

        public ushort block(int i);

        public ushort biomeIds(int i);

        public short heights(int i);

        public short terrainHeights(int i);

        public bool depth(int i);

        public bool isShade(int i);
        public void Set_isShade(int i, bool value);
    }

    public class GenData : IGenData { // 72bit
        private ushort[] _blockIds;
        private ushort[] _biomeIds;
        private short[] _heights;
        private short[] _terrainHeights;
        private bool[] _isShade;

        private ushort depthblock;
        public GenData(RawData rawData, ushort depthblock) { 
            this._blockIds = rawData.blockIds;
            this._biomeIds = rawData.biomeIds;
            this._heights = rawData.heights;
            this._terrainHeights = rawData.terrainHeights;
            this._isShade = new bool[512 * 512];

            this.depthblock = depthblock;
        }

        public bool depth(int i) => heights(i) != terrainHeights(i);

        public ushort terrainBlock(int i) => _blockIds[i];

        public ushort block(int i) => depth(i) ? depthblock : terrainBlock(i);

        public ushort biomeIds(int i) => _biomeIds[i];

        public short heights(int i) => _heights[i];

        public short terrainHeights(int i) => _terrainHeights[i];

        public bool isShade(int i) => _isShade[i];
        public void Set_isShade(int i, bool value) { _isShade[i] = value; }
    }

    public class RawData {
        public ushort[] blockIds;
        public ushort[] biomeIds;
        public short[] heights;
        public short[] terrainHeights;
        public bool[] shadeFrame;
        public bool[] shadeValues;
        public byte[] shadeValuesLen;

        public RawData() { 
            blockIds = new ushort[512 * 512];
            biomeIds = new ushort[512 * 512];
            heights = new short[512 * 512];
            terrainHeights = new short[512 * 512];
            if(Global.App.Settings.SHADE3D) {
                shadeFrame = new bool[(ShadeConstants.GLB.rX * 512) * (ShadeConstants.GLB.rZ * 512)];
                shadeValues = new bool[512 * 512 * ShadeConstants.GLB.blockReachLenMax];
                shadeValuesLen = new byte[512 * 512];
            }
        }
    }
}
