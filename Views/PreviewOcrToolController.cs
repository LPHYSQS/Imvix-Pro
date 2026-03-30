using ImvixPro.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    internal sealed class PreviewOcrToolController
    {
        public const string UnavailableInputErrorCode = "preview-ocr-unavailable-input";

        private readonly PreviewOcrService _previewOcrService;

        public PreviewOcrToolController(PreviewOcrService? previewOcrService = null)
        {
            _previewOcrService = previewOcrService ?? new PreviewOcrService();
        }

        public async Task<PreviewOcrRecognition> RecognizeAsync(
            Func<CancellationToken, Task<byte[]?>> imageBytesFactory,
            PreviewOcrLanguageOption languageOption,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(imageBytesFactory);

            var imageBytes = await imageBytesFactory(cancellationToken).ConfigureAwait(false);
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return PreviewOcrRecognition.Error(UnavailableInputErrorCode);
            }

            return await _previewOcrService
                .RecognizeAsync(imageBytes, languageOption, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
