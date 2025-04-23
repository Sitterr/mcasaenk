using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Shaders {
    public unsafe abstract class ShaderArray : IDisposable {
        public abstract void Dispose();

        public void Data<T>(T[] data) where T : unmanaged {
            fixed(T* p = data) {
                this.DataP((nint)p);
            }
        }

        public abstract void DataP(nint data);
        public abstract void Use(int point);
    }

    public unsafe class ShaderTexture2D : ShaderArray {
        public readonly int l, w, h;
        private readonly int textureHandle;

        private readonly PixelFormat format;
        private readonly PixelType pixelType;
        private readonly int brchannels, channelsize;

        private ShaderTexture2D(int l, int w, int h, SizedInternalFormat preciseformat, PixelFormat format, PixelType pixelType, int brchannels, int channelsize) {
            this.l = l;
            this.w = w;
            this.h = h;

            this.brchannels = brchannels;
            this.channelsize = channelsize;

            this.format = format;
            this.pixelType = pixelType;


            textureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureHandle);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, preciseformat, w, h, l);
        }

        private bool disposed = false;
        public override void Dispose() {
            if(disposed) return;

            GL.DeleteTexture(textureHandle);
            disposed = true;
        }

        public override void DataP(nint p) {
            int stagingBuffer;
            GL.CreateBuffers(1, out stagingBuffer);
            GL.NamedBufferStorage(stagingBuffer, l * w * h * brchannels * channelsize, p, BufferStorageFlags.ClientStorageBit);

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, stagingBuffer);
            GL.BindTexture(TextureTarget.Texture2DArray, textureHandle);
            GL.TextureSubImage3D(textureHandle, 0, 0, 0, 0, w, h, l, format, pixelType, 0);

            GL.DeleteBuffers(1, ref stagingBuffer);
        }

        public override void Use(int point) {
            GL.ActiveTexture((TextureUnit)point);
            GL.BindTexture(TextureTarget.Texture2DArray, textureHandle);
        }



        public static ShaderTexture2D CreateRGBA16i(int l, int w, int h) => new ShaderTexture2D(l, w, h, SizedInternalFormat.Rgba16i, PixelFormat.RgbaInteger, PixelType.Short, 4, sizeof(short));
    }

    //public unsafe class ShaderTexture2D : ShaderArray {
    //    public readonly int l, w, h;
    //    private readonly int textureHandle;

    //    private readonly PixelFormat format;
    //    private readonly PixelType pixelType;
    //    public ShaderTexture2D(int l, int w, int h, SizedInternalFormat preciseformat, PixelFormat format, PixelType pixelType) {
    //        this.l = l;
    //        this.w = w;
    //        this.h = h;

    //        this.format = format;
    //        this.pixelType = pixelType;


    //        textureHandle = GL.GenTexture();
    //        GL.BindTexture(TextureTarget.Texture2DArray, textureHandle);
    //        GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, preciseformat, w, h, l);
    //    }

    //    private bool disposed = false;
    //    public override void Dispose() {
    //        if(disposed) return;

    //        GL.DeleteTexture(textureHandle);
    //        disposed = true;
    //    }

    //    public override void DataP(nint p) {
    //        int stagingBuffer;
    //        GL.CreateBuffers(1, out stagingBuffer);
    //        GL.NamedBufferStorage(stagingBuffer, l * w * h * 4 * sizeof(short), p, BufferStorageFlags.ClientStorageBit);

    //        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, stagingBuffer);
    //        GL.BindTexture(TextureTarget.Texture2DArray, textureHandle);
    //        GL.TextureSubImage3D(textureHandle, 0, 0, 0, 0, w, h, l, format, pixelType, 0);

    //        GL.DeleteBuffers(1, ref stagingBuffer);
    //    }

    //    public override void Use(int point) {
    //        GL.ActiveTexture((TextureUnit)point);
    //        GL.BindTexture(TextureTarget.Texture2DArray, textureHandle);
    //    }
    //}


    public unsafe class ShaderBufferTexture : ShaderArray {
        public readonly int size;
        private readonly int bufferHandle, textureHandle;

        public ShaderBufferTexture(int size, SizedInternalFormat format) {
            this.size = size;

            GL.CreateBuffers(1, out bufferHandle);
            GL.NamedBufferStorage(bufferHandle, (IntPtr)size, 0, BufferStorageFlags.DynamicStorageBit);
            GL.BindBuffer(BufferTarget.TextureBuffer, bufferHandle);

            textureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureBuffer, textureHandle);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, format, bufferHandle);
        }

        private bool disposed = false;
        public override void Dispose() {
            if(disposed) return;

            GL.DeleteTexture(textureHandle);
            GL.DeleteBuffers(1, [bufferHandle]);
            disposed = true;
        }

        public override void DataP(nint p) {
            int stagingBuffer;
            GL.CreateBuffers(1, out stagingBuffer);
            GL.NamedBufferStorage(stagingBuffer, size, p, BufferStorageFlags.ClientStorageBit);
            GL.CopyNamedBufferSubData(stagingBuffer, bufferHandle, 0, 0, size);

            GL.DeleteBuffers(1, [stagingBuffer]);
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

            GL.CreateBuffers(1, out bufferHandle);
            GL.NamedBufferStorage(bufferHandle, (IntPtr)size, 0, BufferStorageFlags.DynamicStorageBit);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, bufferHandle);
        }

        private bool disposed = false;
        public override void Dispose() {
            if(disposed) return;

            GL.DeleteBuffers(1, [bufferHandle]);
            disposed = true;
        }

        public override void DataP(nint p) {
            int stagingBuffer;
            GL.CreateBuffers(1, out stagingBuffer);
            GL.NamedBufferStorage(stagingBuffer, size, p, BufferStorageFlags.ClientStorageBit);
            GL.CopyNamedBufferSubData(stagingBuffer, bufferHandle, 0, 0, size);

            GL.DeleteBuffers(1, [stagingBuffer]);
        }

        public override void Use(int point) {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, point, bufferHandle);
        }
    }
}
