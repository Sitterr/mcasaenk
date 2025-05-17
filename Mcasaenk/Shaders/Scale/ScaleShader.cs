using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using Mcasaenk.Shaders.Scene;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Shaders.Scale {
    public class ScaleShader : Shader {
        private readonly int VAO;
        public ScaleShader(int VAO) : base(ResourceMapping.tile_vert, ResourceMapping.scale_frag) {
            this.VAO = VAO;
        }


        public void Use(WorldPosition screen, OpenGLDrawTileMap tilemap, int fbo = 1) {
            int w = (int)Math.Ceiling(1 + screen.Width * screen.InSimZoom), h = (int)Math.Ceiling(1 + screen.Height * screen.InSimZoom);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(-(int)Math.Floor(screen.Start.X.DecPart() * screen.OutSimzoom), -(int)Math.Floor((1 - screen.Start.Y.DecPart()) * screen.OutSimzoom), (int)(w * screen.OutSimzoom), (int)(h * screen.OutSimzoom));

            GL.ClearColor(new Color4(15, 15, 15, 255)); GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(Handle);
            GL.BindVertexArray(VAO);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "tv_zoom"), (float)screen.InSimZoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_cam"), (int)Math.Floor(screen.Start.X), (int)Math.Floor(screen.Start.Y));
                int tilesize = 0;
                if (tilemap != null) tilesize = tilemap.TileSize;
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_regSize"), tilesize, tilesize);
            }

            GL.Uniform3(GL.GetUniformLocation(Handle, "defcolor"), 15 / 255f, 15 / 255f, 15 / 255f);
            GL.Uniform2(GL.GetUniformLocation(Handle, "glPos"), (float)screen.Start.X, (float)screen.Start.Y);
            GL.Uniform2(GL.GetUniformLocation(Handle, "size"), w, h);
            //GL.Uniform1(GL.GetUniformLocation(Handle, "zoom"), (float)screen.zoom);

            if (tilemap != null) {
                foreach (var reg in tilemap.GetVisibleTilesPositions(screen)) {
                    var tileTex = tilemap.GetTile(reg);
                    if (tileTex == default) continue;

                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, tileTex);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region0"), 0);

                    GL.Uniform2(GL.GetUniformLocation(Handle, "tv_glR"), reg.X, reg.Z);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }
        }
    }
}
