// <copyright file="YuvToRGBAConverter.cs" company="SIGA Services AG">
// Copyright (c) SIGA Services AG. All rights reserved.
// </copyright>

namespace Swissualize.Droid.YuvTools
{
    using System;
    using Android.Content;
    using Android.Graphics;
    using Android.Media;
    using Android.Renderscripts;
    using Java.Nio;
    using HelloAR.YuvTools;

    class YuvToRGBAConverter
    {
        private RenderScript rs;
        private Context ctxt;
        private ScriptIntrinsicYuvToRGB scriptYuvToRGB;

        private ByteBuffer yuvBits = null;
        private Byte[] bytes;
        private Allocation inputAllocation = null;
        private Allocation outputAllocation = null;

        public YuvToRGBAConverter(Context context)
        {
            this.ctxt = context;
            this.rs = RenderScript.Create(this.ctxt);
            this.scriptYuvToRGB = ScriptIntrinsicYuvToRGB.Create(this.rs, Element.U8_4(this.rs));
        }

        /// <summary>
        /// Converts the input image to the output bitmap.
        /// </summary>
        /// <param name="image">Input android YUV image.</param>
        /// <param name="bitmap">Output android bitmap.</param>
        public void YuvToRGBA(Image image, ref Bitmap bitmap)
        {
            // Initialize buffer and yuvbits
            var yuvBuffer = new YuvByteBuffer(image, ref this.yuvBits);

            if (this.NeedToCreateAllocations(image, yuvBuffer))
            {
                if (this.inputAllocation != null)
                {
                    this.inputAllocation.Destroy();
                }

                if (this.outputAllocation != null)
                {
                    this.outputAllocation.Destroy();
                }

                var yuvType = new Android.Renderscripts.Type.Builder(this.rs, Element.U8(this.rs))
                    .SetX(image.Width)
                    .SetY(image.Height)
                    .SetYuvFormat(yuvBuffer.Type);
                this.inputAllocation = Allocation.CreateTyped(
                    this.rs, yuvType.Create(), AllocationUsage.Script);
                this.bytes = new byte[yuvBuffer.Length];
                var rgbaType = new Android.Renderscripts.Type.Builder(
                    this.rs, Element.RGBA_8888(this.rs))
                    .SetX(image.Width)
                    .SetY(image.Height);

                this.outputAllocation = Allocation.CreateTyped(
                    this.rs, rgbaType.Create(), AllocationUsage.Script);
            }

            yuvBuffer.Get(ref this.bytes);
            this.inputAllocation.CopyFrom(this.bytes);

            this.scriptYuvToRGB.SetInput(this.inputAllocation);
            this.scriptYuvToRGB.ForEach(this.outputAllocation);
            this.outputAllocation.CopyTo(bitmap);
        }

        private bool NeedToCreateAllocations(Image image, YuvByteBuffer yuvBuffer)
        {
            return this.inputAllocation == null || this.inputAllocation.Type.GetX() != image.Width ||
                this.inputAllocation.Type.GetY() != image.Height ||
                this.inputAllocation.Type.Yuv != yuvBuffer.Type ||
                this.bytes.Length != yuvBuffer.Length;
        }
    }
}