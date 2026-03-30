using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    internal sealed record PreviewToolLinkItem(string Url, bool UsesHttps);

    internal sealed record PreviewQrToolResultItem(string Content, IReadOnlyList<PreviewToolLinkItem> Urls)
    {
        public bool HasUrls => Urls.Count > 0;

        public bool HasSingleUrl => Urls.Count == 1;

        public bool HasMultipleUrls => Urls.Count > 1;
    }

    internal sealed record PreviewQrToolResult(
        IReadOnlyList<PreviewQrToolResultItem> Items,
        string? ErrorMessage)
    {
        public bool HasResults => Items.Count > 0;

        public bool HasSingleResult => Items.Count == 1;

        public PreviewQrToolResultItem? SingleItem => HasSingleResult ? Items[0] : null;
    }

    internal sealed class PreviewQrToolController
    {
        public const string UnavailableInputErrorCode = "preview-qr-unavailable-input";

        private readonly PreviewQrService _previewQrService;

        public PreviewQrToolController(PreviewQrService? previewQrService = null)
        {
            _previewQrService = previewQrService ?? new PreviewQrService();
        }

        public async Task<PreviewQrToolResult> RecognizeAsync(
            Func<CancellationToken, Task<byte[]?>> imageBytesFactory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(imageBytesFactory);

            var imageBytes = await imageBytesFactory(cancellationToken).ConfigureAwait(false);
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return new PreviewQrToolResult([], UnavailableInputErrorCode);
            }

            var result = await _previewQrService
                .RecognizeAllAsync(imageBytes, cancellationToken)
                .ConfigureAwait(false);

            if (!result.HasResults)
            {
                return new PreviewQrToolResult([], result.ErrorMessage);
            }

            List<PreviewQrToolResultItem> items = [];
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

                items.Add(new PreviewQrToolResultItem(recognition.Content, links));
            }

            return new PreviewQrToolResult(items, result.ErrorMessage);
        }
    }
}
