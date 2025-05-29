using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Mcasaenk.Rendering;
using Mcasaenk.UI.Canvas;

namespace Mcasaenk.Rendering_bitmap {
    public class WPFCanvas : CanvasCoordinator {
        public class OnRenderFrameworkElement : FrameworkElement {

            public delegate void A_OnRender(DrawingContext context);
            protected override void OnRender(DrawingContext drawingContext) {
                base.OnRender(drawingContext);

                OnDraw.Invoke(drawingContext);
            }

            public event A_OnRender OnDraw;
        }

        List<Painter> painters;
        ScenePainter scenePainter;
        ScreenshotPainer screenshotPainer;
        GridPainter2 gridPainter;
        BackgroundPainter backgroundPainter;

        DispatcherTimer fasttick;
        public WPFCanvas(OnRenderFrameworkElement canvas, WorldPosition lastpos) : base(canvas, Global.App.Window, 50, lastpos) {
            scenePainter = new ScenePainter();
            screenshotPainer = new ScreenshotPainer();
            gridPainter = new GridPainter2();
            backgroundPainter = new BackgroundPainter();
            painters = [
                backgroundPainter,
                scenePainter,
                screenshotPainer,
                gridPainter,
            ];

            canvas.OnDraw += OnRender;

            fasttick = new DispatcherTimer(DispatcherPriority.Send);
            fasttick.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            fasttick.Tick += OnFastTick;
            fasttick.Start();
        }

        protected override void OnUnloaded() {
            base.OnUnloaded();

            ((OnRenderFrameworkElement)canvas).OnDraw -= OnRender;
            fasttick.Tick -= OnFastTick;
            fasttick.Stop();
        }

        protected override (double dpix, double dpiy) GetDpiScale() => (1, 1);

        private void OnRender(DrawingContext context) {
            foreach(var painter in painters) {
                context.DrawDrawing(painter.GetDrawing());
            }
        }

        private DateTime lastm;
        private void OnFastTick(object sender, EventArgs e) {
            bool slowtick = base.OnFastTick((int)((DateTime.Now - lastm).TotalMilliseconds));
            lastm = DateTime.Now;

            if(true) {
                scenePainter.SetTileMap(drawTileMap as BitmapDrawTileMap, genTileMap);
                screenshotPainer.SetManager(drawTileMap as BitmapDrawTileMap, genTileMap, window.screenshot);
            }

            foreach(var painter in painters) {
                painter.Update(screen);
            }
        }

        protected override DrawGroupTileMap CreateGroupTileMap() => new BitmapDrawTileMap(genTileMap, drawTileMap as BitmapDrawTileMap);

        public override ScreenshotTaker CreateScreenshotCamera(ScreenshotManager screenshot) => new BitmapClusterScreenshotTaker(drawTileMap as BitmapDrawTileMap, screenshot.AsScreen(), screenshot.IsRotated());
    }

}
