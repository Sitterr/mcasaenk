using Mcasaenk.Resources;
using OpenTK.Graphics.OpenGL4;

namespace Mcasaenk.Rendering_Opengl {
    public class DissectShader : Shader {
        private readonly int VAO;
        private readonly int fbo;
        public DissectShader(int VAO) : base(ResourceMapping.def_vert, ResourceMapping.copy_frag) {
            this.VAO = VAO;

            fbo = GL.GenFramebuffer();
        }
        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
        }

        public void Use(int bigtexture, IEnumerable<(Point2i p, int tex)> smalltextures, Point2i smallSize, Point2i bigSize) {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Viewport(0, 0, smallSize.X, smallSize.Z);
            GL.UseProgram(Handle);

            GL.BindVertexArray(VAO);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, bigtexture);
            GL.Uniform1(GL.GetUniformLocation(Handle, "bigtexture"), 0);

            foreach(var sm in smalltextures) {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, sm.tex, 0);

                Point2i st = sm.p * smallSize;
                GL.Uniform2(GL.GetUniformLocation(Handle, "st"), st.X, bigSize.Z - st.Z - smallSize.Z);

                GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            }

        }
    }
}
