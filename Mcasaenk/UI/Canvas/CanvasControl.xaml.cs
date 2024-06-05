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
        ScreenshotManager screenshotManager;

        List<Painter> painters;
        ScenePainter scenePainter;
        ScreenshotPainer screenshotPainer;
        GridPainter2 gridPainter;
        BackgroundPainter backgroundPainter;

        DispatcherTimer secondaryTimer;

        public CanvasControl() {
            InitializeComponent();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            screen = new WorldPosition(new Point(0, 0), (int)ActualWidth, (int)ActualHeight, 1);

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
        
            this.SizeChanged += OnSizeChange;
            this.MouseWheel += OnMouseWheel;
            this.MouseDown += OnMouseDown;
            this.MouseUp += OnMouseUp;
            this.MouseMove += (a, e) => OnMouseMove(e.GetPosition(this));
            this.MouseLeave += OnMouseLeave;

            this.KeyDown += OnKeyDown;

            secondaryTimer = new DispatcherTimer(new TimeSpan(0_50 * TimeSpan.TicksPerMicrosecond), DispatcherPriority.Background, OnSlowTick, this.Dispatcher);
            CompositionTarget.Rendering += OnFastTick;
        }

        TileMap tileMap { get => Global.App.TileMap; }
        MainWindow window { get => Global.App.Window; }
        public void OnTilemapChanged() { 
            scenePainter.SetTileMap(tileMap);
            screenshotManager = null;
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

            //{ // footer update
            //    double elapsed = ((RenderingEventArgs)e).RenderingTime.TotalMilliseconds;
            //    double t = elapsed - tick_lastElapsed;
            //    tick_accumulation += t;
            //    tick_lastElapsed = elapsed;
            //    tick_count++;
            //    if(tick_accumulation > 1000) {
            //        window.footer.Fps = tick_count;
            //        tick_accumulation = 0;
            //        tick_count = 0;
            //    }
            //    window.footer.Region = screen.GetRegionPos(mousePos);
            //}
        }

        private void OnSlowTick(object a, object b) {
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

            //if(window.footer != null && false) {
            //    { // footer update
            //        window.footer.RegionQueue = tileMap.generateTilePool.GetLoadingQueue();
            //        window.footer.HardDraw_Raw = $"{(TileDraw.drawTime / TileDraw.drawCount)} / {(GenerateTilePool.redrawAcc / GenerateTilePool.redrawCount)}";
            //        window.footer.ShadeTiles = tileMap.ShadeTiles();
            //        window.footer.ShadeFrames = tileMap.ShadeFrames();

            //        window.footer.Biome = tileMap?.GetTile(screen.GetRegionPos(mousePos))?.genData?.biomeIds(screen.GetRelBlockPos(mousePos).ToRegionInt()).ToString();
            //    }
            //}
        }

        void UpdateUILocation() {
            var mid = screen.Mid;
            window.loc_x.Text = ((int)mid.X).ToString();
            window.loc_z.Text = ((int)mid.Y).ToString();

            Resolution.frame.X = (int)Math.Ceiling(screen.Width) + 1;
            Resolution.frame.Y = (int)Math.Ceiling(screen.Height) + 1;
        }
        public void SetUpScreenShot(Resolution res, bool canresize) {
            screenshotManager = res != null ? new ScreenshotManager(tileMap, res, canresize, screen.Mid.Floor().Sub(new Point(res.X / 2, res.Y / 2).Floor())) : null;
            screenshotPainer.SetManager(screenshotManager);
            //this.InvalidateVisual();
        }
        public ScreenshotManager ScreenshotManager { get {  return screenshotManager; } }
        public void GoTo(Point p) {
            screen.Mid = p;
        }
        public Size ScreenSize() {
            return new Size(screen.Width, screen.Height);
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
        public bool mousedown = false, mousemiddle, mousescreenshot = false;
        public Cursor mousedownCursor;
        public void OnMouseDown(object sender, MouseButtonEventArgs e) {
            switch(e.ChangedButton) {
                case MouseButton.Left:
                    mouseStart = screen.GetGlobalPos(mousePos);
                    mousedown = true;
                    mousedownCursor = Mouse.OverrideCursor;
                    Mouse.OverrideCursor = Cursors.Hand;
                    break;
                case MouseButton.Middle:
                    mousemiddle = true;
                    goto case MouseButton.Left;
                case MouseButton.Right:
                    break;
                default: break;
            }
            this.Focus();
        }
        public void OnMouseUp(object sender, MouseButtonEventArgs e) {
            switch(e?.ChangedButton) {
                case MouseButton.Left:
                    mouseStart = default;
                    mousedown = false;
                    Mouse.OverrideCursor = null;
                    break;
                case MouseButton.Middle:
                    mousemiddle = false;
                    goto case MouseButton.Left;
                case MouseButton.Right:
                    GC.Collect(2, GCCollectionMode.Aggressive);
                    //screenshotManager.Rotate();
                    break;
                default: break;
            }
        }
        public void OnMouseMove(Point point) {
            mousePos = point;
            if(mousedown) {
                if(mousemiddle || !mousescreenshot) {
                    screen.Start = mouseStart.Sub(mousePos.Dev(screen.zoom));
                } else {
                    var globalMousePos = screen.GetGlobalPos(mousePos).Floor();

                    mouseStart = mouseStart.Floor();

                    if(mousedownCursor == Cursors.Cross) {
                        screenshotManager.Location = screenshotManager.Location.Add(globalMousePos.Sub(mouseStart));
                    } else if(mousedownCursor == Cursors.ScrollNW) {
                        var offset = mouseStart.Sub(globalMousePos);
                        screenshotManager.Resolution.X += (int)offset.X;
                        screenshotManager.Resolution.Y += (int)offset.Y;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(offset.X, offset.Y));
                    } else if(mousedownCursor == Cursors.ScrollNE) {
                        var offset = mouseStart.Sub(globalMousePos);
                        offset = new Point(-offset.X, offset.Y);
                        screenshotManager.Resolution.X += (int)offset.X;
                        screenshotManager.Resolution.Y += (int)offset.Y;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(0, offset.Y));
                    } else if(mousedownCursor == Cursors.ScrollSW) {
                        var offset = mouseStart.Sub(globalMousePos);
                        offset = new Point(offset.X, -offset.Y);
                        screenshotManager.Resolution.X += (int)offset.X;
                        screenshotManager.Resolution.Y += (int)offset.Y;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(offset.X, 0));
                    } else if(mousedownCursor == Cursors.ScrollSE) {
                        var offset = mouseStart.Sub(globalMousePos);
                        offset = new Point(-offset.X, -offset.Y);
                        screenshotManager.Resolution.X += (int)offset.X;
                        screenshotManager.Resolution.Y += (int)offset.Y;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(0, 0));
                    } else if(mousedownCursor == Cursors.ScrollN) {
                        var offset = mouseStart.Sub(globalMousePos);
                        screenshotManager.Resolution.Y += (int)offset.Y;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(0, offset.Y));
                    } else if(mousedownCursor == Cursors.ScrollS) {
                        var offset = mouseStart.Sub(globalMousePos);
                        offset = new Point(0, -offset.Y);
                        screenshotManager.Resolution.Y += (int)offset.Y;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(0, 0));
                    } else if(mousedownCursor == Cursors.ScrollW) {
                        var offset = mouseStart.Sub(globalMousePos);
                        offset = new Point(offset.X, 0);
                        screenshotManager.Resolution.X += (int)offset.X;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(offset.X, 0));
                    } else if(mousedownCursor == Cursors.ScrollE) {
                        var offset = mouseStart.Sub(globalMousePos);
                        offset = new Point(-offset.X, 0);
                        screenshotManager.Resolution.X += (int)offset.X;
                        screenshotManager.Location = screenshotManager.Location.Sub(new Point(0, 0));
                    }

                    mouseStart = globalMousePos;

                }


                UpdateUILocation();
            } else {
                Cursor newcursor = null;
                mousescreenshot = false;

                if(screenshotManager != null) {
                    newcursor = screenshotManager.MouseOverWhat(screen, mousePos);

                    
                    if(newcursor != null) {
                        mousescreenshot = true;
                    }
                }

                if(newcursor != Mouse.OverrideCursor) Mouse.OverrideCursor = newcursor;
            }
        }
        public void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            int delta = e.Delta > 0 ? 1 : -1;
            if(screen.ZoomScale + delta < Global.App.Settings.MINZOOM || screen.ZoomScale + delta > Global.App.Settings.MAXZOOM) return;

            Point mouseRel = e.GetPosition((IInputElement)sender);
            Point mouseGl = screen.GetGlobalPos(mouseRel);

            screen.ZoomScale += delta;
            screen.Start = mouseGl.Sub(mouseRel.Dev(screen.zoom));
            UpdateUILocation();
        }
        private void OnSizeChange(object sender, SizeChangedEventArgs e) {
            screen.ScreenWidth = (int)this.ActualWidth + 1;
            screen.ScreenHeight = (int)this.ActualHeight + 1;
            UpdateUILocation();
        }
        private void OnMouseLeave(object sender, MouseEventArgs e) {
            //mousePos = default;
            //mouseStart = default;
            //origScreenPos = default;

            Mouse.OverrideCursor = null;
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if(screenshotManager != null) {
                if(e.Key == Key.D || e.Key == Key.Right) {
                    screenshotManager.Location.X++;
                    e.Handled = true;
                } else if(e.Key == Key.A || e.Key == Key.Left) {
                    screenshotManager.Location.X--;
                    e.Handled = true;
                } else if(e.Key == Key.W || e.Key == Key.Up) {
                    screenshotManager.Location.Y--;
                    e.Handled = true;
                } else if(e.Key == Key.S || e.Key == Key.Down) {
                    screenshotManager.Location.Y++;
                    e.Handled = true;
                }
            }
        }
        #endregion
    }
}
