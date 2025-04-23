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
        private readonly WorldPosition screen;
        public ScaleShader(WorldPosition screen) : base(ResourceMapping.def_vert, ResourceMapping.scale_frag) {
            this.screen = screen;
        }


        public void Use(int VAO, int scene_texture) {
            float insimzoom = screen.zoom > 1 ? 1f : (float)screen.zoom;
            float outsimzoom = screen.zoom < 1 ? 1f : (float)screen.zoom;
            int w = (int)Math.Ceiling(1 + screen.Width * insimzoom), h = (int)Math.Ceiling(1 + screen.Height * insimzoom);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 1);
            GL.Viewport(-(int)Math.Floor(screen.Start.X.DecPart() * outsimzoom), -(int)Math.Floor((1 - screen.Start.Y.DecPart()) * outsimzoom), (int)(w * outsimzoom), (int)(h * outsimzoom));

            GL.ClearColor(new Color4(15, 15, 15, 255)); GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(Handle);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, scene_texture);
            GL.Uniform1(GL.GetUniformLocation(Handle, "screenTexture"), 0);

            GL.Uniform2(GL.GetUniformLocation(Handle, "glPos"), (float)screen.Start.X, (float)screen.Start.Y);
            GL.Uniform2(GL.GetUniformLocation(Handle, "size"), w, h);
            GL.Uniform1(GL.GetUniformLocation(Handle, "zoom"), (float)screen.zoom);

            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }
    }
}
