using Mcasaenk.Resources;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Shaders.Kawase {
    class PrepKawase : Shader {
        public readonly int fbo;
        public readonly KawaseTexture texture1 = new KawaseTexture();
        private readonly WorldPosition screen;
        public PrepKawase(WorldPosition screen) : base(ResourceMapping.tile_vert, ResourceMapping.prep_frag) {
            this.screen = screen;

            texture1.tints = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, texture1.tints);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

            texture1.oceandepth = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture1.oceandepth);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

            fbo = GL.GenFramebuffer();
        }
        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
            GL.DeleteTexture(texture1.tints);
            GL.DeleteTexture(texture1.oceandepth);
        }

        public void OnResize() {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;
            int w = (int)Math.Ceiling(1 + (screen.Width + 2 * 512) * insimzoom);
            int h = (int)Math.Ceiling(1 + (screen.Height + 2 * 512) * insimzoom);

            GL.BindTexture(TextureTarget.Texture2DArray, texture1.tints);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba8, w, h, 4, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            GL.BindTexture(TextureTarget.Texture2D, texture1.oceandepth);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16, w, h, 0, PixelFormat.Rg, PixelType.UnsignedShort, IntPtr.Zero);
        }

        public void Use(int VAO, TileMap tilemap) {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;
            int w = (int)Math.Ceiling(1 + (screen.Width + 2 * 512) * insimzoom), h = (int)Math.Ceiling(1 + (screen.Height + 2 * 512) * insimzoom);
            
            GL.Viewport(0, 0, w, h);
            KawaseShader.AttachFramebuffer(fbo, texture1);
            GL.ClearColor(Color4.Transparent); GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(Handle);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "zoom"), insimzoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "cam"), (int)Math.Floor(screen.Start.X - 512), (int)Math.Floor(screen.Start.Y - 512));
            }

            GL.BindVertexArray(VAO);

            if(Global.App.Colormap != null && tilemap != null) {
                // fragment uniforms
                {
                    Global.App.Colormap.TintManager.GetTexture().Use((int)TextureUnit.Texture11);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintpalette"), 11);

                    Global.App.Colormap.BlocksManager.GetTexture().Use((int)TextureUnit.Texture10);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "palette"), 10);
                }

                // fragment uniforms
                foreach(var reg in new WorldPosition(screen.Start.Add(new System.Windows.Point(-512, -512)), (screen.Width + 2 * 512) / screen.zoom, (screen.Height + 2 * 512) / screen.zoom, screen.zoom).GetVisibleTilePositions()) {
                    var tile = tilemap?.GetTile(reg);
                    if(tile == null) continue;
                    if(tile.genData == null) continue;

                    tile.genData.GetTexture().Use((int)TextureUnit.Texture0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region0"), 0);

                    GL.Uniform2(GL.GetUniformLocation(Handle, "glR"), reg.X, reg.Z);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }
        }
    }
}
