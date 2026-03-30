using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    internal sealed record PreviewToolLinkItem(string Url, bool UsesHttps);

    internal sealed record PreviewBarcodeToolResultItem(
        string Content,
        string Format,
        bool IsQrCode,
        IReadOnlyList<PreviewToolLinkItem> Urls)
    {
        public bool HasUrls => Urls.Count > 0;

        public bool HasSingleUrl => Urls.Count == 1;

        public bool HasMultipleUrls => Urls.Count > 1;
    }

    internal sealed record PreviewBarcodeToolResult(
        IReadOnlyList<PreviewBarcodeToolResultItem> Items,
        string? ErrorMessage)
    {
        public bool HasResults => Items.Count > 0;
    }

    internal sealed class PreviewBarcodeToolController
    {
        private readonly PreviewBarcodeService _previewBarcodeService;

        public PreviewBarcodeToolController(PreviewBarcodeService? previewBarcodeService = null)
        {
            _previewBarcodeService = previewBarcodeService ?? new PreviewBarcodeService();
        }

        public async Task<PreviewBarcodeToolResult> RecognizeAsync(
            Func<CancellationToken, Task<byte[]?>> imageBytesFactory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(imageBytesFactory);

            var imageBytes = await imageBytesFactory(cancellationToken).ConfigureAwait(false);
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return new PreviewBarcodeToolResult([], null);
            }

            var result = await _previewBarcodeService
                .RecognizeAllAsync(imageBytes, cancellationToken)
                .ConfigureAwait(false);

            if (!result.HasResults)
            {
                return new PreviewBarcodeToolResult([], result.ErrorMessage);
            }

            List<PreviewBarcodeToolResultItem> items = [];
            foreach (var recognition in result.Results)
            {
                if (string.IsNullOrWhiteSpace(recognition.Content))
                {
                    continue;
                }

                List<PreviewToolLinkItem> links = [];
                foreach (var url in PreviewQrService.ExtractUrls(recognition.Content))
                {
                    links.Add(new PreviewToolLinkItem(
                        url,
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)));
                }

                items.Add(new PreviewBarcodeToolResultItem(
                    recognition.Content,
                    recognition.Format,
                    recognition.IsQrCode,
                    links));
            }

            return new PreviewBarcodeToolResult(items, result.ErrorMessage);
        }
    }
}
