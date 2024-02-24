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
        TileMap tileMap;
        MainWindow window;

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

            screen = new WorldPosition(new Point(0, 0), (int)ActualWidth, (int)ActualHeight, 2);

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
        }
        public void Init(MainWindow window) {
            this.window = window;
        }
        public void SetTileMap(TileMap tileMap) { 
            this.tileMap = tileMap;
            scenePainter.SetTileMap(tileMap);

            tileMap.SetSettings();

            CompositionTarget.Rendering += OnFastTick;
            secondaryTimer = new Timer(OnSlowTick, null, 0_500, 0_100);
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
                tick_accumulation += (elapsed - tick_lastElapsed);
                tick_lastElapsed = elapsed;
                tick_count++;
                if(tick_accumulation > 1000) {
                    window.footer.Fps = tick_count;
                    tick_accumulation = 0;
                    tick_count = 0;
                }
                window.footer.RegionQueue = tileMap.generateTilePool.GetLoadingQueue();
                window.footer.Region = screen.GetTilePos(mousePos);
                window.footer.HardDraw = GenerateTilePool.lastRedrawTime;
                window.footer.ShadeTiles = tileMap.ShadeTiles();
                window.footer.ShadeFrames = tileMap.ShadeFrames();
            }
        }

        private void OnSlowTick(object a) {
            if(tileMap == null) return;
            foreach(var pos in screen.GetVisibleTilePositions()) {
                var tile = tileMap.GetTile(pos);
                if(tile == null) continue;
                if(tileMap.generateTilePool.HasLoaded(tile) == false && tileMap.generateTilePool.IsLoading(tile) == false) {
                    tile.QueueGenerate(screen);
                }
                if(tile.shade.ShouldRedraw()) {
                    tile.QueueShadeUpdate();
                }
            }
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
                case MouseButton.Middle:
                    mouseStart = screen.GetGlobalPos(e.GetPosition(this));
                    mousedown = true;
                    break;
                case MouseButton.Right:
                    break;
                default: break;
            }
            
        }
        public void OnMouseUp(object sender, MouseButtonEventArgs e) {
            switch(e?.ChangedButton) {
                case null:
                case MouseButton.Left:
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
            if(screen.ZoomScale + delta < Settings.MINZOOM || screen.ZoomScale + delta > Settings.MAXZOOM) return;

            Point mouseRel = e.GetPosition((IInputElement)sender);
            Point mouseGl = screen.GetGlobalPos(mouseRel);

            screen.ZoomScale += delta;
            screen.SetStart(mouseGl.Sub(mouseRel.Dev(screen.zoom)));

            //OnFastTick(null, null);
        }
        private void OnSizeChange(object sender, SizeChangedEventArgs e) {
            screen.ScreenWidth = (int)this.ActualWidth;
            screen.ScreenHeight = (int)this.ActualHeight;
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
