// <copyright file="Yuv.cs" company="SIGA Services AG">
// Copyright (c) SIGA Services AG. All rights reserved.
// Adapted from https://github.com/android/camera-samples/blob/master/Camera2Basic/utils/
// </copyright>

namespace HelloAR.YuvTools
{
    using Android.App;
    using Android.Content;
    using Android.Graphics;
    using Android.Media;
    using Android.OS;
    using Android.Runtime;
    using Android.Views;
    using Android.Widget;
    using Java.Nio;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    class YuvByteBuffer
    {
        private ImageFormatType type;
        private ByteBuffer buffer;

        /// <summary>
        /// Gets image yuv type.
        /// </summary>
        public int Type
        {
            get
            {
                return (int)this.type;
            }
        }

        /// <summary>
        /// Gets the size of the buffer.
        /// </summary>
        public int Length
        {
            get
            {
                return this.buffer.Capacity();
            }
        }

        /// <summary>
        /// Copies the buffer bytes into the byte array for processing.
        /// </summary>
        /// <param name="bytes">The destination bytes array.</param>
        public void Get(ref byte[] bytes)
        {
            this.buffer.Get(bytes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YuvByteBuffer"/> class.
        /// </summary>
        /// <param name="image">The raw YUV image.</param>
        /// <param name="dstBuffer">Destination buffer holding the normalized yuv content.</param>
        public YuvByteBuffer(Image image, ref ByteBuffer dstBuffer)
        {
            var wrappedImage = new ImageWrapper(image);

            if (wrappedImage.U.PixelStride == 1)
            {
                this.type = ImageFormatType.Yuv420888;
            }
            else
            {
                this.type = ImageFormatType.Nv21;
            }

            var size = image.Width * image.Height * 3 / 2;
            if (dstBuffer is null ||
                dstBuffer.Capacity() < size ||
                dstBuffer.IsReadOnly ||
                !dstBuffer.IsDirect)
            {
                this.buffer = ByteBuffer.AllocateDirect(size);
                dstBuffer = this.buffer;
            }
            else
            {
                this.buffer = dstBuffer;
            }

            this.buffer.Rewind();
            this.RemovePadding(ref wrappedImage);
        }

        private void RemovePadding(ref ImageWrapper image)
        {
            var sizeLuma = image.Y.Width * image.Y.Height;
            var sizeChroma = image.U.Width * image.U.Height;

            if (image.Y.RowStride > image.Y.Width)
            {
                this.RemovePaddingCompact(ref image.Y, 0);
            }
            else
            {
                this.buffer.Position(0);
                this.buffer.Put(image.Y.Buffer);
            }
            if (this.type == ImageFormatType.Yuv420888)
            {
                if (image.U.RowStride > image.U.Width)
                {
                    this.RemovePaddingCompact(ref image.U, sizeLuma);
                    this.RemovePaddingCompact(ref image.V, sizeLuma + sizeChroma);
                }
                else
                {
                    this.buffer.Position(sizeLuma);
                    this.buffer.Put(image.U.Buffer);
                    this.buffer.Position(sizeLuma + sizeChroma);
                    this.buffer.Put(image.V.Buffer);
                }
            }
            else
            {
                if (image.U.RowStride > image.U.Width * 2)
                {
                    this.RemovePaddingNotCompact(ref image, sizeLuma);
                }
                else
                {
                    this.buffer.Position(sizeLuma);
                    var uv = image.V.Buffer;
                    var properUVSize = (image.V.Height * image.V.RowStride) - 1;
                    if (uv.Capacity() > properUVSize)
                    {
                        uv = this.ClipBuffer(image.V.Buffer, 0, properUVSize);
                    }

                    this.buffer.Put(uv);
                    var lastOne = image.U.Buffer.Get(image.U.Buffer.Capacity() - 1);
                    this.buffer.Put(this.buffer.Capacity() - 1, lastOne);
                }
            }

            this.buffer.Rewind();
        }

        private void RemovePaddingCompact(
            ref PlaneWrapper plane,
            int offset)
        {
            var src = plane.Buffer;
            var rowStride = plane.RowStride;
            ByteBuffer row;
            this.buffer.Position(offset);
            for (int i = 0; i < plane.Height; i++)
            {
                row = this.ClipBuffer(src, i * rowStride, plane.Width);
                this.buffer.Put(row);
            }
        }

        private void RemovePaddingNotCompact(
            ref ImageWrapper image,
            int offset)
        {
            if (image.U.PixelStride != 2)
            {
                throw new ArgumentException("The image passed must not be compact");
            }

            var width = image.U.Width;
            var height = image.U.Height;
            var rowStride = image.U.RowStride;
            ByteBuffer row;
            this.buffer.Position(offset);
            for (int i = 0; i < height - 1; i++)
            {
                row = this.ClipBuffer(image.V.Buffer, i * rowStride, width * 2);
                this.buffer.Put(row);
            }

            row = this.ClipBuffer(image.U.Buffer, ((height - 1) * rowStride) - 1, width * 2);
            this.buffer.Put(row);
        }

        private ByteBuffer ClipBuffer(ByteBuffer buffer, int start, int size)
        {
            var duplicate = buffer.Duplicate();
            duplicate.Position(start);
            duplicate.Limit(start + size);
            return duplicate.Slice();
        }

        private class ImageWrapper
        {
            public int Width;
            public int Height;
            public PlaneWrapper Y;
            public PlaneWrapper U;
            public PlaneWrapper V;

            public ImageWrapper(Image image)
            {
                this.Width = image.Width;
                this.Height = image.Height;
                this.Y = new PlaneWrapper(this.Width, this.Height, image.GetPlanes()[0]);
                this.U = new PlaneWrapper(this.Width / 2, this.Height / 2, image.GetPlanes()[1]);
                this.V = new PlaneWrapper(this.Width / 2, this.Height / 2, image.GetPlanes()[2]);
                if (this.Y.PixelStride != 1)
                {
                    throw new ArgumentException($"Expected a pixel stride of 1 for y " +
                        $"but got {this.Y.PixelStride}");
                }

                if (U.PixelStride != V.PixelStride || U.RowStride != V.RowStride)
                {
                    throw new ArgumentException($"U and V planes must have the same " +
                        $"pixel and row strides\n" +
                        $"But got pixel={this.U.PixelStride} and row={this.U.RowStride} for U\n" +
                        $"and pixel={this.V.PixelStride} and row={this.V.RowStride} for V");
                }
            }
        }

        private class PlaneWrapper
        {
            public PlaneWrapper(int width, int height, Image.Plane plane)
            {
                this.Width = width;
                this.Height = height;
                this.Buffer = plane.Buffer;
                this.RowStride = plane.RowStride;
                this.PixelStride = plane.PixelStride;
            }

            public int Width { get; }
            public int Height { get; }
            public ByteBuffer Buffer { get; }
            public int RowStride { get; }
            public int PixelStride { get; }
        }
    }
}