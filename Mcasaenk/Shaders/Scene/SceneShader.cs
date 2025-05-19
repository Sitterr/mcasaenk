using Mcasaenk.Colormaping;
using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using Mcasaenk.Shaders.Kawase;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Shaders.Scene {
    public class SceneShader : Shader {
        public int fbo;
        private readonly int VAO = 0;
        public SceneShader(int VAO) : base(ResourceMapping.tile_vert, ResourceMapping.scene_frag) {
            this.VAO = VAO;

            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        }
        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
        }

        public void Use(WorldPosition screen, GenDataTileMap tilemap, KawaseTexture tex, int[] blendtints, int kawaseR, int outputtexture) {
            int w = (int)Math.Ceiling(screen.Width * screen.InSimZoom), h = (int)Math.Ceiling(screen.Height * screen.InSimZoom);
            
            GL.Viewport(0, 0, w, h);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, outputtexture, 0);

            GL.ClearColor(Color4.Transparent); GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(Handle);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "tv_zoom"), (float)screen.InSimZoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_cam"), (int)Math.Floor(screen.Start.X), (int)Math.Floor(screen.Start.Y));
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_regSize"), 512, 512);
            }

            GL.BindVertexArray(VAO);

            if(Global.App.Colormap != null && tilemap != null) {
                // fragment uniforms
                {
                    // blured data
                    {
                        GL.ActiveTexture(TextureUnit.Texture8);
                        GL.BindTexture(TextureTarget.Texture2D, tex.oceandepth);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_oceandepth"), 8);

                        GL.ActiveTexture(TextureUnit.Texture9);
                        GL.BindTexture(TextureTarget.Texture2DArray, tex.tints);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_tintcolors"), 9);

                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_tintcount"), blendtints.Length);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_blendtints"), blendtints.Length, blendtints);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_R"), kawaseR);
                    }

                    Global.App.Colormap.TintManager.GetTexture().Use((int)TextureUnit.Texture11);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintpalette"), 11);

                    Global.App.Colormap.BlocksManager.GetTexture().Use((int)TextureUnit.Texture10);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "palette"), 10);
                }

                // settings
                {
                    GL.Uniform1(GL.GetUniformLocation(Handle, "CONTRAST"), (float)Global.Settings.Contrast);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "SUN_LIGHT"), Global.Settings.SUN_LIGHT / 15f);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "BLOCK_LIGHT"), Global.Settings.BLOCK_LIGHT / 15f);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "WATER_TRANSPARENCY"), (float)Global.Settings.WATER_TRANSPARENCY);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "WATER_SMART_SHADE"), Global.Settings.WATER_SMART_SHADE ? 1 : 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "SHADE3D"), Global.Settings.SHADE3D ? 1 : 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "STATIC_SHADE"), Global.Settings.STATIC_SHADE ? 1 : 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "ADEG"), (float)Global.Settings.ADEG);
                }

                // fragment uniforms
                foreach (var reg in tilemap.GetVisibleTilesPositions(screen)) {
                    var tile = tilemap?.GetTile(reg);
                    if(tile == null) continue;

                    tile.GetTexture().Use((int)TextureUnit.Texture0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region0"), 0);

                    GL.Uniform2(GL.GetUniformLocation(Handle, "tv_glR"), reg.X, reg.Z);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }

        }
    }
}
