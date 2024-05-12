using Mcasaenk.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Xsl;


namespace Mcasaenk.UI.Canvas {
    /// <summary>
    /// Interaction logic for CanvasControl.xaml
    /// </summary>
    public partial class CanvasControl : UserControl {

        WorldPosition screen;

        List<Painter> painters;
        ScenePainter scenePainter;
        GridPainter2 gridPainter;
        BackgroundPainter backgroundPainter;

        Timer secondaryTimer;

        public CanvasControl() {
            InitializeComponent();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            screen = new WorldPosition(new Point(0, 0), (int)ActualWidth, (int)ActualHeight, 1);

            scenePainter = new ScenePainter();
            gridPainter = new GridPainter2();
            backgroundPainter = new BackgroundPainter();
            painters = [
                backgroundPainter,
                scenePainter,
                gridPainter,
            ];
        
            this.SizeChanged += OnSizeChange;
            this.MouseWheel += OnMouseWheel;
            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += (a, e) => OnMouseMove(e.GetPosition(this));
            this.MouseLeave += OnMouseLeave;


            CompositionTarget.Rendering += OnFastTick;
        }

        TileMap tileMap { get => Global.App.TileMap; }
        MainWindow window { get => Global.App.Window; }
        public void OnTilemapChanged() { 
            scenePainter.SetTileMap(tileMap);

            secondaryTimer = new Timer(OnSlowTick, null, 0_500, 0_50);
        }

        protected override void OnRender(DrawingContext drawingContext) {
            base.OnRender(drawingContext);

            foreach(var painter in painters) {
                drawingContext.DrawDrawing(painter.GetDrawing());
            }
        }


        double tick_lastElapsed = 0, tick_accumulation = 0;
        int tick_count = 0;
        private void OnFastTick(object sender, EventArgs e) {
            foreach(var painter in painters) {
                painter.Update(screen);
            }

            { // footer update
                double elapsed = ((RenderingEventArgs)e).RenderingTime.TotalMilliseconds;
                double t = elapsed - tick_lastElapsed;
                tick_accumulation += t;
                tick_lastElapsed = elapsed;
                tick_count++;
                if(tick_accumulation > 1000) {
                    window.footer.Fps = tick_count;
                    tick_accumulation = 0;
                    tick_count = 0;
                }
                window.footer.Region = screen.GetTilePos(mousePos);
            }
        }

        private void OnSlowTick(object a) {
            if(tileMap == null) return;
            foreach(var pos in screen.GetVisibleTilePositions().Shuffle()) {
                var tile = tileMap.GetTile(pos);
                if(tile == null) continue;
                if(tileMap.generateTilePool.ShouldDo(tile)) {
                    tile.QueueGenerate(screen);
                }
                if(tileMap.drawTilePool.ShouldDo(tile)) {
                    tile.QueueDraw();
                }
            }

            Dispatcher.BeginInvoke(new Action(() => {
                if(window == null) return;
                if(window.footer == null) return;
                { // footer update
                    window.footer.RegionQueue = tileMap.generateTilePool.GetLoadingQueue();
                    window.footer.HardDraw_Raw = $"{(TileDraw.drawTime / TileDraw.drawCount)} / {(GenerateTilePool.redrawAcc / GenerateTilePool.redrawCount)}";
                    window.footer.ShadeTiles = tileMap.ShadeTiles();
                    window.footer.ShadeFrames = tileMap.ShadeFrames();
                }
            }));
        }






        #region INPUT
        public Point GetRelativeMouse(Point screenMouse) {
            Point pos;
            Dispatcher.Invoke(() => {
                pos = this.TranslatePoint(window.PointFromScreen(screenMouse), this);
            });
            return pos;
        }

        private Point mousePos = default, mouseStart = default;
        public bool mousedown = false;
        public void OnMouseDown(object sender, MouseButtonEventArgs e) {
            switch(e.ChangedButton) {
                case MouseButton.Left:
                    break;
                case MouseButton.Middle:
                    mouseStart = screen.GetGlobalPos(e.GetPosition(this));
                    mousedown = true;
                    break;
                case MouseButton.Right:
                    Global.App.Settings.LAND_BLEND = 1;
                    break;
                default: break;
            }
            
        }
        public void OnMouseUp(object sender, MouseButtonEventArgs e) {
            switch(e?.ChangedButton) {
                case null:
                    break;
                case MouseButton.Left:
                    tileMap?.GetTile(screen.GetTilePos(mousePos))?.QueueDraw();
                    break;
                case MouseButton.Middle:
                    mouseStart = default;
                    mousedown = false;
                    break;
                case MouseButton.Right:
                    GC.Collect(2, GCCollectionMode.Aggressive);
                    break;
                default: break;
            }
        }
        public void OnMouseMove(Point point) {
            mousePos = point;
            if(mousedown) {             
                screen.SetStart(mouseStart.Sub(mousePos.Dev(screen.zoom)));
                //OnFastTick(null, null);
            }
        }
        public void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            int delta = e.Delta > 0 ? 1 : -1;
            if(screen.ZoomScale + delta < Global.App.Settings.MINZOOM || screen.ZoomScale + delta > Global.App.Settings.MAXZOOM) return;

            Point mouseRel = e.GetPosition((IInputElement)sender);
            Point mouseGl = screen.GetGlobalPos(mouseRel);

            screen.ZoomScale += delta;
            screen.SetStart(mouseGl.Sub(mouseRel.Dev(screen.zoom)));

            //OnFastTick(null, null);
        }
        private void OnSizeChange(object sender, SizeChangedEventArgs e) {
            screen.ScreenWidth = (int)this.ActualWidth + 1;
            screen.ScreenHeight = (int)this.ActualHeight + 1;
            //OnFastTick(null, null);
        }
        private void OnMouseLeave(object sender, MouseEventArgs e) {
            //mousePos = default;
            //mouseStart = default;
            //origScreenPos = default;
        }
        #endregion
    }
}
