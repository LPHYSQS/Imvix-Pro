using RapidOcrNet;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImvixPro.Services
{
    internal sealed class PreviewPaddleOcrEngineAdapter : IDisposable
    {
        private readonly RapidOcr _engine = new();
        private readonly RapidOcrOptions _options;

        public PreviewPaddleOcrEngineAdapter(
            string detPath,
            string clsPath,
            string recPath,
            string keysPath,
            RapidOcrOptions options,
            int numThreads = 0)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _engine.InitModels(detPath, clsPath, recPath, keysPath, numThreads);
        }

        public PreviewPaddleOcrEngineResult Recognize(byte[] imageBytes)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);

            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null)
            {
                return PreviewPaddleOcrEngineResult.Empty();
            }

            var result = _engine.Detect(bitmap, _options);
            var text = result.StrRes ?? string.Empty;
            var meanConfidence = CalculateMeanConfidence(result);

            return string.IsNullOrWhiteSpace(text)
                ? PreviewPaddleOcrEngineResult.Empty(meanConfidence)
                : new PreviewPaddleOcrEngineResult(text, true, meanConfidence);
        }

        public void Dispose()
        {
            _engine.Dispose();
        }

        private static float CalculateMeanConfidence(OcrResult result)
        {
            var scores = new List<float>();

            foreach (var block in result.TextBlocks)
            {
                if (block.CharScores is { Length: > 0 })
                {
                    foreach (var score in block.CharScores)
                    {
                        if (!float.IsNaN(score) && !float.IsInfinity(score))
                        {
                            scores.Add(score);
                        }
                    }

                    continue;
                }

                if (!float.IsNaN(block.BoxScore) && !float.IsInfinity(block.BoxScore))
                {
                    scores.Add(block.BoxScore);
                }
            }

            if (scores.Count == 0)
            {
                return 0f;
            }

            var mean = scores.Average();
            return Math.Clamp(mean, 0f, 1f);
        }
    }

    internal sealed record PreviewPaddleOcrEngineResult(
        string Text,
        bool HasText,
        float MeanConfidence)
    {
        public static PreviewPaddleOcrEngineResult Empty(float meanConfidence = 0f)
        {
            return new PreviewPaddleOcrEngineResult(string.Empty, false, meanConfidence);
        }
    }
}
