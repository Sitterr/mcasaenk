using Mcasaenk.Colormaping;
using Mcasaenk.Resources;
using Mcasaenk.Shaders.Blur;
using Mcasaenk.Shaders.Kawase;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Shaders.Scene {
    public class SceneShader : Shader {
        public int fbo, texture;
        private readonly WorldPosition screen;
        public SceneShader(WorldPosition screen) : base(ResourceMapping.tile_vert, ResourceMapping.scene_frag) {
            this.screen = screen;

            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        }
        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
            GL.DeleteTexture(texture);
        }

        public void OnResize() {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;

            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, (int)Math.Ceiling(1 + screen.Width * insimzoom), (int)Math.Ceiling(1 + screen.Height * insimzoom), 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
        }

        public void Use(int VAO, KawaseTexture kawase, TileMap tilemap) {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;

            int w = (int)Math.Ceiling(1 + screen.Width * insimzoom), h = (int)Math.Ceiling(1 + screen.Height * insimzoom);
            GL.Viewport(0, 0, w, h);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.ClearColor(Color4.Transparent); GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(Handle);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "zoom"), insimzoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "cam"), (int)Math.Floor(screen.Start.X), (int)Math.Floor(screen.Start.Y));
            }

            GL.BindVertexArray(VAO);

            if(Global.App.Colormap != null && tilemap != null) {
                // fragment uniforms
                {
                    //GL.ActiveTexture(TextureUnit.Texture9);
                    //GL.BindTexture(TextureTarget.Texture2D, blurShader.texture);
                    //GL.Uniform1(GL.GetUniformLocation(Handle, "blurdata"), 9);
                    //GL.Uniform1(GL.GetUniformLocation(Handle, "coeff"), blurShader.coeff.Length, blurShader.coeff);

                    //GL.Uniform1(GL.GetUniformLocation(Handle, "R"), (Global.App.Settings.OCEAN_DEPTH_BLENDING - 1) / 2);


                    GL.ActiveTexture(TextureUnit.Texture9);
                    GL.BindTexture(TextureTarget.Texture2D, kawase.oceandepth);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "oceandepth"), 9);

                    Global.App.Colormap.TintManager.GetTexture().Use((int)TextureUnit.Texture11);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintpalette"), 11);

                    Global.App.Colormap.BlocksManager.GetTexture().Use((int)TextureUnit.Texture10);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "palette"), 10);

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
                foreach(var reg in screen.GetVisibleTilePositions()) {
                    var tile = tilemap?.GetTile(reg);
                    if(tile == null) continue;
                    if(tile.genData == null) continue;

                    //tile.genData.GetTexture().Use(0);
                    tile.genData.GetTexture().Use((int)TextureUnit.Texture0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region0"), 0);

                    GL.Uniform2(GL.GetUniformLocation(Handle, "glR"), reg.X, reg.Z);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }

        }
    }
}
