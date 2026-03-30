using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class ImagePreviewWindow
    {
        private readonly List<PreviewBarcodeToolResultItem> _barcodeResultItems = new();

        private bool HasBarcodeResults => _barcodeResultItems.Count > 0;

        private async Task RecognizeCurrentBarcodePreviewAsync(bool revealBusyPanelImmediately)
        {
            await RunRecognitionSessionAsync(
                PreviewRecognitionMode.Barcode,
                revealBusyPanelImmediately,
                async cancellationSource =>
                {
                    var result = await _barcodeToolController
                        .RecognizeAsync(CreateCurrentOcrImageBytesAsync, cancellationSource.Token);
                    if (ShouldIgnoreRecognitionResult(cancellationSource))
                    {
                        return;
                    }

                    ApplyBarcodeResult(result);
                });
        }

        private void ApplyBarcodeResult(PreviewBarcodeToolResult result)
        {
            _panelMode = PreviewRecognitionMode.Barcode;
            ResetQrLinkState();
            ResetQrResultState();
            ResetBarcodeResultState();

            _ocrText = string.Empty;
            _copyAllText = string.Empty;
            OcrTextBox.Text = string.Empty;

            if (result.HasResults)
            {
                SetStructuredBarcodeResults(result.Items);
                _copyAllText = BuildBarcodeResultsCopyText(result.Items);
                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(string.Empty);
            }
            else
            {
                var placeholder = result.ErrorMessage switch
                {
                    PreviewBarcodeToolController.UnavailableInputErrorCode => T("PreviewBarcodeUnavailable"),
                    null or "" => T("PreviewBarcodeEmpty"),
                    _ => result.ErrorMessage!
                };

                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(placeholder);
            }

            RefreshRecognitionContentVisibility();
            RefreshRecognitionChrome();
            RefreshOcrControls();
        }

        private void ResetBarcodeResultState()
        {
            _barcodeResultItems.Clear();
            QrResultsSection.IsVisible = false;
            RefreshQrResultsView();
        }

        private void SetStructuredBarcodeResults(IReadOnlyList<PreviewBarcodeToolResultItem> results)
        {
            _barcodeResultItems.Clear();
            _barcodeResultItems.AddRange(results);

            RefreshQrResultsView();
        }

        private string GetBarcodeResultsSummaryText()
        {
            if (_barcodeResultItems.Count <= 1)
            {
                return string.Empty;
            }

            var qrCount = 0;
            foreach (var item in _barcodeResultItems)
            {
                if (item.IsQrCode)
                {
                    qrCount++;
                }
            }

            var linearCount = _barcodeResultItems.Count - qrCount;

            if (qrCount > 0 && linearCount > 0)
            {
                return FormatT("PreviewBarcodeMixedDetected", _barcodeResultItems.Count, linearCount, qrCount);
            }

            if (qrCount == _barcodeResultItems.Count)
            {
                return FormatT("PreviewBarcodeQrOnlyDetected", qrCount);
            }

            return FormatT("PreviewBarcodeMultipleDetected", _barcodeResultItems.Count);
        }

        private string BuildBarcodeResultsCopyText(IReadOnlyList<PreviewBarcodeToolResultItem> results)
        {
            var builder = new StringBuilder();

            for (var index = 0; index < results.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(results[index].Content))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.AppendLine(FormatT("PreviewBarcodeItemTitle", index + 1));
                builder.AppendLine($"{T("PreviewBarcodeTypeLabel")}: {results[index].Format}");
                builder.AppendLine($"{T("PreviewQrContentLabel")}:");
                builder.Append(results[index].Content);
            }

            return builder.ToString();
        }

        private Control BuildBarcodeResultCard(PreviewBarcodeToolResultItem item, int displayIndex)
        {
            var border = new Border
            {
                Background = CreateQrRowBackgroundBrush(),
                BorderBrush = CreateQrRowBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel
            {
                Spacing = 10
            };

            var titleText = new TextBlock
            {
                Text = FormatT("PreviewBarcodeItemTitle", displayIndex),
                FontWeight = FontWeight.SemiBold
            };

            var typeLabelText = new TextBlock
            {
                Text = T("PreviewBarcodeTypeLabel")
            };
            typeLabelText.Classes.Add("secondary");

            var typeValueText = new TextBlock
            {
                Text = item.Format,
                FontWeight = FontWeight.SemiBold
            };

            var labelText = new TextBlock
            {
                Text = T("PreviewQrContentLabel")
            };
            labelText.Classes.Add("secondary");

            var contentText = new TextBlock
            {
                Text = item.Content,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold
            };

            stack.Children.Add(titleText);
            stack.Children.Add(typeLabelText);
            stack.Children.Add(typeValueText);
            stack.Children.Add(labelText);
            stack.Children.Add(contentText);

            if (item.HasSingleUrl && !item.Urls[0].UsesHttps)
            {
                var warningText = new TextBlock
                {
                    Text = T("PreviewQrLinkNotSecure"),
                    TextWrapping = TextWrapping.Wrap
                };
                warningText.Classes.Add("secondary");
                stack.Children.Add(warningText);
            }

            if (item.HasMultipleUrls)
            {
                var summaryText = new TextBlock
                {
                    Text = FormatT("PreviewQrLinksSummaryMultiple", item.Urls.Count),
                    TextWrapping = TextWrapping.Wrap
                };
                summaryText.Classes.Add("secondary");
                stack.Children.Add(summaryText);
            }

            var copyButton = new Button
            {
                MinHeight = 38,
                Padding = new Thickness(10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SetWrappedButtonContent(copyButton, T("PreviewQrCopyContent"));
            copyButton.Click += async (_, _) =>
            {
                await CopyTextToClipboardAsync(item.Content);
            };

            if (item.HasMultipleUrls)
            {
                var toggleButton = new Button
                {
                    MinHeight = 38,
                    Padding = new Thickness(10, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var linksHost = new StackPanel
                {
                    Spacing = 8,
                    IsVisible = false
                };

                for (var index = 0; index < item.Urls.Count; index++)
                {
                    linksHost.Children.Add(BuildQrResultLinkRow(item.Urls[index], index + 1));
                }

                var isExpanded = false;
                SetWrappedButtonContent(toggleButton, T("PreviewQrExpandLinks"));
                toggleButton.Click += (_, _) =>
                {
                    isExpanded = !isExpanded;
                    linksHost.IsVisible = isExpanded;
                    SetWrappedButtonContent(
                        toggleButton,
                        isExpanded
                            ? T("PreviewQrCollapseLinks")
                            : T("PreviewQrExpandLinks"));
                };

                var actionsGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 8
                };
                actionsGrid.Children.Add(copyButton);
                Grid.SetColumn(copyButton, 0);
                actionsGrid.Children.Add(toggleButton);
                Grid.SetColumn(toggleButton, 1);

                stack.Children.Add(actionsGrid);
                stack.Children.Add(linksHost);
            }
            else if (item.HasSingleUrl)
            {
                var openButton = new Button
                {
                    MinHeight = 38,
                    Padding = new Thickness(10, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                SetWrappedButtonContent(openButton, T("PreviewQrOpenLink"));
                openButton.Click += (_, _) =>
                {
                        OpenQrUrl(item.Urls[0].Url);
                    };

                var actionsGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 8
                };
                actionsGrid.Children.Add(openButton);
                Grid.SetColumn(openButton, 0);
                actionsGrid.Children.Add(copyButton);
                Grid.SetColumn(copyButton, 1);

                stack.Children.Add(actionsGrid);
            }
            else
            {
                stack.Children.Add(copyButton);
            }

            border.Child = stack;
            return border;
        }

        private async void OnBarcodeClick(object? sender, RoutedEventArgs e)
        {
            await RecognizeCurrentBarcodePreviewAsync(revealBusyPanelImmediately: true);
        }
    }
}
