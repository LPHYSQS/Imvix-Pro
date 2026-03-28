using Avalonia;
using ImvixPro.Services;
using ImvixPro.AI.Matting.Models;
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

namespace ImvixPro.AI.Matting.Inference
{
    public sealed class AiMattingService
    {
        private readonly AppLogger _logger;

        public AiMattingService(AppLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AiMattingResult> ProcessAsync(
            string inputPath,
            AiMattingModel model,
            AiMattingDevice device,
            AiMattingResolutionMode resolutionMode,
            bool edgeOptimizationEnabled,
            int edgeOptimizationStrength,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("The source image for AI matting does not exist.", inputPath);
            }

            var modelsDirectory = RuntimeAssetLocator.AiMattingModelsDirectory;
            var selection = AiMattingModelCatalog.ResolveModelSelection(modelsDirectory, model);
            var workingDirectory = Path.Combine(
                Path.GetTempPath(),
                "ImvixPro",
                "Matting",
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                return await Task.Run(
                    () => RunMattingCore(
                        inputPath,
                        selection,
                        device,
                        resolutionMode,
                        edgeOptimizationEnabled,
                        Math.Clamp(edgeOptimizationStrength, 0, 100),
                        workingDirectory,
                        cancellationToken),
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
                _logger.LogDebug(nameof(AiMattingService), $"Failed to delete AI matting working directory '{path}'.", ex);
            }
        }

        private AiMattingResult RunMattingCore(
            string inputPath,
            AiMattingModelCatalog.ResolvedAiMattingModelSelection selection,
            AiMattingDevice requestedDevice,
            AiMattingResolutionMode resolutionMode,
            bool edgeOptimizationEnabled,
            int edgeOptimizationStrength,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var sourceBitmap = DecodeSourceBitmap(inputPath);
            using var previewBitmap = CreatePreviewBitmap(sourceBitmap, resolutionMode);

            using var runtime = CreateRuntime(selection, requestedDevice, out var usedGpuFallback);
            var definition = selection.Definition;
            var inputSize = ResolveInputSize(runtime.Session, definition);
            using var preparedBitmap = CreateModelInputBitmap(previewBitmap, inputSize, definition, out var preprocess);
            using var rawMaskBitmap = RunInference(runtime.Session, preparedBitmap, definition, cancellationToken);
            using var restoredPreviewMask = RestoreMaskToPreviewSize(rawMaskBitmap, previewBitmap.Width, previewBitmap.Height, preprocess);
            using var finalMaskBitmap = ResizeMaskToSourceSize(restoredPreviewMask, sourceBitmap.Width, sourceBitmap.Height);
            using var optimizedMask = edgeOptimizationEnabled && edgeOptimizationStrength > 0
                ? ApplyEdgeOptimization(finalMaskBitmap, edgeOptimizationStrength)
                : finalMaskBitmap.Copy();
            using var resultBitmap = ApplyMaskToSource(sourceBitmap, optimizedMask);

            var outputPath = Path.Combine(workingDirectory, "matting-result.png");
            SavePng(resultBitmap, outputPath);

            var metrics = CalculateMaskMetrics(optimizedMask);
            return new AiMattingResult(
                workingDirectory,
                outputPath,
                new PixelSize(sourceBitmap.Width, sourceBitmap.Height),
                new PixelSize(previewBitmap.Width, previewBitmap.Height),
                metrics.AverageAlphaPercent,
                metrics.ForegroundCoveragePercent,
                selection.Definition.Model,
                selection.UsedFallback,
                runtime.EffectiveDevice,
                usedGpuFallback);
        }

        private RuntimeHandle CreateRuntime(
            AiMattingModelCatalog.ResolvedAiMattingModelSelection selection,
            AiMattingDevice requestedDevice,
            out bool usedGpuFallback)
        {
            usedGpuFallback = false;

            if (requestedDevice == AiMattingDevice.GpuDirectMl)
            {
                try
                {
                    return CreateSession(selection.ModelPath, AiMattingDevice.GpuDirectMl);
                }
                catch (Exception ex)
                {
                    usedGpuFallback = true;
                    _logger.LogWarning(nameof(AiMattingService), "DirectML session creation failed. Falling back to CPU.", ex);
                }
            }

            return CreateSession(selection.ModelPath, AiMattingDevice.Cpu);
        }

        private static RuntimeHandle CreateSession(string modelPath, AiMattingDevice device)
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            if (device == AiMattingDevice.GpuDirectMl)
            {
                options.AppendExecutionProvider_DML(0);
            }

            var session = new InferenceSession(modelPath, options);
            return new RuntimeHandle(session, device);
        }

