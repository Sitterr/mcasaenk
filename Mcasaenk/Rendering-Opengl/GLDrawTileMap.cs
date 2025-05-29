using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mcasaenk.Nbt;
using Mcasaenk.Rendering;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;

namespace Mcasaenk.Rendering_Opengl {
    public class OpenGLDrawTileMap : DrawGroupTileMap<int> {
        private readonly DissectShader dissectShader;
        private readonly ShaderPipeline gldrawer;
        public readonly int emptyTile;

        public OpenGLDrawTileMap(GenDataTileMap gentilemap, ShaderPipeline gldrawer, DissectShader dissectShader, double bundlebr, double scale, OpenGLDrawTileMap oldTileMap) : base(gentilemap, bundlebr, Math.Min(scale, 1), false, oldTileMap) {
            this.gldrawer = gldrawer;
            this.dissectShader = dissectShader;

            this.emptyTile = CreateTile();

            {
                thebigtexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, thebigtexture);
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 500, 500, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                //GL.BindTexture(TextureTarget.Texture2D, thebigtexture);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            }
        }

        HashSet<Point2i> todo = new();
        protected override int __Do(Point2i p, int texture) {
            todo.Add(p);
            // actual work in DoVisible();
            return texture;
        }

        public override void OnScaleChange(double scale) {
            base.OnScaleChange(scale);
            this.Reset(this.gentilemap);
        }

        public override void DoVisible(KeyValuePair<string, WorldPosition> visiblescreen, KeyValuePair<string, WorldPosition>[] movingextras, bool quickscan) {
            todo.Clear();
            base.DoVisible(visiblescreen, movingextras, quickscan);

            if(todo.Count > 0) {
                Point2i min = new Point2i(int.MaxValue, int.MaxValue), max = new Point2i(int.MinValue, int.MinValue);
                foreach(var t in todo) {
                    min.X = Math.Min(min.X, t.X);
                    min.Z = Math.Min(min.Z, t.Z);

                    max.X = Math.Max(max.X, t.X);
                    max.Z = Math.Max(max.Z, t.Z);
                }

                Point2i bigsize = (max - min + 1) * TileSizeR;
                ResizeTheBigTexture(bigsize);
                gldrawer.Render(new WorldPosition(new System.Windows.Point(min.X * TileSize, min.Z * TileSize), bigsize.X / scale, bigsize.Z / scale, scale), gentilemap, Global.App.Colormap, extras.GetValueOrDefault("map_screenshot", (WorldPosition)default), thebigtexture);

                dissectShader.Use(thebigtexture, todo.Select(t => (t - min, GetTile(t))), new Point2i(TileSizeR, TileSizeR), bigsize);
            }
        }

        private int thebigtexture;
        private Point2i thebigtexture_size = new Point2i(0, 0);
        private void ResizeTheBigTexture(Point2i size) {
            if(size.X > thebigtexture_size.X || size.Z > thebigtexture_size.Z) {
                GL.DeleteTexture(thebigtexture);
                thebigtexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, thebigtexture);
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 500, 500, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, size.X, size.Z);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

