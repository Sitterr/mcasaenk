using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using Mcasaenk.UI.Canvas;
using OpenTK.Compute.OpenCL;
using OpenTK.Core.Exceptions;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.Opengl_rendering.Kawase {
    public class KawaseShader : Shader {
        private readonly int fbo;
        private readonly KawaseTexture texture2 = new KawaseTexture();

        private readonly int VAO;
        public KawaseShader(int VAO) : base(ResourceMapping.def_vert, ResourceMapping.kawase_frag) {
            this.VAO = VAO;

            texture2.tints = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, texture2.tints);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

            texture2.meanheight_oceandepth = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture2.meanheight_oceandepth);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, [0f, 0f, 0f, 0f]);

            fbo = GL.GenFramebuffer();
            //GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        }

        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
            GL.DeleteTexture(texture2.tints);
            GL.DeleteTexture(texture2.meanheight_oceandepth);
        }

        int[] ikernels = new int[8];
        int[][] kawasepasses = new int[8][];
        public int[] blendtints = [];

        private int fw = -1, fh = -1;
        private void ResizeFramebuffer(int w, int h) {
            if(fw != w || fh != h) {
                GL.BindTexture(TextureTarget.Texture2DArray, texture2.tints);
                GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba8, w, h, 7, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

                GL.BindTexture(TextureTarget.Texture2D, texture2.meanheight_oceandepth);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32ui, w, h, 0, PixelFormat.RedInteger, PixelType.UnsignedInt, IntPtr.Zero);

                fw = w; fh = h;
            }
        }

        public void UpdateBlendTintCounter(int[] blendtints) { 
            this.blendtints = blendtints;
        }

        public static void AttachFramebuffer(int fbo, KawaseTexture texture, int tintcount) {
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture.meanheight_oceandepth, 0);
            for(int i = 0; i < tintcount; i++) {
                GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1 + i, texture.tints, 0, i);
            }
        }

        public static void SetUpFramebuffer(int tintcount) {
            DrawBuffersEnum[] drawBuffers = new DrawBuffersEnum[1 + tintcount];
            for(int i = 0; i < 1 + tintcount; i++) {
                drawBuffers[i] = DrawBuffersEnum.ColorAttachment0 + i;
            }
            GL.DrawBuffers(drawBuffers.Length, drawBuffers);
        }

        public unsafe KawaseTexture Use(WorldPosition screen, Span<int> kernels, int R, KawaseTexture texture1) {
            int w = (int)Math.Ceiling((screen.Width + 2 * R) * screen.InSimZoom), h = (int)Math.Ceiling((screen.Height + 2 * R) * screen.InSimZoom);

            KawaseTexture[] textures = [texture1, texture2];
            KawaseTexture finaltexture = textures[0];

            Array.Fill(ikernels, -1);
            int passes = 0;
            for(int i = 0; i < kernels.Length; i++) { 
                kawasepasses[i] = KawaseKernels.Get(kernels[i]);
                passes = Math.Max(passes, kawasepasses[i].Length);
            }

            if(passes > 0) {
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                    ResizeFramebuffer((int)Math.Ceiling((screen.Width + 2 * 512) * screen.InSimZoom), (int)Math.Ceiling((screen.Height + 2 * 512) * screen.InSimZoom));
                    SetUpFramebuffer(blendtints.Length);


                    GL.Viewport((int)((512 - R) * screen.InSimZoom), (int)((512 - R) * screen.InSimZoom), w, h);
                    GL.Scissor((int)((512 - R) * screen.InSimZoom), (int)((512 - R) * screen.InSimZoom), w, h);
                }
                
                GL.UseProgram(Handle);

                {
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintcount"), blendtints.Length);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "t_tints"), 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "t_meanheight_oceandepth"), 1);
                }

                for(int p = 0; p < passes; p++) {
                    AttachFramebuffer(fbo, textures[(p + 1) % 2], blendtints.Length);                

                    for(int i = 0; i < kernels.Length; i++) {
                        if(p < kawasepasses[i].Length) ikernels[i] = (int)(kawasepasses[i][p] * screen.InSimZoom);
                        else ikernels[i] = -1;
                    }
                    GL.Uniform1(GL.GetUniformLocation(Handle, "ikernels"), ikernels.Length, ikernels);

                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2DArray, textures[p % 2].tints);
                    


                    GL.ActiveTexture(TextureUnit.Texture1);
                    GL.BindTexture(TextureTarget.Texture2D, textures[p % 2].meanheight_oceandepth);
                    

                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                    finaltexture = textures[(p + 1) % 2];
                }
            }

            return finaltexture;
        }
    }

    public static class KawaseKernels {
        public readonly static IDictionary<int, KawaseKernel> kernels;
        static KawaseKernels() {
            kernels = new Dictionary<int, KawaseKernel>();
            foreach(string _line in ResourceMapping.kawase_approximations.Split(Environment.NewLine)) {
                KawaseKernel krnl = new KawaseKernel();
                int k = -1;

                try {
                    string line = _line.Replace(" ", "");
                    string[] parts = line.Split(':');

                    string arr = parts[1].Trim('[', ']');
                    if(arr == "") krnl.kernel = new int[0];
                    else krnl.kernel = arr.Split(',').Select(int.Parse).ToArray();

                    foreach(var part in parts[0].Split(',')) {
                        if(part.StartsWith("k=")) k = Convert.ToInt32(part.Split('=')[1], CultureInfo.InvariantCulture);
                        if(part.StartsWith("sim=")) krnl.approximation = Convert.ToDouble(part.Split('=')[1].TrimEnd('%'), CultureInfo.InvariantCulture) / 100;
                    }

                    if(k == -1) throw new Exception();
                }
                catch { continue; }

                kernels.Add(k, krnl);
            }

            kernels = kernels.ToFrozenDictionary();
        }

        public struct KawaseKernel {
            public int[] kernel;
            public double approximation;
            public KawaseKernel(int[] kernel, double approximation) {
                this.kernel = kernel;
                this.approximation = approximation;
            }
        }

        public static int[] Get(int k) {
            if(k <= 1) return [];
            if(kernels.ContainsKey(k)) return kernels[k].kernel;
            else return Get(k - 2);
        }
    }

    public struct KawaseTexture {
        public int tints, meanheight_oceandepth;
        public KawaseTexture(int tints, int oceandepth) {
            this.tints = tints;
            this.meanheight_oceandepth = oceandepth;
        }
    }
}
