using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public CanvasControl() {
            InitializeComponent();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            screen = new WorldPosition(new Point(0, 0), (int)ActualWidth, (int)ActualHeight, 1);

            scenePainter = new ScenePainter();
            gridPainter = new GridPainter2();
            backgroundPainter = new BackgroundPainter();
            painters = [
                backgroundPainter,
                scenePainter,
                gridPainter,
            ];

            CompositionTarget.Rendering += OnFastTick;

            var secondaryTimer = new DispatcherTimer(DispatcherPriority.Normal, this.Dispatcher);
            secondaryTimer.Tick += OnSlowTick;
            secondaryTimer.Interval = TimeSpan.FromMilliseconds(500);
            secondaryTimer.Start();

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
        }

        protected override void OnRender(DrawingContext drawingContext) {
            base.OnRender(drawingContext);

            foreach(var painter in painters){
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
                window.footer.RegionQueue = Tile.loading_pool.TaskCount();
                window.footer.Region = screen.GetTilePos(mousePos);
            }
        }

        private void OnSlowTick(object sender, EventArgs e) {
            if(tileMap == null) return;
            foreach(var pos in screen.GetVisibleTilePositions()) {
                var tile = tileMap.GetTile(pos, screen);
                if(tile.Loaded == false && tile.Queued == false) {
                    tile.Load();
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
                    //screen.GetTilePos(e.GetPosition(this)).Load();
                    break;
                default: break;
            }
        }
        public void OnMouseMove(Point point) {
            mousePos = point;
            if(mousedown) {             
                screen.SetStart(mouseStart.Sub(mousePos.Dev(screen.zoom)));
                //Render();
            }
        }
        public void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            int delta = e.Delta > 0 ? 1 : -1;
            if(screen.ZoomScale + delta < Settings.MINZOOM || screen.ZoomScale + delta > Settings.MAXZOOM) return;

            Point mouseRel = e.GetPosition((IInputElement)sender);
            Point mouseGl = screen.GetGlobalPos(mouseRel);

            screen.ZoomScale += delta;
            screen.SetStart(mouseGl.Sub(mouseRel.Dev(screen.zoom)));

            mouseStart = screen.coord.TopLeft;
            //Render();
        }
        private void OnSizeChange(object sender, SizeChangedEventArgs e) {
            screen.ScreenWidth = (int)this.ActualWidth;
            screen.ScreenHeight = (int)this.ActualHeight;
            //Render();
        }
        private void OnMouseLeave(object sender, MouseEventArgs e) {
            //mousePos = default;
            //mouseStart = default;
            //origScreenPos = default;
        }
        #endregion
    }
}
