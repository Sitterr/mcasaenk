using OpenTK.Graphics.OpenGL4;

namespace Mcasaenk.Rendering_Opengl {
    public unsafe abstract class ShaderArray : IDisposable {
        public bool disposed { get; protected set; }
        public abstract void Dispose();

        public static readonly List<ShaderArray> AllInstances = new();
        public ShaderArray() {
            AllInstances.Add(this);
        }

        public void Data<T>(T[] data) where T : unmanaged {
            fixed(T* p = data) {
                this.DataP((nint)p, data.Length * sizeof(T));
            }
        }

        public abstract void DataP(nint data, int size = -1);
        public abstract void Use(int point);
    }




    public unsafe class ShaderTexture2D : ShaderArray {
        public readonly int l, w, h;
        public readonly int textureHandle;

        private readonly PixelFormat format;
        private readonly PixelType pixelType;
        private readonly int brchannels, channelsize;

        private readonly TextureTarget type;

        private ShaderTexture2D(TextureTarget type, int l, int w, int h, SizedInternalFormat preciseformat, PixelFormat format, PixelType pixelType, int brchannels, int channelsize) {
            this.l = l;
            this.w = w;
            this.h = h;

            this.brchannels = brchannels;
            this.channelsize = channelsize;

            this.format = format;
            this.pixelType = pixelType;
            this.type = type;

            textureHandle = GL.GenTexture();
            GL.BindTexture(type, textureHandle);
            if(type == TextureTarget.Texture2DArray) GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, preciseformat, w, h, l);
            else GL.TexStorage2D(TextureTarget2d.Texture2D, 1, preciseformat, w, h);
        }

        public override void Dispose() {
            if(disposed) return;

            GL.DeleteTexture(textureHandle);
            disposed = true;
        }

        //public nint uploadSync = -1;
        public override void DataP(nint p, int size = -1) {
            int stagingBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.CopyReadBuffer, stagingBuffer);
            GL.BufferData(BufferTarget.CopyReadBuffer, l * w * h * brchannels * channelsize, p, BufferUsageHint.StreamCopy);

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, stagingBuffer);
            GL.BindTexture(type, textureHandle);
            if(type == TextureTarget.Texture2DArray) GL.TexSubImage3D(type, 0, 0, 0, 0, w, h, l, format, pixelType, 0);
            else GL.TexSubImage2D(type, 0, 0, 0, w, h, format, pixelType, 0);

            GL.DeleteBuffer(stagingBuffer);

            //this.uploadSync = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
        }

        public byte[] ReadData() {
            GL.BindTexture(type, textureHandle);
            byte[] data = new byte[l * w * h * brchannels * channelsize];
            GL.GetTexImage(type, 0, format, pixelType, data);

            return data;
        }

        //public bool HasLoadingCompleted() {
        //    if (uploadSync == -1) return false;
        //    var res = GL.ClientWaitSync(uploadSync, ClientWaitSyncFlags.None, 0);
        //    return res == WaitSyncStatus.AlreadySignaled;
        //}

        public override void Use(int point) {
            GL.ActiveTexture((TextureUnit)point);
            GL.BindTexture(type, textureHandle);
        }



        public static ShaderTexture2D CreateRGBA16i_Array(int l, int w, int h) => new ShaderTexture2D(TextureTarget.Texture2DArray, l, w, h, SizedInternalFormat.Rgba16i, PixelFormat.RgbaInteger, PixelType.Short, 4, sizeof(short));
        public static ShaderTexture2D CreateRGBA8_Single(int w, int h) => new ShaderTexture2D(TextureTarget.Texture2D, 1, w, h, SizedInternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, 4, sizeof(byte));
    }

    public unsafe class ShaderBufferTexture : ShaderArray {
        public readonly int size;
        private readonly int bufferHandle, textureHandle;

        public ShaderBufferTexture(int size, SizedInternalFormat format) {
            this.size = size;

            bufferHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.TextureBuffer, bufferHandle);
            GL.BufferData(BufferTarget.TextureBuffer, (IntPtr)size, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            textureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureBuffer, textureHandle);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, format, bufferHandle);
        }

        public override void Dispose() {
            if(disposed) return;

            GL.DeleteTexture(textureHandle);
            GL.DeleteBuffer(bufferHandle);
            disposed = true;
        }

        public override void DataP(nint p, int size = -1) {
            if(size == -1) size = this.size;
            int stagingBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.CopyReadBuffer, stagingBuffer);
            GL.BufferData(BufferTarget.CopyReadBuffer, size, p, BufferUsageHint.StreamCopy);

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, bufferHandle);
            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, IntPtr.Zero, IntPtr.Zero, size);

            GL.DeleteBuffer(stagingBuffer);
        }

        public override void Use(int point) {
            GL.ActiveTexture((TextureUnit)point);
            GL.BindTexture(TextureTarget.TextureBuffer, textureHandle);
        }
    }


    public unsafe class ShaderSSBO : ShaderArray {
        public readonly int size;
        private readonly int bufferHandle;

        public ShaderSSBO(int size, SizedInternalFormat format) {
            this.size = size;

            bufferHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferHandle);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr)size, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        public override void Dispose() {
            if(disposed) return;

            GL.DeleteBuffer(bufferHandle);
            disposed = true;
        }

        public override void DataP(nint p, int size = -1) {
            if(size == -1) size = this.size;
            int stagingBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.CopyReadBuffer, stagingBuffer);
            GL.BufferData(BufferTarget.CopyReadBuffer, size, p, BufferUsageHint.StreamCopy);

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, bufferHandle);
            GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, IntPtr.Zero, IntPtr.Zero, size);

            GL.DeleteBuffer(stagingBuffer);
        }

        public override void Use(int point) {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, point, bufferHandle);
        }
    }

}