                thebigtexture_size = size;
            }
        }

        protected override int CreateTile() {
            int texture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, texture);

            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, TileSizeR, TileSizeR);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);


            //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, TileSizeR, TileSizeR, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

            return texture;
        }

        protected override void DisposeTile(int tile) {
            if(tile != 0) GL.DeleteTexture(tile);
        }
        public override void Dispose() {
            if(!disposed) {
                disposed = true;
                base.Dispose();
                DisposeTile(emptyTile);
                GL.DeleteTexture(thebigtexture);
            }
        }
        bool disposed = false;

    }
    public class OpenGLScreenshotTaker : ScreenshotTaker, IDisposable {
        private readonly GenDataTileMap gentilemap;
        private readonly ShaderPipeline renderer;
        private readonly WorldPosition frame;
        private readonly bool rotate;

        private readonly ShaderTexture2D sceneimage;
        public OpenGLScreenshotTaker(GenDataTileMap gentilemap, ShaderPipeline renderer, WorldPosition frame, bool rotate) {
            this.gentilemap = gentilemap;
            this.renderer = renderer;
            this.frame = frame;
            this.rotate = rotate;

            sceneimage = ShaderTexture2D.CreateRGBA8_Single((int)(frame.Width * frame.InSimZoom), (int)(frame.Height * frame.InSimZoom));
        }
        public void Dispose() {
            sceneimage.Dispose();
        }

        public BitmapSource TakeScreenshotAsImage() {
            int w = frame.ScreenWidth, h = frame.ScreenHeight;
            if(rotate) (w, h) = (h, w);
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, Render(false), w * 4);
        }

        public CompoundTag_Allgemein TakeScreenshotAsMap(Dimension dim, int version, ColorApproximationAlgorithm coloralgo) {
            return NBTBlueprints.CreateMapScreenshot(MemoryMarshal.Cast<byte, uint>(Render(true)), frame, dim, version, coloralgo);
        }
        private byte[] Render(bool map) {
            renderer.Render(frame, gentilemap, Global.App.Colormap, map ? frame : default, sceneimage.textureHandle);

            byte[] data = sceneimage.ReadData();
            if(frame.OutSimzoom > 1) data = ScaleUpRaw32bit(data, (int)frame.Width, (int)frame.Height, (int)frame.OutSimzoom);
            FlipVert(data, frame.ScreenWidth, frame.ScreenHeight);
            RgbaToBgra(data);
            if(rotate) data = RotateMinus90(data, frame.ScreenWidth, frame.ScreenHeight);
            return data;
        }

        static void FlipVert(byte[] data, int width, int height) {
            const int channels = 4; // For RGBA/BGRA
            int rowSize = width * channels;

            for(int y = 0; y < height / 2; y++) {
                int topIndex = y * rowSize;
                int bottomIndex = (height - 1 - y) * rowSize;

                for(int i = 0; i < rowSize; i++) {
                    byte temp = data[topIndex + i];
                    data[topIndex + i] = data[bottomIndex + i];
                    data[bottomIndex + i] = temp;
                }
            }
        }
        static void RgbaToBgra(byte[] data) {
            for(int i = 0; i < data.Length; i += 4) {
                byte r = data[i];
                byte b = data[i + 2];

                data[i] = b;       // B
                data[i + 2] = r;   // R
                                   // G and A stay in place
            }
        }
        static byte[] ScaleUpRaw32bit(byte[] srcPixels, int width, int height, int scale) {
            int bytesPerPixel = 4;
            int srcStride = width * bytesPerPixel;
            int newWidth = width * scale;
            int newHeight = height * scale;
            int destStride = newWidth * bytesPerPixel;

            byte[] destPixels = new byte[newWidth * newHeight * bytesPerPixel];

            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    int srcIndex = y * srcStride + x * bytesPerPixel;

                    // Copy this pixel into a scale x scale block
                    for(int dy = 0; dy < scale; dy++) {
                        for(int dx = 0; dx < scale; dx++) {
                            int destX = x * scale + dx;
                            int destY = y * scale + dy;
                            int destIndex = destY * destStride + destX * bytesPerPixel;

                            System.Buffer.BlockCopy(srcPixels, srcIndex, destPixels, destIndex, bytesPerPixel);
                        }
                    }
                }
            }

            return destPixels;
        }

        static byte[] RotateMinus90(byte[] input, int width, int height) {
            int bytesPerPixel = 4;
            byte[] output = new byte[input.Length];

            for(int y = 0; y < height; y++) {
                for(int x = 0; x < width; x++) {
                    int srcIndex = (y * width + x) * bytesPerPixel;

                    int dstX = y;
                    int dstY = width - 1 - x;
                    int dstIndex = (dstY * height + dstX) * bytesPerPixel;

                    for(int b = 0; b < bytesPerPixel; b++) {
                        output[dstIndex + b] = input[srcIndex + b];
                    }
                }
            }

            return output;
        }
    }
}
