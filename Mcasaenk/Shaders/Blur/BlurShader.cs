using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mcasaenk.Resources;
using Mcasaenk.Shaders;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Mcasaenk.Shaders.Blur {
    public class BlurShader : Shader {
        const int maxR = 127;
        public readonly float[] coeff = new float[maxR + 1];

        public readonly int fbo, texture;
        private readonly WorldPosition screen;
        public BlurShader(WorldPosition screen) : base(ResourceMapping.tile_vert, ResourceMapping.blur_frag) {
            this.screen = screen;

            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.GenerateMipmap, 0);
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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, (int)Math.Ceiling(1 + (screen.Width + 2 * maxR) * insimzoom), (int)Math.Ceiling(1 + screen.Height * insimzoom), 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
        }

        public void Use(int VAO, TileMap tilemap) {
            int R = (Global.App.Settings.OCEAN_DEPTH_BLENDING - 1) / 2;
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;

            int w = (int)Math.Ceiling(1 + (screen.Width + 2 * R) * insimzoom), h = (int)Math.Ceiling(1 + screen.Height * insimzoom);
            GL.Viewport((int)((maxR - R) * insimzoom), 0, w, h);
            //GL.Scissor(maxR - R, 0, w, h);
            //GL.Enable(EnableCap.ScissorTest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);


            GL.ClearColor(Color4.Transparent); GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(Handle);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "zoom"), insimzoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "cam"), (int)Math.Floor(screen.Start.X - R), (int)Math.Floor(screen.Start.Y));
            }

            GL.BindVertexArray(VAO);

            if(Global.App.Colormap != null && tilemap != null) {
                // fragment uniforms
                {
                    Global.App.Colormap.TintManager.GetTexture().Use((int)TextureUnit.Texture11);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintpalette"), 11);

                    Global.App.Colormap.BlocksManager.GetTexture().Use((int)TextureUnit.Texture10);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "palette"), 10);

                    GL.Uniform1(GL.GetUniformLocation(Handle, "R"), R);

                    fillcoeff(coeff.AsSpan().Slice(0, R + 1));
                    GL.Uniform1(GL.GetUniformLocation(Handle, "coeff"), coeff.Length, coeff);
                }

                // fragment uniforms
                foreach(var reg in new WorldPosition(screen.Start.Add(new System.Windows.Point(-R, 0)), (screen.Width + 2 * R) / screen.zoom, screen.Height / screen.zoom, screen.zoom).GetVisibleTilePositions()) {
                    var tile = tilemap?.GetTile(reg);
                    if(tile == null) continue;
                    if(tile.genData == null) continue;


                    tilemap?.GetTile(tile.pos + new Point2i(-1, -1))?.genData?.GetTexture().Use((int)TextureUnit.Texture1);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_aa"), 1);
                    tilemap?.GetTile(tile.pos + new Point2i(-1,  0))?.genData?.GetTexture().Use((int)TextureUnit.Texture2);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_ab"), 2);
                    tilemap?.GetTile(tile.pos + new Point2i(-1,  1))?.genData?.GetTexture().Use((int)TextureUnit.Texture3);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_ac"), 3);
                    tilemap?.GetTile(tile.pos + new Point2i( 0, -1))?.genData?.GetTexture().Use((int)TextureUnit.Texture4);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_ba"), 4);
                    tile.genData.GetTexture().Use((int)TextureUnit.Texture0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region0"), 0);
                    tilemap?.GetTile(tile.pos + new Point2i( 0,  1))?.genData?.GetTexture().Use((int)TextureUnit.Texture5);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_bc"), 5);
                    tilemap?.GetTile(tile.pos + new Point2i( 1, -1))?.genData?.GetTexture().Use((int)TextureUnit.Texture6);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_ca"), 6);
                    tilemap?.GetTile(tile.pos + new Point2i( 1,  0))?.genData?.GetTexture().Use((int)TextureUnit.Texture7);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_cb"), 7);
                    tilemap?.GetTile(tile.pos + new Point2i( 1,  1))?.genData?.GetTexture().Use((int)TextureUnit.Texture8);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region_cc"), 8);


                    GL.Uniform2(GL.GetUniformLocation(Handle, "glR"), reg.X, reg.Z);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }
        }



        static void fillcoeff(Span<float> coeff) {
            if(coeff.Length == 1) {
                coeff[0] = 1;
                return;
            }

            int d = (coeff.Length - 1) * 2 + 1;
            float s = (d - 1) / 6f;

            for(int i = 0; i < coeff.Length; i++) {
                coeff[i] = (float)(1 * 255 * f(i, s));
            }
        }
        static double f(int x, double s) => (1 / Math.Sqrt(2 * Math.PI * s * s)) * Math.Pow(Math.E, -(x * x) / (2 * s * s));

    }
}
