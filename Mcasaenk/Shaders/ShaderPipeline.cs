using Mcasaenk.Shaders.Kawase;
using Mcasaenk.Shaders.Scale;
using Mcasaenk.Shaders.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Extentions;
using System.Windows;
using Mcasaenk.Colormaping;
using Mcasaenk.UI.Canvas;
using Mcasaenk.Rendering;

namespace Mcasaenk.Shaders {
    public class ShaderPipeline : IDisposable {
        private readonly PrepKawase prepShader;
        public readonly KawaseShader kawaseShader;
        private readonly SceneShader sceneShader;

        public ShaderPipeline(int VAO) {
            sceneShader = new SceneShader(VAO);
            kawaseShader = new KawaseShader(VAO);
            prepShader = new PrepKawase(VAO);

            GL.BindVertexArray(0);

            int maxTextureBufferSize;
            GL.GetInteger(GetPName.MaxTextureBufferSize, out maxTextureBufferSize);

            int maxssbo;
            GL.GetInteger(GetPName.MaxUniformBufferBindings, out maxssbo);

            int maxtextures;
            GL.GetInteger(GetPName.MaxTextureImageUnits, out maxtextures);

            int maxrazshirenia;
            GL.GetInteger(GetPName.MaxDrawBuffers, out maxrazshirenia);
        }

        bool disposed = false;
        public void Dispose() {
            if(!disposed) {
                sceneShader.Dispose();
                kawaseShader.Dispose();
                prepShader.Dispose();
            }
            disposed = true;
        }

        public void OnRender(WorldPosition screen, GenDataTileMap tilemap, int outputtexture) {
            int[] kawaseKernels = new int[1 + kawaseShader.blendtints.Length];
            kawaseKernels[0] = Global.Settings.TRANSPARENTLAYERS > 0 ? Global.Settings.OCEAN_DEPTH_BLENDING : 0;

            var blendtints = Global.App.Colormap?.TintManager.GetBlendingTints();
            for(int i = 0; i < kawaseShader.blendtints.Length; i++) {
                var tint = blendtints[i];
                if(tint is DynamicTint dt) kawaseKernels[1 + i] = dt.Blend;
                else kawaseKernels[1 + i] = 0;
            }
            int kawaseReach = (kawaseKernels.Max() - 1) / 2;
            kawaseReach = (int)(kawaseReach * 1.1);

            prepShader.Use(screen, tilemap, kawaseShader.blendtints, kawaseReach);
            var kawase_tex = kawaseShader.Use(screen, kawaseKernels, kawaseReach, prepShader.texture1);
            sceneShader.Use(screen, tilemap, kawase_tex, kawaseShader.blendtints, kawaseReach, outputtexture);

        }
    }
}
