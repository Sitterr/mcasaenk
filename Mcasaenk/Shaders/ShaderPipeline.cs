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
using System.Diagnostics;

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

        public void Render(WorldPosition screen, GenDataTileMap tilemap, int outputtexture) {
            Span<int> kernels = stackalloc int[1 + kawaseShader.blendtints.Length];
            kernels[0] = Global.Settings.TRANSPARENTLAYERS > 0 ? Global.Settings.OCEAN_DEPTH_BLENDING : 0;

            var blendtints = Global.App.Colormap?.TintManager.GetBlendingTints();
            for(int i = 0; i < kawaseShader.blendtints.Length; i++) {
                var tint = blendtints[i];
                if(tint is DynamicTint dt) kernels[1 + i] = dt.Blend;
                else kernels[1 + i] = 0;
            }

            int maxR = 0;
            for(int i = 0; i < kernels.Length; i++) if(kernels[i] > maxR) maxR = kernels[i];
            maxR = (maxR - 1) / 2;
            int sc = (int)Math.Pow(2, Math.Abs(screen.ZoomScale));
            maxR += (sc - maxR % sc);

            prepShader.Use(screen, tilemap, kawaseShader.blendtints, maxR);
            var kawase_tex = kawaseShader.Use(screen, kernels, maxR, prepShader.texture1);
            sceneShader.Use(screen, tilemap, kawase_tex, kawaseShader.blendtints, maxR, outputtexture);

        }
    }
}
