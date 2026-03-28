using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;

namespace ImvixPro.Services
{
    internal static class MetadataProcessor
    {
        private const int GifPropertyTagFrameDelay = 0x5100;
        private const int GifPropertyTagLoopCount = 0x5101;
        private const short GifPropertyTypeShort = 3;
        private const short GifPropertyTypeLong = 4;

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void CreateAnimatedGifFromFrames(
            List<string> framePaths,
            List<int> durations,
            string destinationPath,
            ushort? loopCount,
            ConversionPauseController? pauseController,
            System.Threading.CancellationToken cancellationToken)
        {
            ThrowIfInterrupted(pauseController, cancellationToken);
            if (framePaths.Count == 0)
            {
                throw new InvalidOperationException("No frames to encode.");
            }

            if (framePaths.Count != durations.Count)
            {
                throw new InvalidOperationException("Frame count and duration count mismatch.");
            }

            var gifEncoder = GetGifEncoder();

            using var firstFrame = new System.Drawing.Bitmap(framePaths[0]);
            ApplyGifFrameMetadata(firstFrame, durations, loopCount);

            using var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
            firstFrame.Save(destinationPath, gifEncoder, encoderParameters);

            encoderParameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            for (var i = 1; i < framePaths.Count; i++)
            {
                ThrowIfInterrupted(pauseController, cancellationToken);
                using var frame = new System.Drawing.Bitmap(framePaths[i]);
                firstFrame.SaveAdd(frame, encoderParameters);
            }

            ThrowIfInterrupted(pauseController, cancellationToken);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
            firstFrame.SaveAdd(encoderParameters);
        }

        private static void ThrowIfInterrupted(ConversionPauseController? pauseController, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pauseController?.WaitIfPaused(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static ImageCodecInfo GetGifEncoder()
        {
            var encoder = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => codec.FormatID == ImageFormat.Gif.Guid);

            if (encoder is null)
            {
                throw new InvalidOperationException("GIF encoder not found.");
            }

            return encoder;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void ApplyGifFrameMetadata(System.Drawing.Image image, List<int> durations, ushort? loopCount)
        {
            var delayBytes = new byte[durations.Count * 4];

            for (var i = 0; i < durations.Count; i++)
            {
                var delay = Math.Max(1, (int)Math.Round(durations[i] / 10d));
                var bytes = BitConverter.GetBytes(delay);
                var offset = i * 4;
                delayBytes[offset] = bytes[0];
                delayBytes[offset + 1] = bytes[1];
                delayBytes[offset + 2] = bytes[2];
                delayBytes[offset + 3] = bytes[3];
            }

            try
            {
                image.SetPropertyItem(CreateGifPropertyItem(GifPropertyTagFrameDelay, GifPropertyTypeLong, delayBytes));
                if (loopCount.HasValue)
                {
                    image.SetPropertyItem(CreateGifPropertyItem(GifPropertyTagLoopCount, GifPropertyTypeShort, BitConverter.GetBytes(loopCount.Value)));
                }
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(MetadataProcessor), "Failed to apply GIF metadata to the output image.", ex);
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static PropertyItem CreateGifPropertyItem(int id, short type, byte[] value)
        {
            var item = CreatePropertyItem();
            item.Id = id;
            item.Type = type;
            item.Len = value.Length;
            item.Value = value;
            return item;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static PropertyItem CreatePropertyItem()
        {
            var item = (PropertyItem?)Activator.CreateInstance(typeof(PropertyItem), nonPublic: true);
            if (item is null)
            {
                throw new InvalidOperationException("Unable to create a PropertyItem instance.");
            }

            return item;
        }
    }
}
