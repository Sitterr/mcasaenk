using CommunityToolkit.HighPerformance;
using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using Mcasaenk.Opengl_rendering.Scene;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Opengl_rendering.Scale {
    public class ScaleShader : Shader {
        private readonly int VAO;
        public ScaleShader(int VAO) : base(ResourceMapping.tile_vert, ResourceMapping.scale_frag) {
            this.VAO = VAO;
        }


        int[] isloading = new int[400], isqueued = new int[400];

        public void Use(WorldPosition screen, OpenGLDrawTileMap drawtilemap, GenDataTileMap gentilemap, ScreenshotManager screenshot, int fbo = 1) {
            int w = (int)Math.Ceiling(1 + screen.Width * screen.InSimZoom), h = (int)Math.Ceiling(1 + screen.Height * screen.InSimZoom);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(-(int)Math.Floor(screen.Start.X.DecPart() * screen.OutSimzoom), -(int)Math.Floor((1 - screen.Start.Y.DecPart()) * screen.OutSimzoom), (int)Math.Ceiling(w * screen.OutSimzoom), (int)Math.Ceiling(h * screen.OutSimzoom));

            GL.ClearColor(new Color4(15, 15, 15, 255)); GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(Handle);
            GL.BindVertexArray(VAO);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "tv_zoom"), (float)screen.InSimZoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_cam"), (int)Math.Floor(screen.Start.X), (int)Math.Floor(screen.Start.Y));
                int tilesize = 0;
                if (drawtilemap != null) tilesize = drawtilemap.TileSize;
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_regSize"), tilesize, tilesize);
            }

            // settings
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "REGIONGRID"), (int)Global.Settings.REGIONGRID);
                GL.Uniform1(GL.GetUniformLocation(Handle, "CHUNKGRID"), (int)Global.Settings.CHUNKGRID);
                GL.Uniform1(GL.GetUniformLocation(Handle, "BACKGROUND"), (int)Global.Settings.BACKGROUND);
                GL.Uniform1(GL.GetUniformLocation(Handle, "MAPGRID"), (int)Global.Settings.MAPGRID);
                GL.Uniform1(GL.GetUniformLocation(Handle, "OVERLAYS"), Global.Settings.OVERLAYS ? 1 : 0);
                GL.Uniform1(GL.GetUniformLocation(Handle, "UNLOADED"), Global.Settings.UNLOADED ? 1 : 0);
            }

            GL.Uniform1(GL.GetUniformLocation(Handle, "zoom"), (float)screen.zoom);

            if(screenshot != null) {
                var screenshotrec = screenshot.AsRect();
                GL.Uniform4(GL.GetUniformLocation(Handle, "screenshot"), (float)screenshotrec.X, (float)screenshotrec.Y, (float)screenshotrec.Width, (float)screenshotrec.Height);
                GL.Uniform1(GL.GetUniformLocation(Handle, "screenshot_resizable"), screenshot.canResize ? 1 : 0);

                var statecolor = WPFColor.FromUInt((uint)screenshot.GetState(gentilemap));
                GL.Uniform3(GL.GetUniformLocation(Handle, "screenshot_statecolor"), statecolor.R / 255f, statecolor.G / 255f, statecolor.B / 255f);

            } else {
                GL.Uniform4(GL.GetUniformLocation(Handle, "screenshot"), 0f, 0f, 0f, 0f);
            }

            // per region data
            if(gentilemap != null) {
                Array.Fill(isloading, 0);
                Array.Fill(isqueued, 0);

                var (min, max) = gentilemap.GetVisibleRect(screen.Extend(1));
                int rw = max.X - min.X + 1, rh = max.Z - min.Z + 1;

                for(int x = 0; x < rw; x++) {
                    for(int z = 0; z < rh; z++) {
                        int indx = z * rw + x;
                        if(indx >= 400 * 32) break;
                        isloading[indx / 32] = isloading[indx / 32] | (gentilemap.IsLoading(min + new Point2i(x, z)) ? 1 : 0) << (indx % 32);
                        isqueued[indx / 32] = isqueued[indx / 32] | (gentilemap.IsQueued(min + new Point2i(x, z)) ? 1 : 0) << (indx % 32);
                    }
                }

                GL.Uniform3(GL.GetUniformLocation(Handle, "reg_regRect"), min.X, min.Z, rw);
                GL.Uniform1(GL.GetUniformLocation(Handle, "reg_isloading"), (int)Math.Min(400, Math.Ceiling(rw * rh / 32.0)), isloading);
                GL.Uniform1(GL.GetUniformLocation(Handle, "reg_isqueued"), (int)Math.Min(400, Math.Ceiling(rw * rh / 32.0)), isqueued);
            }


            // per pre-rendered draw image
            if(drawtilemap != null) {
                foreach(var reg in drawtilemap.GetVisibleTilesPositions(screen.Extend(1))) {
                    var tileTex = drawtilemap.GetTile(reg);
                    if(tileTex == default) tileTex = drawtilemap.emptyTile;

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