        private static SKBitmap DecodeSourceBitmap(string inputPath)
        {
            using var codec = SKCodec.Create(inputPath);
            if (codec is null)
            {
                throw new InvalidOperationException("The selected image cannot be decoded for AI matting.");
            }

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            {
                bitmap.Dispose();
                throw new InvalidOperationException("The selected image cannot be decoded for AI matting.");
            }

            return bitmap;
        }

        private static SKBitmap CreatePreviewBitmap(SKBitmap sourceBitmap, AiMattingResolutionMode resolutionMode)
        {
            var maxDimension = resolutionMode switch
            {
                AiMattingResolutionMode.Max1024 => 1024,
                AiMattingResolutionMode.Max2048 => 2048,
                _ => 0
            };

            if (maxDimension <= 0)
            {
                return sourceBitmap.Copy();
            }

            var currentMax = Math.Max(sourceBitmap.Width, sourceBitmap.Height);
            if (currentMax <= maxDimension)
            {
                return sourceBitmap.Copy();
            }

            var scale = maxDimension / (double)currentMax;
            var width = Math.Max(1, (int)Math.Round(sourceBitmap.Width * scale, MidpointRounding.AwayFromZero));
            var height = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale, MidpointRounding.AwayFromZero));
            return ResizeBitmap(sourceBitmap, width, height);
        }

        private static PixelSize ResolveInputSize(InferenceSession session, AiMattingModelCatalog.AiMattingModelDefinition definition)
        {
            var metadata = session.InputMetadata.Values.FirstOrDefault();
            if (metadata is not null && metadata.Dimensions.Length >= 4)
            {
                var height = metadata.Dimensions[^2];
                var width = metadata.Dimensions[^1];
                if (height > 0 && width > 0)
                {
                    return new PixelSize(width, height);
                }
            }

            return new PixelSize(definition.DefaultInputWidth, definition.DefaultInputHeight);
        }

        private static SKBitmap CreateModelInputBitmap(
            SKBitmap sourceBitmap,
            PixelSize inputSize,
            AiMattingModelCatalog.AiMattingModelDefinition definition,
            out PreprocessMetadata metadata)
        {
            var width = Math.Max(1, inputSize.Width);
            var height = Math.Max(1, inputSize.Height);

            if (definition.KeepAspectRatio)
            {
                var scale = Math.Min(width / (double)sourceBitmap.Width, height / (double)sourceBitmap.Height);
                var resizedWidth = Math.Max(1, (int)Math.Round(sourceBitmap.Width * scale, MidpointRounding.AwayFromZero));
                var resizedHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale, MidpointRounding.AwayFromZero));
                var offsetX = (width - resizedWidth) / 2;
                var offsetY = (height - resizedHeight) / 2;

                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var bitmap = new SKBitmap(info);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Black);
                using var paint = CreateResizePaint();
                canvas.DrawBitmap(sourceBitmap, new SKRect(offsetX, offsetY, offsetX + resizedWidth, offsetY + resizedHeight), paint);
                canvas.Flush();

                metadata = new PreprocessMetadata(width, height, resizedWidth, resizedHeight, offsetX, offsetY);
                return bitmap;
            }

            metadata = new PreprocessMetadata(width, height, width, height, 0, 0);
            return ResizeBitmap(sourceBitmap, width, height);
        }

        private static SKBitmap RunInference(
            InferenceSession session,
            SKBitmap inputBitmap,
            AiMattingModelCatalog.AiMattingModelDefinition definition,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputMeta = session.InputMetadata.First();
            var inputName = inputMeta.Key;
            var tensor = BuildInputTensor(inputBitmap, definition);

            using var results = session.Run(
                [NamedOnnxValue.CreateFromTensor(inputName, tensor)],
                session.OutputMetadata.Keys.ToArray());

            return ExtractMaskBitmap(results);
        }

        private static DenseTensor<float> BuildInputTensor(
            SKBitmap inputBitmap,
            AiMattingModelCatalog.AiMattingModelDefinition definition)
        {
            var tensor = new DenseTensor<float>([1, 3, inputBitmap.Height, inputBitmap.Width]);

            for (var y = 0; y < inputBitmap.Height; y++)
            {
                for (var x = 0; x < inputBitmap.Width; x++)
                {
                    var color = inputBitmap.GetPixel(x, y);
                    var red = color.Red / 255f;
                    var green = color.Green / 255f;
                    var blue = color.Blue / 255f;

                    tensor[0, 0, y, x] = (red - definition.Mean[0]) / definition.Std[0];
                    tensor[0, 1, y, x] = (green - definition.Mean[1]) / definition.Std[1];
                    tensor[0, 2, y, x] = (blue - definition.Mean[2]) / definition.Std[2];
                }
            }

            return tensor;
        }

        private static SKBitmap ExtractMaskBitmap(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            MaskTensorCandidate? bestCandidate = null;

            foreach (var result in results)
            {
                if (!TryCreateMaskCandidate(result.Value, out var candidate))
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
                throw new InvalidOperationException("The selected AI matting model did not return a compatible mask tensor.");
            }

            return CreateMaskBitmap(bestCandidate.Value.Width, bestCandidate.Value.Height, bestCandidate.Value.Values);
        }

        private static bool TryCreateMaskCandidate(object? value, out MaskTensorCandidate candidate)
        {
            candidate = default;

            return value switch
            {
                Tensor<float> tensor => TryExtractMaskValues(tensor, static raw => raw, out candidate),
                Tensor<double> tensor => TryExtractMaskValues(tensor, static raw => (float)raw, out candidate),
                Tensor<byte> tensor => TryExtractMaskValues(tensor, static raw => raw / 255f, out candidate),
                Tensor<Half> tensor => TryExtractMaskValues(tensor, static raw => (float)raw, out candidate),
                _ => false
            };
        }

        private static bool TryExtractMaskValues<T>(
            Tensor<T> tensor,
            Func<T, float> converter,
            out MaskTensorCandidate candidate)
        {
            candidate = default;

            var dimensions = tensor.Dimensions.ToArray();
            if (dimensions.Length < 2)
            {
                return false;
            }

            if (!TryResolveMaskShape(dimensions, out var width, out var height, out var layout))
            {
                return false;
            }

            var source = tensor.ToArray();
            var values = new float[width * height];
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var raw = converter(source[ResolveTensorOffset(dimensions, layout, x, y)]);
                    values[(y * width) + x] = raw;
                    if (raw < min)
                    {
                        min = raw;
                    }

                    if (raw > max)
                    {
                        max = raw;
                    }
                }
            }

            NormalizeMaskValues(values, min, max);
            candidate = new MaskTensorCandidate(width, height, values);
            return true;
        }

        private static bool TryResolveMaskShape(
            IReadOnlyList<int> dimensions,
            out int width,
            out int height,
            out MaskLayout layout)
        {
            width = 0;
            height = 0;
            layout = default;

            if (dimensions.Count == 4)
            {
                if (dimensions[1] > 0 && dimensions[1] <= 4)
                {
                    height = Math.Max(1, dimensions[2]);
                    width = Math.Max(1, dimensions[3]);
                    layout = MaskLayout.Nchw;
                    return true;
                }

                if (dimensions[3] > 0 && dimensions[3] <= 4)
                {
                    height = Math.Max(1, dimensions[1]);
                    width = Math.Max(1, dimensions[2]);
                    layout = MaskLayout.Nhwc;
                    return true;
                }
            }

            if (dimensions.Count == 3)
            {
                if (dimensions[0] > 0 && dimensions[0] <= 4 && dimensions[1] > 4 && dimensions[2] > 4)
                {
                    height = Math.Max(1, dimensions[1]);
                    width = Math.Max(1, dimensions[2]);
                    layout = MaskLayout.Chw;
                    return true;
                }

                if (dimensions[2] > 0 && dimensions[2] <= 4)
                {
                    height = Math.Max(1, dimensions[0]);
                    width = Math.Max(1, dimensions[1]);
                    layout = MaskLayout.Hwc;
                    return true;
                }

                height = Math.Max(1, dimensions[1]);
                width = Math.Max(1, dimensions[2]);
                layout = MaskLayout.Bhw;
                return true;
            }

            if (dimensions.Count == 2)
            {
                height = Math.Max(1, dimensions[0]);
                width = Math.Max(1, dimensions[1]);
                layout = MaskLayout.Hw;
                return true;
            }

            return false;
        }

        private static int ResolveTensorOffset(
            IReadOnlyList<int> dimensions,
            MaskLayout layout,
            int x,
            int y)
        {
            return layout switch
            {
                MaskLayout.Nchw => GetOffset(dimensions, 0, 0, y, x),
                MaskLayout.Nhwc => GetOffset(dimensions, 0, y, x, 0),
                MaskLayout.Chw => GetOffset(dimensions, 0, y, x),
                MaskLayout.Hwc => GetOffset(dimensions, y, x, 0),
                MaskLayout.Bhw => GetOffset(dimensions, 0, y, x),
                _ => GetOffset(dimensions, y, x)
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

        private static void NormalizeMaskValues(float[] values, float min, float max)
        {
            if (values.Length == 0)
            {
                return;
            }

            if (min >= 0f && max <= 1f)
            {
                return;
            }

            if (max - min < 1e-6f)
            {
                for (var index = 0; index < values.Length; index++)
                {
                    values[index] = Math.Clamp(values[index], 0f, 1f);
                }

                return;
            }

            var scale = 1f / (max - min);
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = Math.Clamp((values[index] - min) * scale, 0f, 1f);
            }
        }

        private static SKBitmap CreateMaskBitmap(int width, int height, IReadOnlyList<float> values)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var alpha = (byte)Math.Clamp((int)Math.Round(values[(y * width) + x] * 255d, MidpointRounding.AwayFromZero), 0, 255);
                    bitmap.SetPixel(x, y, new SKColor(alpha, alpha, alpha, 255));
                }
            }

            return bitmap;
        }

        private static SKBitmap RestoreMaskToPreviewSize(
            SKBitmap rawMaskBitmap,
            int previewWidth,
            int previewHeight,
            PreprocessMetadata preprocess)
        {
            if (preprocess.OffsetX == 0 &&
                preprocess.OffsetY == 0 &&
                preprocess.ResizedWidth == rawMaskBitmap.Width &&
                preprocess.ResizedHeight == rawMaskBitmap.Height)
            {
                return ResizeBitmap(rawMaskBitmap, previewWidth, previewHeight);
            }

            using var cropped = new SKBitmap(new SKImageInfo(preprocess.ResizedWidth, preprocess.ResizedHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.Clear(SKColors.Black);
                using var paint = CreateResizePaint();
                canvas.DrawBitmap(
                    rawMaskBitmap,
                    new SKRectI(preprocess.OffsetX, preprocess.OffsetY, preprocess.OffsetX + preprocess.ResizedWidth, preprocess.OffsetY + preprocess.ResizedHeight),
                    new SKRect(0, 0, preprocess.ResizedWidth, preprocess.ResizedHeight),
                    paint);
                canvas.Flush();
            }

            return ResizeBitmap(cropped, previewWidth, previewHeight);
        }

        private static SKBitmap ResizeMaskToSourceSize(SKBitmap previewMaskBitmap, int sourceWidth, int sourceHeight)
        {
            if (previewMaskBitmap.Width == sourceWidth && previewMaskBitmap.Height == sourceHeight)
            {
                return previewMaskBitmap.Copy();
            }

            return ResizeBitmap(previewMaskBitmap, sourceWidth, sourceHeight);
        }

        private static SKBitmap ApplyEdgeOptimization(SKBitmap maskBitmap, int strength)
        {
            var blurRadius = 0.35f + (strength / 100f * 3.25f);
            using var image = SKImage.FromBitmap(maskBitmap);
            using var surface = SKSurface.Create(new SKImageInfo(maskBitmap.Width, maskBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                ImageFilter = SKImageFilter.CreateBlur(blurRadius, blurRadius)
            })
            {
                surface.Canvas.Clear(SKColors.Black);
                surface.Canvas.DrawImage(image, 0, 0, paint);
                surface.Canvas.Flush();
            }

            using var blurred = SKBitmap.FromImage(surface.Snapshot());
            var result = new SKBitmap(new SKImageInfo(maskBitmap.Width, maskBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            var blend = Math.Clamp(strength / 100f, 0f, 1f) * 0.75f;

            for (var y = 0; y < maskBitmap.Height; y++)
            {
                for (var x = 0; x < maskBitmap.Width; x++)
                {
                    var original = maskBitmap.GetPixel(x, y).Red / 255f;
                    var softened = blurred.GetPixel(x, y).Red / 255f;
                    var edgeBlend = original > 0.03f && original < 0.97f ? blend : 0f;
                    var alpha = original + ((softened - original) * edgeBlend);
                    var byteAlpha = (byte)Math.Clamp((int)Math.Round(alpha * 255d, MidpointRounding.AwayFromZero), 0, 255);
                    result.SetPixel(x, y, new SKColor(byteAlpha, byteAlpha, byteAlpha, 255));
                }
            }

            return result;
        }

        private static SKBitmap ApplyMaskToSource(SKBitmap sourceBitmap, SKBitmap maskBitmap)
        {
            var result = new SKBitmap(new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));

            for (var y = 0; y < sourceBitmap.Height; y++)
            {
                for (var x = 0; x < sourceBitmap.Width; x++)
                {
                    var source = sourceBitmap.GetPixel(x, y);
                    var mask = maskBitmap.GetPixel(x, y).Red;
                    var sourceAlpha = source.Alpha / 255f;
                    var finalAlpha = (byte)Math.Clamp((int)Math.Round(mask * sourceAlpha, MidpointRounding.AwayFromZero), 0, 255);
                    result.SetPixel(x, y, new SKColor(source.Red, source.Green, source.Blue, finalAlpha));
                }
            }

            return result;
        }

        private static MaskMetrics CalculateMaskMetrics(SKBitmap maskBitmap)
        {
            if (maskBitmap.Width <= 0 || maskBitmap.Height <= 0)
            {
                return new MaskMetrics(0d, 0d);
            }

            var total = maskBitmap.Width * maskBitmap.Height;
            var alphaSum = 0d;
            var foregroundPixels = 0;

            for (var y = 0; y < maskBitmap.Height; y++)
            {
                for (var x = 0; x < maskBitmap.Width; x++)
                {
                    var alpha = maskBitmap.GetPixel(x, y).Red;
                    alphaSum += alpha;
                    if (alpha >= 16)
                    {
                        foregroundPixels++;
                    }
                }
            }

            return new MaskMetrics(
                alphaSum / total / 255d * 100d,
                foregroundPixels / (double)total * 100d);
        }

        private static void SavePng(SKBitmap bitmap, string outputPath)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(outputPath);
            data.SaveTo(stream);
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

        private static SKPaint CreateResizePaint()
        {
            return new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true
            };
        }

        private readonly record struct RuntimeHandle(InferenceSession Session, AiMattingDevice EffectiveDevice) : IDisposable
        {
            public void Dispose()
            {
                Session.Dispose();
            }
        }

        private readonly record struct PreprocessMetadata(
            int InputWidth,
            int InputHeight,
            int ResizedWidth,
            int ResizedHeight,
            int OffsetX,
            int OffsetY);

        private readonly record struct MaskMetrics(double AverageAlphaPercent, double ForegroundCoveragePercent);

        private readonly record struct MaskTensorCandidate(int Width, int Height, float[] Values)
        {
            public int PixelCount => Width * Height;
        }

        private enum MaskLayout
        {
            Hw,
            Bhw,
            Chw,
            Hwc,
            Nchw,
            Nhwc
        }
    }
}




