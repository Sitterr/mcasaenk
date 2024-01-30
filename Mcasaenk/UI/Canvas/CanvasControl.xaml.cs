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

        List<Painter> painters;
        Painter scenePainter, gridPainter, backgroundPainter;

        public CanvasControl() {
            InitializeComponent();


            this.SizeChanged += OnSizeChange;
            this.MouseWheel += OnMouseWheel;
            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += OnMouseMove;

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            screen = new WorldPosition() { zoom = 1, position = new Point2f(0, 0), w = (int)ActualWidth, h = (int)ActualWidth };
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
            secondaryTimer.Interval = TimeSpan.FromMilliseconds(100);
            secondaryTimer.Start();
        }

        private FooterInterface footer;
        public void Init(FooterInterface footer) {
            this.footer = footer;
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
            double elapsed = ((RenderingEventArgs)e).RenderingTime.TotalMilliseconds;
            tick_accumulation += (elapsed - tick_lastElapsed);
            tick_lastElapsed = elapsed;
            tick_count++;
            if(tick_accumulation > 1000) {
                footer.Fps = tick_count;
                tick_accumulation = 0;
                tick_count = 0;
            }

            foreach(var painter in painters) {
                painter.Update(screen);
            }
        }

        private void OnSlowTick(object sender, EventArgs e) {
            Debug.WriteLine("slow");

            foreach(var tile in screen.GetVisibleTiles()) {
                if(tile.Loaded == false && tile.Loading == false) {
                    tile.Load();
                }
            }

            var scheduler = TileMap.loading_pool;
            footer.RegionQueue = scheduler.TaskCount();
        }


        #region INPUT
        private Point2f startDrag = default, startPos = default;
        public void OnMouseDown(object sender, MouseButtonEventArgs e) {
            if(e.ChangedButton == MouseButton.Middle) {
                startDrag = new Point2f(e.GetPosition(this));
                startPos = screen.position;
            }
        }
        public void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if(e.ChangedButton == MouseButton.Middle) {
                startDrag = default;
                startPos = default;
            } else if(e.ChangedButton == MouseButton.Left) {
                TileMap.GetTile(screen.GetRelativeTile(new Point2f(e.GetPosition(this))), screen).Load();
            }
        }
        public void OnMouseMove(object sender, MouseEventArgs e) {
            if(startDrag != default) {
                Point2f currDrag = new Point2f(e.GetPosition(this));
                screen.position = startPos + (startDrag - currDrag) / screen.zoom;
                //Render();
            }
        }
        public void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            int delta = e.Delta > 0 ? 1 : -1;
            if(screen.GetZoomScale() + delta < Settings.MINZOOM || screen.GetZoomScale() + delta > Settings.MAXZOOM) return;

            Point2f newCenter;
            if(delta == 1) {
                newCenter = screen.position + new Point2f(e.GetPosition((IInputElement)sender)) / screen.zoom;
            } else {
                newCenter = screen.position + new Point2f(screen.w / 2, screen.h / 2) / screen.zoom;
            }
            screen.zoom *= (float)Math.Pow(2, delta);
            screen.position = newCenter - new Point2f(screen.w / 2, screen.h / 2) / screen.zoom;
            startPos = screen.position;

            //Render();
        }
        private void OnSizeChange(object sender, SizeChangedEventArgs e) {
            screen.w = (int)this.ActualWidth; screen.h = (int)this.ActualHeight;
            //Render();
        }
        #endregion
    }
}
