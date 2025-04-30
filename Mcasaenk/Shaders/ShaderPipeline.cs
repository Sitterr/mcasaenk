using Mcasaenk.Shaders.Kawase;
using Mcasaenk.Shaders.Scale;
using Mcasaenk.Shaders.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Extentions;
using System.Windows;
using Mcasaenk.Colormaping;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk.Shaders {
    public class ShaderPipeline : IDisposable {
        private  PrepKawase prepShader;
        public KawaseShader kawaseShader;
        private SceneShader sceneShader;
        private ScaleShader scaleShader;

        public int VBuffer, IBuffer, VAO;

        public ShaderPipeline(GLWpfControl canvas) {
            var openglsettings = new GLWpfControlSettings {
                MajorVersion = 4,
                MinorVersion = 3
            };
            canvas.Start(openglsettings);
            GL.Enable(EnableCap.DebugOutput);
            GL.DebugMessageCallback((src, type, id, severity, len, msg, user) => {
                string str = Marshal.PtrToStringAnsi(msg);
                if(type == DebugType.DebugTypeError) {
                    Console.WriteLine(str);
                }
            }, IntPtr.Zero);


        }

        public void OnLoad() {

            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            float[] vertices = {
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
               -1.0f,  1.0f, 0.0f,
               -1.0f, -1.0f, 0.0f,
            };
            uint[] indices = { 3, 0, 1, 3, 2, 1 };

            VBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            IBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, IBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            sceneShader = new SceneShader(VAO);
            scaleShader = new ScaleShader(VAO);
            kawaseShader = new KawaseShader(VAO);
            prepShader = new PrepKawase(VAO);

            GL.BindVertexArray(0);

            int maxTextureBufferSize;
            GL.GetInteger(GetPName.MaxTextureBufferSize, out maxTextureBufferSize);

            int maxssbo;
            GL.GetInteger(GetPName.MaxUniformBufferBindings, out maxssbo);

            int maxtextures;
            GL.GetInteger(GetPName.MaxTextureImageUnits, out maxtextures);

            int maxrazshirenia;
            GL.GetInteger(GetPName.MaxDrawBuffers, out maxrazshirenia);
        }

        bool disposed = false;
        public void Dispose() {
            if(!disposed) {
                sceneShader.Dispose();
                scaleShader.Dispose();
                kawaseShader.Dispose();
                prepShader.Dispose();
            }
            disposed = true;
        }


        public void OnRender(WorldPosition screen, TileMap tilemap) {
            int[] kawaseKernels = new int[1 + kawaseShader.blendtints.Length];
            kawaseKernels[0] = Global.Settings.TRANSPARENTLAYERS > 0 ? Global.Settings.OCEAN_DEPTH_BLENDING : 0;

            var blendtints = Global.App.Colormap?.TintManager.GetBlendingTints();
            for(int i = 0; i < kawaseShader.blendtints.Length; i++) {
                var tint = blendtints[i];
                if(tint is DynamicTint dt) kawaseKernels[1 + i] = dt.Blend;
                else kawaseKernels[1 + i] = 0;
            }
            int kawaseReach = (kawaseKernels.Max() - 1) / 2;
            kawaseReach = (int)(kawaseReach * 1.1);


            prepShader.Use(screen, tilemap, kawaseShader.blendtints, kawaseReach);
            var kawase_tex = kawaseShader.Use(screen, tilemap, kawaseKernels, kawaseReach, prepShader.texture1);
            sceneShader.Use(screen, tilemap, kawase_tex, kawaseShader.blendtints, kawaseReach);
            scaleShader.Use(screen, sceneShader.texture);
        }
    }
}
