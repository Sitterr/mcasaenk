using Mcasaenk.Rendering;
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
        private readonly int fbo, VAO;
        public readonly KawaseTexture texture1 = new KawaseTexture();
        public PrepKawase(int VAO) : base(ResourceMapping.tile_vert, ResourceMapping.prep_frag) {
            this.VAO = VAO;

            texture1.tints = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, texture1.tints);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

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

        private int fw = -1, fh = -1;
        private void ResizeFramebuffer(int w, int h) {
            if(fw != w || fh != h) {
                GL.BindTexture(TextureTarget.Texture2DArray, texture1.tints);
                GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba8, w, h, 7, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

                GL.BindTexture(TextureTarget.Texture2D, texture1.oceandepth);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16, w, h, 0, PixelFormat.Rg, PixelType.UnsignedShort, IntPtr.Zero);

                fw = w; fh = h;
            }
        }

        public void Use(WorldPosition screen, GenDataTileMap tilemap, int[] blendtints, int R) {
            int w = (int)Math.Ceiling((screen.Width + 2 * R) * screen.InSimZoom), h = (int)Math.Ceiling((screen.Height + 2 * R) * screen.InSimZoom);
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            ResizeFramebuffer((int)Math.Ceiling((screen.Width + 2 * 512) * screen.InSimZoom), (int)Math.Ceiling((screen.Height + 2 * 512) * screen.InSimZoom)); 
            KawaseShader.SetUpFramebuffer(blendtints.Length);
            GL.Viewport((int)((512 - R) * screen.InSimZoom), (int)((512 - R) * screen.InSimZoom), w, h);
            KawaseShader.AttachFramebuffer(fbo, texture1, blendtints.Length);
            //GL.ClearColor(new Color4(0, 0, 0, 0)); GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(Handle);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "tv_zoom"), (float)screen.InSimZoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_cam"), (int)Math.Floor(screen.Start.X - R), (int)Math.Floor(screen.Start.Y - R));
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_regSize"), 512, 512);
            }

            GL.BindVertexArray(VAO);

            if(Global.App.Colormap != null && tilemap != null) {
                // fragment uniforms
                {
                    Global.App.Colormap.TintManager.GetTexture().Use((int)TextureUnit.Texture11);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintpalette"), 11);

                    Global.App.Colormap.BlocksManager.GetTexture().Use((int)TextureUnit.Texture10);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "palette"), 10);

                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintcount"), blendtints.Length);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "blendtints"), blendtints.Length, blendtints);
                }

                // fragment uniforms
                foreach(var reg in tilemap.GetVisibleTilesPositions(new WorldPosition(screen.Start.Add(new System.Windows.Point(-R, -R)), (screen.Width + 2 * R), (screen.Height + 2 * R), screen.zoom))) {
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
