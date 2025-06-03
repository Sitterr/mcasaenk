using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Mcasaenk.Rendering;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;

namespace Mcasaenk.Rendering_Opengl {
    public class GLCanvasCoordinator : CanvasCoordinator {
        const double DRAWZOOM = 1;

        public ShaderPipeline pipeline;
        ScaleShader scaleShader;
        DissectShader dissectShader;

        private GLWpfControl canvas;
        public GLCanvasCoordinator(GLWpfControl canvas, WorldPosition lastpos) : base(canvas, Global.App.Window, 50, lastpos) {
            this.canvas = canvas;
        }
        protected override (double dpix, double dpiy) GetDpiScale() => (PresentationSource.FromVisual(canvas).CompositionTarget.TransformToDevice.M11, PresentationSource.FromVisual(canvas).CompositionTarget.TransformToDevice.M22);

        protected override void OnLoaded() {
            base.OnLoaded();

            int VAO = Shader.SetUpRectVAO();
            pipeline = new ShaderPipeline(VAO);
            scaleShader = new ScaleShader(VAO);
            dissectShader = new DissectShader(VAO);

            canvas.Render += Canvas_Render;

            drawTileMap = CreateGroupTileMap();
        }

        protected override void OnUnloaded() {
            base.OnUnloaded();

            pipeline.Dispose();
            scaleShader.Dispose();
            dissectShader.Dispose();

            foreach(var tex in ShaderArray.AllInstances) {
                tex.Dispose();
            }
            ShaderArray.AllInstances.Clear();

            canvas.Render -= Canvas_Render;
            canvas.Dispose();
        }

        private void Canvas_Render(TimeSpan elapsedTime) {
            bool slowtick = base.OnFastTick(elapsedTime.Milliseconds);

            scaleShader?.Use(screen, (OpenGLDrawTileMap)drawTileMap, genTileMap, window.screenshot);
        }

        public override ScreenshotTaker CreateScreenshotCamera(ScreenshotManager screenshot) => new OpenGLScreenshotTaker(genTileMap, pipeline, screenshot.AsScreen(), screenshot.IsRotated());
        protected override DrawGroupTileMap<int> CreateGroupTileMap() => new OpenGLDrawTileMap(genTileMap, pipeline, dissectShader, DRAWZOOM, screen.zoom, (OpenGLDrawTileMap)drawTileMap);
    }
}
