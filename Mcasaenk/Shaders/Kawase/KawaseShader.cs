using Mcasaenk.Resources;
using Mcasaenk.Shaders.Blur;
using Mcasaenk.UI.Canvas;
using OpenTK.Core.Exceptions;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Shaders.Kawase {
    public class KawaseShader : Shader {
        public readonly int fbo;
        private readonly KawaseTexture texture2 = new KawaseTexture();

        private readonly WorldPosition screen;

        private readonly PrepKawase prepShader;

        public KawaseShader(WorldPosition screen) : base(ResourceMapping.def_vert, ResourceMapping.kawase_frag) {
            this.screen = screen;

            prepShader = new PrepKawase(screen);

            texture2.tints = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, texture2.tints);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

            texture2.oceandepth = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture2.oceandepth);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

            fbo = GL.GenFramebuffer();
            //GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        }

        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
            GL.DeleteTexture(texture2.tints);
            GL.DeleteTexture(texture2.oceandepth);
            prepShader.Dispose();
        }
        public void OnResize() {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;
            int w = (int)Math.Ceiling(1 + (screen.Width + 2 * 512) * insimzoom);
            int h = (int)Math.Ceiling(1 + (screen.Height + 2 * 512) * insimzoom);

            GL.BindTexture(TextureTarget.Texture2DArray, texture2.tints);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba8, w, h, 4, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            GL.BindTexture(TextureTarget.Texture2D, texture2.oceandepth);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16, w, h, 0, PixelFormat.Rg, PixelType.UnsignedShort, IntPtr.Zero);

            prepShader.OnResize();
        }

        public static void AttachFramebuffer(int fbo, KawaseTexture texture) {
            GL.BindTexture(TextureTarget.Texture2D, texture.oceandepth);
            GL.BindTexture(TextureTarget.Texture2DArray, texture.tints);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture.oceandepth, 0);
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, texture.tints, 0, 0);
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, texture.tints, 0, 1);
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, texture.tints, 0, 2);
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment4, texture.tints, 0, 3);


            DrawBuffersEnum[] drawBuffers = [DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4];
            GL.DrawBuffers(5, drawBuffers);
        }

        public KawaseTexture Use(int VAO, int[][] kernels, TileMap tilemap) {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;
            int w = (int)Math.Ceiling(1 + (screen.Width + 2 * 512) * insimzoom), h = (int)Math.Ceiling(1 + (screen.Height + 2 * 512) * insimzoom);

            prepShader.Use(VAO, tilemap);

            KawaseTexture[] textures = [prepShader.texture1, texture2];
            KawaseTexture finaltexture = textures[0];

            int[] ikernels = new int[5];
            Array.Fill(ikernels, -1);

            int passes = kernels.Max(k => k.Length);
            for(int p = 0; p < passes; p++) {
                GL.Viewport(0, 0, w, h);
                AttachFramebuffer(fbo, textures[(p + 1) % 2]);
                GL.ClearColor(Color4.Transparent); GL.Clear(ClearBufferMask.ColorBufferBit);
                GL.UseProgram(Handle);

                for(int i = 0; i < kernels.Length; i++) {
                    if(p < kernels[i].Length) ikernels[i] = kernels[i][p];
                    else ikernels[i] = -1;
                }
                GL.Uniform1(GL.GetUniformLocation(Handle, "ikernels"), ikernels.Length, ikernels);

                GL.Uniform1(GL.GetUniformLocation(Handle, "ii"), p);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2DArray, textures[p % 2].tints);
                GL.Uniform1(GL.GetUniformLocation(Handle, "t_tints"), 0);


                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, textures[p % 2].oceandepth);
                GL.Uniform1(GL.GetUniformLocation(Handle, "t_oceandepth"), 1);

                GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                finaltexture = textures[(p + 1) % 2];
            }

            return finaltexture;
        }

    }

    public struct KawaseTexture {
        public int tints, oceandepth;
        public KawaseTexture(int tints, int oceandepth) {
            this.tints = tints;
            this.oceandepth = oceandepth;
        }
    }
}
