using Mcasaenk.Colormaping;
using Mcasaenk.Rendering;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;

namespace Mcasaenk.Rendering_Opengl {
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

        public void Render(WorldPosition screen, GenDataTileMap tilemap, Colormap colormap, WorldPosition map_screenshot, int outputtexture) {
            if(colormap == null) return;
            var blendtints = colormap.TintManager.GetBlendingTints();
            if(blendtints?.Count > 7) blendtints.RemoveRange(7, outputtexture - 7);

            Span<int> kernels = stackalloc int[1 + blendtints.Count];
            kernels[0] = Global.Settings.TRANSPARENTLAYERS > 0 ? Global.Settings.OCEAN_DEPTH_BLENDING : 0;


            for(int i = 0; i < blendtints.Count; i++) {
                var tint = blendtints[i];
                if(tint is DynamicTint dt) kernels[1 + i] = dt.Blend;
                else kernels[1 + i] = 0;
            }

            int maxR = 0;
            for(int i = 0; i < kernels.Length; i++) if(kernels[i] > maxR) maxR = kernels[i];
            maxR = (maxR - 1) / 2;
            int sc = (int)(1 / screen.InSimZoom);
            maxR += (sc - maxR % sc);

            var blendtintindexes = blendtints.Select(t => colormap.TintManager.IndexOf(t)).ToArray();
            prepShader.Use(screen, tilemap, colormap, blendtintindexes, maxR);
            var kawase_tex = kawaseShader.Use(screen, kernels, blendtintindexes, maxR, prepShader.texture1);
            sceneShader.Use(screen, tilemap, colormap, kawase_tex, map_screenshot, blendtintindexes, maxR, outputtexture);
        }
    }
}
