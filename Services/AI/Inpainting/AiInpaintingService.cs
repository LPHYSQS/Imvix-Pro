using Avalonia;
using ImvixPro.Models;
using ImvixPro.Services;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.AI.Inpainting.Inference
{
    public sealed class AiInpaintingService
    {
        private const int DefaultInputSize = 512;

        private static readonly HashSet<string> SupportedInputExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".bmp",
            ".gif",
            ".tif",
            ".tiff"
        };

        private readonly AppLogger _logger;

        public AiInpaintingService(AppLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static bool IsEligible(ImageItemViewModel image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (image.IsPdfDocument || image.IsAnimatedGif)
            {
                return false;
            }

            return SupportedInputExtensions.Contains(Path.GetExtension(image.FilePath));
        }

        public async Task<AiInpaintingResult> ProcessAsync(
            string inputPath,
            SKBitmap maskBitmap,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
            ArgumentNullException.ThrowIfNull(maskBitmap);

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("The source image for AI eraser does not exist.", inputPath);
            }

            var modelPath = RuntimeAssetLocator.AiInpaintingLaMaModelPath;
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The packaged LaMa ONNX model is missing. Ensure 'runtime{Path.DirectorySeparatorChar}ai{Path.DirectorySeparatorChar}inpainting{Path.DirectorySeparatorChar}models{Path.DirectorySeparatorChar}LaMa{Path.DirectorySeparatorChar}lama.onnx' is included next to the application."),
                    modelPath);
            }

            var workingDirectory = Path.Combine(
                Path.GetTempPath(),
                "ImvixPro",
                "Inpainting",
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                return await Task.Run(
                    () => RunInpaintingCore(inputPath, maskBitmap, modelPath, workingDirectory, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryDeleteWorkingDirectory(workingDirectory);
                throw;
            }
        }

        public void TryDeleteWorkingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(nameof(AiInpaintingService), $"Failed to delete AI inpainting working directory '{path}'.", ex);
            }
        }

        private AiInpaintingResult RunInpaintingCore(
            string inputPath,
            SKBitmap maskBitmap,
            string modelPath,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var sourceBitmap = DecodeSourceBitmap(inputPath);
            using var normalizedMaskBitmap = NormalizeMaskBitmap(maskBitmap, sourceBitmap.Width, sourceBitmap.Height);

            if (!HasVisibleMask(normalizedMaskBitmap))
            {
                throw new InvalidOperationException("The AI eraser mask is empty.");
            }

            using var session = CreateSession(modelPath);
            var modelInputSize = ResolveModelInputSize(session);
            using var preparedImageBitmap = ResizeBitmap(sourceBitmap, modelInputSize.Width, modelInputSize.Height);
            using var preparedMaskBitmap = ResizeMaskBitmap(normalizedMaskBitmap, modelInputSize.Width, modelInputSize.Height);

            using var results = RunInference(session, preparedImageBitmap, preparedMaskBitmap, cancellationToken);
            using var inferredBitmap = ExtractOutputBitmap(results);
            using var restoredBitmap = ResizeBitmap(inferredBitmap, sourceBitmap.Width, sourceBitmap.Height);
            using var blendMaskBitmap = CreateBlendMaskBitmap(normalizedMaskBitmap);
            using var mergedBitmap = MergeInpaintingResult(sourceBitmap, restoredBitmap, blendMaskBitmap);

            var resultPath = Path.Combine(workingDirectory, "inpainting-result.png");
            SavePng(mergedBitmap, resultPath);

            return new AiInpaintingResult(
                workingDirectory,
                resultPath,
                new PixelSize(sourceBitmap.Width, sourceBitmap.Height),
                CalculateMaskCoveragePercent(normalizedMaskBitmap));
        }

        private static InferenceSession CreateSession(string modelPath)
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            return new InferenceSession(modelPath, options);
        }

        private static IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunInference(
            InferenceSession session,
            SKBitmap imageBitmap,
            SKBitmap maskBitmap,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.InputMetadata.Count == 1)
            {
                var input = session.InputMetadata.First();
                var combinedTensor = BuildCombinedInputTensor(imageBitmap, maskBitmap);
                return session.Run(
                    [NamedOnnxValue.CreateFromTensor(input.Key, combinedTensor)],
                    session.OutputMetadata.Keys.ToArray());
            }

            var orderedInputs = session.InputMetadata.Keys.ToArray();
            var imageInputName = orderedInputs.FirstOrDefault(static key =>
                                     key.Contains("image", StringComparison.OrdinalIgnoreCase))
                                 ?? orderedInputs[0];
            var maskInputName = orderedInputs.FirstOrDefault(key =>
                                    !string.Equals(key, imageInputName, StringComparison.Ordinal) &&
                                    key.Contains("mask", StringComparison.OrdinalIgnoreCase))
                                ?? orderedInputs.First(key =>
                                    !string.Equals(key, imageInputName, StringComparison.Ordinal));

            var imageTensor = BuildImageTensor(imageBitmap);
            var maskTensor = BuildMaskTensor(maskBitmap);

            return session.Run(
                [
                    NamedOnnxValue.CreateFromTensor(imageInputName, imageTensor),
                    NamedOnnxValue.CreateFromTensor(maskInputName, maskTensor)
                ],
                session.OutputMetadata.Keys.ToArray());
        }

        private static DenseTensor<float> BuildCombinedInputTensor(SKBitmap imageBitmap, SKBitmap maskBitmap)
        {
            var tensor = new DenseTensor<float>([1, 4, imageBitmap.Height, imageBitmap.Width]);
            PopulateImageTensor(tensor, imageBitmap);
            PopulateMaskTensor(tensor, maskBitmap, channelOffset: 3);
            return tensor;
        }

        private static DenseTensor<float> BuildImageTensor(SKBitmap imageBitmap)
        {
            var tensor = new DenseTensor<float>([1, 3, imageBitmap.Height, imageBitmap.Width]);
            PopulateImageTensor(tensor, imageBitmap);
            return tensor;
        }

        private static DenseTensor<float> BuildMaskTensor(SKBitmap maskBitmap)
        {
            var tensor = new DenseTensor<float>([1, 1, maskBitmap.Height, maskBitmap.Width]);
            PopulateMaskTensor(tensor, maskBitmap, channelOffset: 0);
            return tensor;
        }

        private static void PopulateImageTensor(DenseTensor<float> tensor, SKBitmap bitmap)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    tensor[0, 0, y, x] = color.Red / 255f;
                    tensor[0, 1, y, x] = color.Green / 255f;
                    tensor[0, 2, y, x] = color.Blue / 255f;
                }
            }
        }

        private static void PopulateMaskTensor(DenseTensor<float> tensor, SKBitmap bitmap, int channelOffset)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    tensor[0, channelOffset, y, x] = color.Alpha >= 16 || color.Red >= 16 ? 1f : 0f;
                }
            }
        }

        private static PixelSize ResolveModelInputSize(InferenceSession session)
        {
            foreach (var input in session.InputMetadata.Values)
            {
                if (input.Dimensions.Length < 4)
                {
                    continue;
                }

                var height = input.Dimensions[^2];
                var width = input.Dimensions[^1];
                if (height > 0 && width > 0)
                {
                    return new PixelSize(width, height);
                }
            }

            return new PixelSize(DefaultInputSize, DefaultInputSize);
        }

        private static SKBitmap ExtractOutputBitmap(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            ImageTensorCandidate? bestCandidate = null;

            foreach (var result in results)
            {
                if (!TryCreateImageCandidate(result.Value, out var candidate))
                {
                    continue;
                }

                if (bestCandidate is null || candidate.PixelCount > bestCandidate.Value.PixelCount)
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate is null)
            {
                throw new InvalidOperationException("The LaMa ONNX model did not return a compatible image tensor.");
            }

            return CreateRgbBitmap(bestCandidate.Value.Width, bestCandidate.Value.Height, bestCandidate.Value.Values);
        }

        private static bool TryCreateImageCandidate(object? value, out ImageTensorCandidate candidate)
        {
            candidate = default;

            return value switch
            {
                Tensor<float> tensor => TryExtractImageValues(tensor, static raw => raw, out candidate),
                Tensor<double> tensor => TryExtractImageValues(tensor, static raw => (float)raw, out candidate),
                Tensor<byte> tensor => TryExtractImageValues(tensor, static raw => raw, out candidate),
                Tensor<Half> tensor => TryExtractImageValues(tensor, static raw => (float)raw, out candidate),
                _ => false
            };
        }

        private static bool TryExtractImageValues<T>(
            Tensor<T> tensor,
            Func<T, float> converter,
            out ImageTensorCandidate candidate)
        {
            candidate = default;

            var dimensions = tensor.Dimensions.ToArray();
            if (!TryResolveImageShape(dimensions, out var width, out var height, out var channels, out var layout) ||
                channels < 3)
            {
                return false;
            }

            var rawValues = tensor.ToArray();
            var values = new byte[width * height * 3];
            var max = float.MinValue;
            var min = float.MaxValue;
            var usesNormalizedRange = true;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var raw = converter(rawValues[ResolveImageTensorOffset(dimensions, layout, x, y, channel)]);
                        min = Math.Min(min, raw);
                        max = Math.Max(max, raw);
                        if (raw < 0f || raw > 1f)
                        {
                            usesNormalizedRange = false;
                        }

                        values[((y * width) + x) * 3 + channel] = 0;
                    }
                }
            }

            var scale = usesNormalizedRange && max <= 1f && min >= 0f
                ? 255f
                : 1f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var raw = converter(rawValues[ResolveImageTensorOffset(dimensions, layout, x, y, channel)]);
                        var value = raw * scale;
                        values[((y * width) + x) * 3 + channel] = (byte)Math.Clamp(
                            (int)Math.Round(value, MidpointRounding.AwayFromZero),
                            0,
                            255);
                    }
                }
            }

            candidate = new ImageTensorCandidate(width, height, values);
            return true;
        }

        private static bool TryResolveImageShape(
            IReadOnlyList<int> dimensions,
            out int width,
            out int height,
            out int channels,
            out ImageLayout layout)
        {
            width = 0;
            height = 0;
            channels = 0;
            layout = default;

            if (dimensions.Count == 4)
            {
                if (dimensions[1] is >= 1 and <= 4)
                {
                    channels = Math.Max(1, dimensions[1]);
                    height = Math.Max(1, dimensions[2]);
                    width = Math.Max(1, dimensions[3]);
                    layout = ImageLayout.Nchw;
                    return true;
                }

                if (dimensions[3] is >= 1 and <= 4)
                {
                    height = Math.Max(1, dimensions[1]);
                    width = Math.Max(1, dimensions[2]);
                    channels = Math.Max(1, dimensions[3]);
                    layout = ImageLayout.Nhwc;
                    return true;
                }
            }

            if (dimensions.Count == 3)
            {
                if (dimensions[0] is >= 1 and <= 4)
                {
                    channels = Math.Max(1, dimensions[0]);
                    height = Math.Max(1, dimensions[1]);
                    width = Math.Max(1, dimensions[2]);
                    layout = ImageLayout.Chw;
                    return true;
                }

                if (dimensions[2] is >= 1 and <= 4)
                {
                    height = Math.Max(1, dimensions[0]);
                    width = Math.Max(1, dimensions[1]);
                    channels = Math.Max(1, dimensions[2]);
                    layout = ImageLayout.Hwc;
                    return true;
                }
            }

            return false;
        }

        private static int ResolveImageTensorOffset(
            IReadOnlyList<int> dimensions,
            ImageLayout layout,
            int x,
            int y,
            int channel)
        {
            return layout switch
            {
                ImageLayout.Nchw => GetOffset(dimensions, 0, channel, y, x),
                ImageLayout.Nhwc => GetOffset(dimensions, 0, y, x, channel),
                ImageLayout.Chw => GetOffset(dimensions, channel, y, x),
                _ => GetOffset(dimensions, y, x, channel)
            };
        }

        private static int GetOffset(IReadOnlyList<int> dimensions, params int[] indices)
        {
            var stride = 1;
            var offset = 0;
            for (var index = dimensions.Count - 1; index >= 0; index--)
            {
                offset += indices[index] * stride;
                stride *= Math.Max(1, dimensions[index]);
            }

            return offset;
        }

        private static SKBitmap NormalizeMaskBitmap(SKBitmap maskBitmap, int width, int height)
        {
            if (maskBitmap.Width == width && maskBitmap.Height == height)
            {
                return ThresholdMask(maskBitmap);
            }

            using var resized = ResizeMaskBitmap(maskBitmap, width, height);
            return ThresholdMask(resized);
        }

        private static SKBitmap ThresholdMask(SKBitmap source)
        {
            var bitmap = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var input = source.GetPixel(x, y);
                    var enabled = input.Alpha >= 16 || input.Red >= 16;
                    bitmap.SetPixel(x, y, enabled ? SKColors.White : SKColors.Transparent);
                }
            }

            return bitmap;
        }

        private static bool HasVisibleMask(SKBitmap maskBitmap)
        {
            for (var y = 0; y < maskBitmap.Height; y++)
            {
                for (var x = 0; x < maskBitmap.Width; x++)
                {
                    if (maskBitmap.GetPixel(x, y).Alpha >= 16)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static SKBitmap CreateBlendMaskBitmap(SKBitmap maskBitmap)
        {
            using var image = SKImage.FromBitmap(maskBitmap);
            using var surface = SKSurface.Create(new SKImageInfo(maskBitmap.Width, maskBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                ImageFilter = SKImageFilter.CreateBlur(1.2f, 1.2f)
            })
            {
                surface.Canvas.Clear(SKColors.Transparent);
                surface.Canvas.DrawImage(image, 0, 0, paint);
                surface.Canvas.Flush();
            }

            var softened = SKBitmap.FromImage(surface.Snapshot());
            var result = new SKBitmap(new SKImageInfo(maskBitmap.Width, maskBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            for (var y = 0; y < maskBitmap.Height; y++)
            {
                for (var x = 0; x < maskBitmap.Width; x++)
                {
                    var baseAlpha = maskBitmap.GetPixel(x, y).Alpha / 255f;
                    var smoothAlpha = softened.GetPixel(x, y).Alpha / 255f;
                    var mixedAlpha = Math.Clamp((baseAlpha * 0.7f) + (smoothAlpha * 0.3f), 0f, 1f);
                    var byteAlpha = (byte)Math.Clamp((int)Math.Round(mixedAlpha * 255d, MidpointRounding.AwayFromZero), 0, 255);
                    result.SetPixel(x, y, new SKColor(byteAlpha, byteAlpha, byteAlpha, byteAlpha));
                }
            }

            softened.Dispose();
            return result;
        }

        private static SKBitmap MergeInpaintingResult(SKBitmap sourceBitmap, SKBitmap inpaintedBitmap, SKBitmap blendMaskBitmap)
        {
            var result = new SKBitmap(new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            for (var y = 0; y < sourceBitmap.Height; y++)
            {
                for (var x = 0; x < sourceBitmap.Width; x++)
                {
                    var source = sourceBitmap.GetPixel(x, y);
                    var inpainted = inpaintedBitmap.GetPixel(x, y);
                    var blend = blendMaskBitmap.GetPixel(x, y).Alpha / 255f;

                    var red = Lerp(source.Red, inpainted.Red, blend);
                    var green = Lerp(source.Green, inpainted.Green, blend);
                    var blue = Lerp(source.Blue, inpainted.Blue, blend);
                    result.SetPixel(x, y, new SKColor(red, green, blue, 255));
                }
            }

            return result;
        }

        private static byte Lerp(byte from, byte to, float amount)
        {
            return (byte)Math.Clamp(
                (int)Math.Round(from + ((to - from) * amount), MidpointRounding.AwayFromZero),
                0,
                255);
        }

        private static double CalculateMaskCoveragePercent(SKBitmap maskBitmap)
        {
            if (maskBitmap.Width <= 0 || maskBitmap.Height <= 0)
            {
                return 0d;
            }

            var totalPixels = maskBitmap.Width * maskBitmap.Height;
            var maskedPixels = 0;
            for (var y = 0; y < maskBitmap.Height; y++)
            {
                for (var x = 0; x < maskBitmap.Width; x++)
                {
                    if (maskBitmap.GetPixel(x, y).Alpha >= 16)
                    {
                        maskedPixels++;
                    }
                }
            }

            return maskedPixels / (double)totalPixels * 100d;
        }

        private static SKBitmap CreateRgbBitmap(int width, int height, IReadOnlyList<byte> values)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = ((y * width) + x) * 3;
                    bitmap.SetPixel(x, y, new SKColor(values[offset], values[offset + 1], values[offset + 2], 255));
                }
            }

            return bitmap;
        }

        private static SKBitmap DecodeSourceBitmap(string inputPath)
        {
            using var codec = SKCodec.Create(inputPath);
            if (codec is null)
            {
                throw new InvalidOperationException("The selected image cannot be decoded for AI eraser.");
            }

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                bitmap.Dispose();
                throw new InvalidOperationException("The selected image cannot be decoded for AI eraser.");
            }

            return bitmap;
        }

        private static SKBitmap ResizeBitmap(SKBitmap sourceBitmap, int width, int height)
        {
            var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            using var paint = CreateResizePaint();
            canvas.DrawBitmap(sourceBitmap, new SKRect(0, 0, width, height), paint);
            canvas.Flush();
            return target;
        }

        private static SKBitmap ResizeMaskBitmap(SKBitmap sourceBitmap, int width, int height)
        {
            var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.None,
                IsAntialias = false
            };
            canvas.DrawBitmap(sourceBitmap, new SKRect(0, 0, width, height), paint);
            canvas.Flush();
            return target;
        }

        private static SKPaint CreateResizePaint()
        {
            return new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true
            };
        }

        private static void SavePng(SKBitmap bitmap, string outputPath)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(outputPath);
            data.SaveTo(stream);
        }

        private readonly record struct ImageTensorCandidate(int Width, int Height, byte[] Values)
        {
            public int PixelCount => Width * Height;
        }

        private enum ImageLayout
        {
            Hwc,
            Chw,
            Nhwc,
            Nchw
        }
    }
}
