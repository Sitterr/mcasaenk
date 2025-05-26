using CommunityToolkit.HighPerformance.Buffers;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Mcasaenk.Opengl_rendering {
    public class Shader : IDisposable {
        public int Handle;

        private bool disposedValue = false;

        public virtual void Dispose() {
            if(!disposedValue) {
                GC.SuppressFinalize(this);
                GL.DeleteProgram(Handle);

                disposedValue = true;
            }
        }

        public Shader(byte[] vertexcode, byte[] fragmentcode) : this(Encoding.UTF8.GetString(vertexcode), Encoding.Default.GetString(fragmentcode)) { }

        public Shader(string vertexcode, string fragmentcode) {
            vertexcode = RemoveUtf8Bom(vertexcode);
            fragmentcode = RemoveUtf8Bom(fragmentcode);

            int VertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(VertexShader, vertexcode);

            int FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(FragmentShader, fragmentcode);




            GL.CompileShader(VertexShader);
            GL.GetShader(VertexShader, ShaderParameter.CompileStatus, out int vert_success);
            if(vert_success == 0) {
                string infoLog = GL.GetShaderInfoLog(VertexShader);
                Console.WriteLine(infoLog);
            }

            GL.CompileShader(FragmentShader);
            GL.GetShader(FragmentShader, ShaderParameter.CompileStatus, out int frag_success);
            if(frag_success == 0) {
                string infoLog = GL.GetShaderInfoLog(FragmentShader);
                Console.WriteLine(infoLog);
            }



            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, VertexShader);
            GL.AttachShader(Handle, FragmentShader);
            GL.LinkProgram(Handle);
            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int success);
            if(success == 0) {
                string infoLog = GL.GetProgramInfoLog(Handle);
                Console.WriteLine(infoLog);
            }


            GL.DetachShader(Handle, VertexShader);
            GL.DetachShader(Handle, FragmentShader);
            GL.DeleteShader(FragmentShader);
            GL.DeleteShader(VertexShader);
        }





        static string RemoveUtf8Bom(string text) {
            if(!string.IsNullOrEmpty(text) && text[0] == '\uFEFF') // U+FEFF = BOM
            {
                return text.Substring(1); // Remove first character
            }
            return text;
        }



        public static int SetUpRectVAO() {
            int VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            float[] vertices = {
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
               -1.0f,  1.0f, 0.0f,
               -1.0f, -1.0f, 0.0f,
            };
            uint[] indices = { 3, 0, 1, 3, 2, 1 };

            int VBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            int IBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, IBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            return VAO;
        }
    }
}
