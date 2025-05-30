using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Mcasaenk.Rendering;
using Mcasaenk.Rendering_Opengl;

namespace Mcasaenk.UI.Canvas {
    public abstract class CanvasCoordinator {
        protected WorldPosition screen;

        protected GenDataTileMap genTileMap;
        protected DrawGroupTileMap drawTileMap;

        protected FrameworkElement canvas;
        protected MainWindow window;

        private int millisecondsReminder;
        public CanvasCoordinator(FrameworkElement canvas, MainWindow window, int millisecondsReminder, WorldPosition lastpos) {
            this.millisecondsReminder = millisecondsReminder;

            this.screen = new WorldPosition(lastpos.Mid, 0, 0, lastpos.zoom);

            this.canvas = canvas;
            this.window = window;
            RenderOptions.SetBitmapScalingMode(canvas, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(canvas, EdgeMode.Aliased);
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            canvas.SizeChanged += OnSizeChange;
            canvas.MouseWheel += OnMouseWheel;
            canvas.MouseDown += (o, e) => OnMouseDown(e.ChangedButton);
            canvas.MouseUp += (o, e) => OnMouseUp(e.ChangedButton);
            canvas.MouseMove += (a, e) => { if(!mousedown || !mousehook) OnMouseMove(e.GetPosition(canvas)); };
            canvas.MouseLeave += OnMouseLeave;

            canvas.KeyDown += OnKeyDown;
            canvas.KeyUp += OnKeyUp;

            canvas.Loaded += (o, e) => OnLoaded();
            canvas.Unloaded += (o, e) => OnUnloaded();

        }
        public WorldPosition GetScreen() => screen;

        new protected bool IsLoaded { get; private set; } = false;

        protected abstract (double dpix, double dpiy) GetDpiScale();
        protected virtual void OnLoaded() {


            (dpix, dpiy) = GetDpiScale();

            if(mousehook) {
                MouseHook.Start();

                MouseHook.MouseEvent += (pos, button) => {
                    switch(button) {
                        case MouseHook.MouseMessages.WM_MOUSEMOVE:
                            if(mousedown) {
                                var off = pos.Add(canvas.PointFromScreen(new Point(0, 0)));
                                pos = off;

                                OnMouseMove(pos);
                            }
                            break;

                        //case MouseHook.MouseMessages.WM_MOUSEWHEEL:
                        //    break;

                        //case MouseHook.MouseMessages.WM_LBUTTONDOWN:
                        //    OnMouseDown(MouseButton.Left);
                        //    break;
                        //case MouseHook.MouseMessages.WM_RBUTTONDOWN:
                        //    OnMouseDown(MouseButton.Right);
                        //    break;
                        //case MouseHook.MouseMessages.WM_MBUTTONDOWN:
                        //    OnMouseDown(MouseButton.Middle);
                        //    break;

                        case MouseHook.MouseMessages.WM_LBUTTONUP:
                            if(mousedown) OnMouseUp(MouseButton.Left);
                            break;
                        case MouseHook.MouseMessages.WM_RBUTTONUP:
                            if(mousedown) OnMouseUp(MouseButton.Right);
                            break;
                        case MouseHook.MouseMessages.WM_MBUTTONUP:
                            if(mousedown) OnMouseUp(MouseButton.Middle);
                            break;
                    }
                };
            }

            IsLoaded = true;
            {
                int w = (int)(canvas.ActualWidth * dpix), h = (int)(canvas.ActualHeight * dpiy);
                screen = new WorldPosition(screen.Start.Add(new Point(-w / 2, -h / 2)), w, h, 1);
            }
            UpdateUILocation();
            canvas.Focus();
        }
        protected virtual void OnUnloaded() {
            if(mousehook) MouseHook.Stop();
            if(drawTileMap is IDisposable i) i.Dispose();
        }
        protected virtual void OnSlowTick() {
            window.footer?.Refresh();

            if(genTileMap != null) {
                genTileMap.DoVisible(new KeyValuePair<string, WorldPosition>("screen", screen));
            }

            if(window.footer.Visibility == Visibility.Visible) {
                { // footer update

                    if(drawTileMap != null) {
                        window.footer.DrawTime = drawTileMap.MeanDoTime();
                    }

                    if(genTileMap != null) {
                        window.footer.GenerateTime = genTileMap.MeanDoTime();

                        window.footer.ShadeTiles = genTileMap.ShadeTiles();
                        window.footer.ShadeFrames = genTileMap.ShadeFrames();
                    }

                    window.footer.SetCursorInfo(new Point2i(screen.GetGlobalPos(mousePos).Floor()), genTileMap);
                }
            }
        }

        private int fps_acc, fps_count;
        private int rem_acc, rem_count;
        protected bool OnFastTick(int elapsedMilliseconds) {
            elapsedMilliseconds = Math.Max(1, elapsedMilliseconds);
            //if(!IsLoaded) return false;
            { // footer update
                fps_acc += elapsedMilliseconds; rem_acc += elapsedMilliseconds;
                fps_count++; rem_count++;
                if(fps_acc > 1000) {
                    window.footer.Fps = fps_count;
                    fps_acc = 0; fps_count = 0;
                }
            }

            bool slowtick = false;
            if(rem_acc > millisecondsReminder) {
                rem_acc = 0; rem_count = 0;
                OnSlowTick();
                slowtick = true;
            }

            if(IsLoaded && genTileMap != null && drawTileMap == null) drawTileMap = CreateGroupTileMap();
            if(drawTileMap != null) {
                WorldPosition map_screenshot = window.screenshot?.ResolutionType() == ResolutionType.map ? window.screenshot.AsScreen() : default;
                drawTileMap.DoVisible(new KeyValuePair<string, WorldPosition>("screen", screen), [new KeyValuePair<string, WorldPosition>("map_screenshot", map_screenshot)], !slowtick);
            }


            return slowtick;
        }

        public virtual void OnTilemapChange(GenDataTileMap tileMap, bool reset) {
            this.genTileMap = tileMap;
            if(reset) {
                screen.Mid = new Point(0, 0);
                UpdateUILocation();
            }

            if(IsLoaded) {
                if(drawTileMap == null) drawTileMap = CreateGroupTileMap();
                else drawTileMap.Reset(tileMap);
            }

            //msg.Visibility = Visibility.Collapsed;
            //if(tileMap != null) {
            //    if(tileMap.Empty()) {
            //        msg.Text = "This dimension is empty";
            //        msg.Visibility = Visibility.Visible;
            //    }
            //}
        }

        public abstract ScreenshotTaker CreateScreenshotCamera(ScreenshotManager screenshot);
        protected abstract DrawGroupTileMap CreateGroupTileMap();


        void UpdateUILocation() {
            var mid = screen.Mid;
            window.loc_x.Text = ((int)mid.X).ToString();
            window.loc_z.Text = ((int)mid.Y).ToString();

            Resolution.frame.X = (int)Math.Ceiling(screen.Width) + 1;
            Resolution.frame.Y = (int)Math.Ceiling(screen.Height) + 1;
        }
        public void GoTo(Point p) {
            screen.Mid = p;
        }
        public Size ScreenSize() {
            return new Size(screen.Width, screen.Height);
        }

        public void DrawMassRedo() { drawTileMap?.MassRedo(); }

        #region INPUT
#if DEBUG
        bool mousehook = false;
#else
        bool mousehook = false;
#endif

        private double dpix, dpiy;

        private Point mousePos = default, mouseStart = default;
        public bool mousein = false, mousedown = false, mousemiddle, mousescreenshot = false;
        protected Cursor mousedownCursor;
        public void OnMouseDown(MouseButton button) {
            switch(button) {
                case MouseButton.Left:
                    mouseStart = screen.GetGlobalPos(mousePos);
                    mousedown = true;
                    mousedownCursor = Mouse.OverrideCursor;

                    if(mousedownCursor == Cursors.ScrollNW || mousedownCursor == Cursors.ScrollN) {
                        window.screenshot.Rebase(true, true);
                    } else if(mousedownCursor == Cursors.ScrollNE || mousedownCursor == Cursors.ScrollE) {
                        window.screenshot.Rebase(true, false);
                    } else if(mousedownCursor == Cursors.ScrollSW || mousedownCursor == Cursors.ScrollW) {
                        window.screenshot.Rebase(false, true);
                    } else if(mousedownCursor == Cursors.ScrollSE || mousedownCursor == Cursors.ScrollS) {
                        window.screenshot.Rebase(false, false);
                    }

                    Mouse.OverrideCursor = Cursors.Hand;
                    break;
                case MouseButton.Middle:
                    mousemiddle = true;
                    goto case MouseButton.Left;
                case MouseButton.Right:
                    break;
                default: break;
            }
            canvas.Focus();
        }
        public void OnMouseUp(MouseButton button) {
            switch(button) {
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
            mousePos = point.Mult(dpix, dpiy);
            if(mousedown) {
                if(mousemiddle || !mousescreenshot) {
                    screen.Start = mouseStart.Sub(mousePos.Dev(screen.zoom));
                } else {
                    var globalMousePos = screen.GetGlobalPos(mousePos).Floor();

                    if(mousedownCursor == Cursors.Cross) {
                        mouseStart = mouseStart.Floor();
                        window.screenshot.Move(globalMousePos.Sub(mouseStart));
                        mouseStart = globalMousePos;
                    } else if(mousedownCursor == Cursors.ScrollNW || mousedownCursor == Cursors.ScrollNE || mousedownCursor == Cursors.ScrollSW || mousedownCursor == Cursors.ScrollSE) {
                        window.screenshot.ResizeCorner(globalMousePos);
                    } else if(mousedownCursor == Cursors.ScrollW || mousedownCursor == Cursors.ScrollE) {
                        window.screenshot.ResizeAxis((int)globalMousePos.X, true);
                    } else if(mousedownCursor == Cursors.ScrollN || mousedownCursor == Cursors.ScrollS) {
                        window.screenshot.ResizeAxis((int)globalMousePos.Y, false);
                    }
                }


                UpdateUILocation();
            } else {
                Cursor newcursor = null;
                mousescreenshot = false;

                if(window.screenshot != null) {
                    newcursor = window.screenshot.MouseOverWhat(screen, mousePos, this is GLCanvasCoordinator);


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

            Point globalMousePos = screen.GetGlobalPos(mousePos);

            screen.ZoomScale += delta;
            screen.Start = globalMousePos.Sub(mousePos.Dev(screen.zoom));

            drawTileMap?.OnScaleChange(screen.zoom);

            UpdateUILocation();
        }
        private void OnSizeChange(object sender, SizeChangedEventArgs e) {
            if(IsLoaded == false) return;
            screen.ScreenWidth = (int)(canvas.ActualWidth * dpix);
            screen.ScreenHeight = (int)(canvas.ActualHeight * dpiy);
            UpdateUILocation();
        }
        private void OnMouseLeave(object sender, MouseEventArgs e) {
            Mouse.OverrideCursor = null;
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if(window.screenshot != null) {
                if(e.Key == Key.D || e.Key == Key.Right) {
                    window.screenshot.Move(new Point(1, 0));
                    e.Handled = true;
                } else if(e.Key == Key.A || e.Key == Key.Left) {
                    window.screenshot.Move(new Point(-1, 0));
                    e.Handled = true;
                } else if(e.Key == Key.W || e.Key == Key.Up) {
                    window.screenshot.Move(new Point(0, -1));
                    e.Handled = true;
                } else if(e.Key == Key.S || e.Key == Key.Down) {
                    window.screenshot.Move(new Point(0, 1));
                    e.Handled = true;
                }
            }
        }


        private void OnKeyUp(object sender, KeyEventArgs e) {
            if(e.Key == Key.F5) {
                Global.App.Reset();
            }
            if(e.Key == Key.F9) {
                Global.Settings.ENABLE_COLORMAP_EDITING = !Global.Settings.ENABLE_COLORMAP_EDITING;
            }
        }
        #endregion
    }
}
