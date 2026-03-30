using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class ImagePreviewWindow
    {
        private const int QrLinksAnimationDurationMilliseconds = 180;
        private const double QrLinksPanelMaxHeight = 256d;
        private const double RecognitionContentFallbackHeight = 360d;
        private const double RecognitionTextMinHeight = 132d;
        private const double QrLinksPanelMinHeight = 72d;
        private const double QrLinksSectionMaxHeightRatio = 0.46d;
        private const double QrLinksSectionSpacingHeight = 40d;

        private enum PreviewRecognitionMode
        {
            Ocr = 0,
            Qr,
            Barcode
        }

        private sealed record PreviewQrResultItem(string Content, IReadOnlyList<PreviewToolLinkItem> Urls)
        {
            public bool HasUrls => Urls.Count > 0;

            public bool HasSingleUrl => Urls.Count == 1;

            public bool HasMultipleUrls => Urls.Count > 1;
        }

        private readonly PreviewQrService _previewQrService = new();
        private readonly List<PreviewToolLinkItem> _qrUrlItems = new();
        private readonly List<PreviewQrResultItem> _qrResultItems = new();
        private PreviewRecognitionMode _panelMode = PreviewRecognitionMode.Ocr;
        private CancellationTokenSource? _qrLinksAnimationCts;
        private bool _isQrLinksExpanded;

        private bool HasQrUrls => _qrUrlItems.Count > 0;
        private bool HasMultipleQrResults => _qrResultItems.Count > 1;

        private string GetActionButtonText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrButton"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeButton"),
                _ => T("PreviewOcrButton")
            };
        }

        private static TextBlock CreateWrappedButtonText(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
        }

        private static void SetWrappedButtonContent(Button button, string text)
        {
            if (button.Content is TextBlock textBlock)
            {
                textBlock.Text = text;
                return;
            }

            button.Content = CreateWrappedButtonText(text);
        }

        private string GetBusyText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrBusy"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeBusy"),
                _ => T("PreviewOcrBusy")
            };
        }

        private string GetPanelTitleText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrViewTitle"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeViewTitle"),
                _ => T("PreviewOcrViewTitle")
            };
        }

        private string GetRetryText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrRetry"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeRetry"),
                _ => T("PreviewOcrRetry")
            };
        }

        private string GetCloseText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrClose"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeClose"),
                _ => T("PreviewOcrClose")
            };
        }

        private string GetEmptyPlaceholderText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrEmpty"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeEmpty"),
                _ => T("PreviewOcrEmpty")
            };
        }

        private string GetUnavailableText(PreviewRecognitionMode mode)
        {
            return mode switch
            {
                PreviewRecognitionMode.Qr => T("PreviewQrUnavailable"),
                PreviewRecognitionMode.Barcode => T("PreviewBarcodeUnavailable"),
                _ => T("PreviewOcrUnavailable")
            };
        }

        private void RefreshRecognitionChrome()
        {
            OcrPanelTitleText.Text = GetPanelTitleText(_panelMode);
            SetWrappedButtonContent(OcrRetryButton, GetRetryText(_panelMode));
            SetWrappedButtonContent(OcrCopyAllButton, T("PreviewOcrCopyAll"));
            SetWrappedButtonContent(CloseOcrButton, GetCloseText(_panelMode));
            SetWrappedButtonContent(OpenLinkButton, T("PreviewQrOpenLink"));

            SetWrappedButtonContent(
                OcrButton,
                _isOcrBusy && _panelMode == PreviewRecognitionMode.Ocr
                    ? GetBusyText(PreviewRecognitionMode.Ocr)
                    : GetActionButtonText(PreviewRecognitionMode.Ocr));
            SetWrappedButtonContent(
                QrButton,
                _isOcrBusy && _panelMode == PreviewRecognitionMode.Qr
                    ? GetBusyText(PreviewRecognitionMode.Qr)
                    : GetActionButtonText(PreviewRecognitionMode.Qr));
            SetWrappedButtonContent(
                BarcodeButton,
                _isOcrBusy && _panelMode == PreviewRecognitionMode.Barcode
                    ? GetBusyText(PreviewRecognitionMode.Barcode)
                    : GetActionButtonText(PreviewRecognitionMode.Barcode));

            RefreshQrResultsView();
            RefreshQrLinksView();
        }

        private async Task RecognizeCurrentQrPreviewAsync(bool revealBusyPanelImmediately)
        {
            if (_isClosed)
            {
                return;
            }

            CancelPendingOcr();
            var cancellationSource = new CancellationTokenSource();
            _ocrCts = cancellationSource;
            _isOcrBusy = true;
            _panelMode = PreviewRecognitionMode.Qr;
            ResetQrLinkState();
            ResetQrResultState();

            try
            {
                UpdateOcrBusyUi();

                if (revealBusyPanelImmediately || _isOcrPanelVisible)
                {
                    await SetOcrPanelVisibleAsync(true);
                    await WaitForUiRenderAsync();
                }

                var imageBytes = await CreateCurrentOcrImageBytesAsync(cancellationSource.Token);
                if (imageBytes is null || imageBytes.Length == 0)
                {
                    ApplyQrResult(PreviewQrRecognition.Error(GetUnavailableText(PreviewRecognitionMode.Qr)));
                    return;
                }

                var result = await _previewQrService.RecognizeAllAsync(imageBytes, cancellationSource.Token);
                if (_isClosed || !ReferenceEquals(_ocrCts, cancellationSource))
                {
                    return;
                }

                if (result.Results.Count > 1)
                {
                    ApplyMultiQrResult(result);
                }
                else
                {
                    ApplyQrResult(ToSingleQrRecognition(result));
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore stale QR requests when the user switches pages or frames.
            }
            finally
            {
                if (ReferenceEquals(_ocrCts, cancellationSource))
                {
                    _ocrCts = null;
                    _isOcrBusy = false;
                    UpdateOcrBusyUi();
                }

                cancellationSource.Dispose();
            }
        }

        private async Task RecognizeCurrentVisibleModeAsync(bool revealBusyPanelImmediately)
        {
            if (_panelMode == PreviewRecognitionMode.Qr)
            {
                await RecognizeCurrentQrPreviewAsync(revealBusyPanelImmediately);
                return;
            }

            if (_panelMode == PreviewRecognitionMode.Barcode)
            {
                await RecognizeCurrentBarcodePreviewAsync(revealBusyPanelImmediately);
                return;
            }

            await RecognizeCurrentPreviewAsync(revealBusyPanelImmediately);
        }

        private void ApplyQrResult(PreviewQrRecognition result)
        {
            _panelMode = PreviewRecognitionMode.Qr;
            ResetQrLinkState();
            ResetQrResultState();
            ResetBarcodeResultState();

            _ocrText = result.HasText ? result.Text : string.Empty;
            _copyAllText = _ocrText;
            OcrTextBox.Text = _ocrText;

            if (result.HasText)
            {
                SetStructuredQrLinks(PreviewQrService.ExtractUrls(result.Text));
                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(string.Empty);
            }
            else
            {
                var placeholder = result.ErrorMessage switch
                {
                    PreviewQrService.PathErrorCode => T("PreviewQrUnavailable"),
                    PreviewQrService.InitializationFailedErrorCode => T("PreviewQrInitializationFailed"),
                    null or "" => T("PreviewQrEmpty"),
                    _ => result.ErrorMessage!
                };

                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(placeholder);
            }

            RefreshRecognitionContentVisibility();
            RefreshRecognitionChrome();
            RefreshOcrControls();
        }

        private void ApplyMultiQrResult(PreviewQrBatchRecognition result)
        {
            _panelMode = PreviewRecognitionMode.Qr;
            ResetQrLinkState();
            ResetQrResultState();
            ResetBarcodeResultState();

            _ocrText = string.Empty;
            _copyAllText = BuildQrResultsCopyText(result.Results);
            OcrTextBox.Text = string.Empty;

            if (result.HasResults)
            {
                SetStructuredQrResults(result.Results);
                OcrStatusText.Text = string.Empty;
                SetOcrPlaceholder(string.Empty);
            }
            else
            {
                SetOcrPlaceholder(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? T("PreviewQrEmpty")
                    : result.ErrorMessage!);
            }

            RefreshRecognitionContentVisibility();
            RefreshRecognitionChrome();
            RefreshOcrControls();
        }

        private void ClearRecognitionResultState()
        {
            _ocrText = string.Empty;
            _copyAllText = string.Empty;
            ResetQrLinkState();
            ResetQrResultState();
            ResetBarcodeResultState();
            OcrTextBox.Text = string.Empty;
            SetOcrPlaceholder(string.Empty);
            OcrStatusText.Text = string.Empty;
            RefreshRecognitionContentVisibility();
            RefreshRecognitionChrome();
            RefreshOcrControls();
        }

        private void RefreshRecognitionContentVisibility()
        {
            var showMultipleQrResults = !_isOcrBusy &&
                                        _panelMode == PreviewRecognitionMode.Qr &&
                                        HasMultipleQrResults;
            var showBarcodeResults = !_isOcrBusy &&
                                     _panelMode == PreviewRecognitionMode.Barcode &&
                                     HasBarcodeResults;
            var showText = !_isOcrBusy &&
                           !showMultipleQrResults &&
                           !showBarcodeResults &&
                           !string.IsNullOrWhiteSpace(_ocrText);
            var showQrLinksSection = !_isOcrBusy &&
                                     _panelMode == PreviewRecognitionMode.Qr &&
                                     HasQrUrls &&
                                     showText;

            OcrTextBox.IsVisible = showText;
            QrResultsSection.IsVisible = showMultipleQrResults || showBarcodeResults;
            QrLinksSection.IsVisible = showQrLinksSection;

            if (!showQrLinksSection)
            {
                CancelQrLinksAnimation();
                QrLinksPanel.IsVisible = false;
                QrLinksPanel.Height = 0d;
                QrLinksPanel.Opacity = 0d;
            }

            RefreshRecognitionLayoutBounds();
        }

        private void ResetQrLinkState()
        {
            CancelQrLinksAnimation();
            _isQrLinksExpanded = false;
            _qrUrlItems.Clear();
            QrLinksSection.IsVisible = false;
            QrLinksPanel.IsVisible = false;
            QrLinksPanel.Height = 0d;
            QrLinksPanel.Opacity = 0d;
            RefreshQrLinksView();
        }

        private void ResetQrResultState()
        {
            _qrResultItems.Clear();
            QrResultsSection.IsVisible = false;
            RefreshQrResultsView();
        }

        private void SetStructuredQrLinks(IReadOnlyList<string> urls)
        {
            _qrUrlItems.Clear();

            foreach (var url in urls)
            {
                _qrUrlItems.Add(new PreviewToolLinkItem(
                    url,
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)));
            }

            _isQrLinksExpanded = false;
            QrLinksPanel.IsVisible = false;
            QrLinksPanel.Height = 0d;
            QrLinksPanel.Opacity = 0d;
            RefreshQrLinksView();
        }

        private void SetStructuredQrResults(IReadOnlyList<PreviewQrCodeResult> results)
        {
            _qrResultItems.Clear();

            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.Content))
                {
                    continue;
                }

                var urlItems = new List<PreviewToolLinkItem>();
                foreach (var url in PreviewQrService.ExtractUrls(result.Content))
                {
                    urlItems.Add(new PreviewToolLinkItem(
                        url,
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)));
                }

                _qrResultItems.Add(new PreviewQrResultItem(result.Content, urlItems));
            }

            RefreshQrResultsView();
        }

        private void RefreshQrResultsView()
        {
            QrResultsSummaryText.Text = _panelMode == PreviewRecognitionMode.Barcode
                ? GetBarcodeResultsSummaryText()
                : GetQrResultsSummaryText();
            QrResultsHost.Children.Clear();

            if (_panelMode == PreviewRecognitionMode.Barcode)
            {
                foreach (var item in _barcodeResultItems)
                {
                    QrResultsHost.Children.Add(BuildBarcodeResultCard(item, QrResultsHost.Children.Count + 1));
                }
            }
            else
            {
                foreach (var item in _qrResultItems)
                {
                    QrResultsHost.Children.Add(BuildQrResultCard(item, QrResultsHost.Children.Count + 1));
                }
            }

            RefreshRecognitionLayoutBounds();
        }

        private void RefreshQrLinksView()
        {
            QrLinksSummaryText.Text = GetQrLinksSummaryText();
            SetWrappedButtonContent(
                QrLinksToggleButton,
                _isQrLinksExpanded
                    ? T("PreviewQrCollapseLinks")
                    : T("PreviewQrExpandLinks"));

            QrLinksHost.Children.Clear();

            foreach (var item in _qrUrlItems)
            {
                QrLinksHost.Children.Add(BuildQrUrlRow(item, QrLinksHost.Children.Count + 1));
            }

            RefreshRecognitionLayoutBounds();
        }

        private void RefreshRecognitionLayoutBounds()
        {
            var contentHeight = GetRecognitionContentAvailableHeight();
            if (contentHeight <= 0)
            {
                return;
            }

            if (QrResultsSection.IsVisible)
            {
                QrResultsScrollViewer.MaxHeight = Math.Max(RecognitionTextMinHeight, contentHeight - 40d);
            }
            else
            {
                QrResultsScrollViewer.MaxHeight = double.PositiveInfinity;
            }

            if (!QrLinksSection.IsVisible)
            {
                OcrTextBox.MaxHeight = Math.Max(RecognitionTextMinHeight, contentHeight);
                QrLinksSection.MaxHeight = double.PositiveInfinity;
                QrLinksPanel.MaxHeight = double.PositiveInfinity;
                QrLinksScrollViewer.MaxHeight = double.PositiveInfinity;
                return;
            }

            var qrLinksSectionMaxHeight = GetQrLinksSectionMaxHeight(contentHeight);
            var qrLinksPanelMaxHeight = GetAvailableQrLinksPanelHeight(qrLinksSectionMaxHeight);
            var reservedSpacing = 10d;
            var qrLinksSectionCurrentHeight = GetQrLinksSectionCurrentHeight(contentHeight);
            var textMaxHeight = Math.Max(RecognitionTextMinHeight, contentHeight - qrLinksSectionCurrentHeight - reservedSpacing);

            OcrTextBox.MaxHeight = textMaxHeight;
            QrLinksSection.MaxHeight = qrLinksSectionMaxHeight;
            QrLinksPanel.MaxHeight = qrLinksPanelMaxHeight;
            QrLinksScrollViewer.MaxHeight = qrLinksPanelMaxHeight;

            if (_isQrLinksExpanded && QrLinksPanel.IsVisible)
            {
                QrLinksPanel.Height = Math.Min(QrLinksPanel.Height, GetTargetQrLinksPanelHeight());
            }
        }

        private double GetRecognitionContentAvailableHeight()
        {
            var contentHeight = RecognitionContentBorder.Bounds.Height;
            if (contentHeight > 0)
            {
                return contentHeight;
            }

            var panelHeight = OcrPanel.Bounds.Height;
            if (panelHeight > 0)
            {
                return Math.Max(RecognitionTextMinHeight, panelHeight - 150d);
            }

            return RecognitionContentFallbackHeight;
        }

        private double GetQrLinksSectionMaxHeight(double contentHeight)
        {
            var chromeHeight = GetQrLinksSectionChromeHeight();
            var minimumHeight = chromeHeight + QrLinksPanelMinHeight;
            var heightByRatio = Math.Min(QrLinksPanelMaxHeight + chromeHeight, contentHeight * QrLinksSectionMaxHeightRatio);
            var heightByRemainingSpace = Math.Max(minimumHeight, contentHeight - RecognitionTextMinHeight - 10d);
            return Math.Max(minimumHeight, Math.Min(heightByRatio, heightByRemainingSpace));
        }

        private double GetQrLinksSectionCurrentHeight(double contentHeight)
        {
            var chromeHeight = GetQrLinksSectionChromeHeight();
            if (!_isQrLinksExpanded)
            {
                return chromeHeight;
            }

            var sectionMaxHeight = GetQrLinksSectionMaxHeight(contentHeight);
            var panelHeight = Math.Min(GetTargetQrLinksPanelHeight(), GetAvailableQrLinksPanelHeight(sectionMaxHeight));
            return chromeHeight + panelHeight;
        }

        private double GetQrLinksSectionChromeHeight()
        {
            var summaryHeight = QrLinksSummaryText.Bounds.Height > 0
                ? QrLinksSummaryText.Bounds.Height
                : 40d;
            var toggleHeight = QrLinksToggleButton.Bounds.Height > 0
                ? QrLinksToggleButton.Bounds.Height
                : 36d;

            return summaryHeight + toggleHeight + QrLinksSectionSpacingHeight;
        }

        private double GetAvailableQrLinksPanelHeight(double qrLinksSectionMaxHeight)
        {
            return Math.Max(QrLinksPanelMinHeight, qrLinksSectionMaxHeight - GetQrLinksSectionChromeHeight());
        }

        private string GetQrLinksSummaryText()
        {
            if (_qrUrlItems.Count <= 0)
            {
                return string.Empty;
            }

            return _qrUrlItems.Count == 1
                ? T("PreviewQrLinksSummarySingle")
                : FormatT("PreviewQrLinksSummaryMultiple", _qrUrlItems.Count);
        }

        private static PreviewQrRecognition ToSingleQrRecognition(PreviewQrBatchRecognition result)
        {
            if (result.Results.Count > 0)
            {
                return new PreviewQrRecognition(result.Results[0].Content, true, result.ErrorMessage);
            }

            return string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? PreviewQrRecognition.Empty()
                : PreviewQrRecognition.Error(result.ErrorMessage);
        }

        private string GetQrResultsSummaryText()
        {
            return _qrResultItems.Count > 1
                ? FormatT("PreviewQrMultipleDetected", _qrResultItems.Count)
                : string.Empty;
        }

        private string BuildQrResultsCopyText(IReadOnlyList<PreviewQrCodeResult> results)
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

                builder.AppendLine(FormatT("PreviewQrItemTitle", index + 1));
                builder.Append(results[index].Content);
            }

            return builder.ToString();
        }

        private Control BuildQrResultCard(PreviewQrResultItem item, int displayIndex)
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
                Text = FormatT("PreviewQrItemTitle", displayIndex),
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

        private Control BuildQrResultLinkRow(PreviewToolLinkItem item, int displayIndex)
        {
            var border = new Border
            {
                Background = CreateQrRowBackgroundBrush(),
                BorderBrush = CreateQrRowBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10)
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                RowSpacing = 8
            };

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8
            };

            var indexText = new TextBlock
            {
                Text = $"{displayIndex}.",
                VerticalAlignment = VerticalAlignment.Top,
                FontWeight = FontWeight.SemiBold
            };

            var textStack = new StackPanel
            {
                Spacing = 3
            };

            var urlText = new TextBlock
            {
                Text = item.Url,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold
            };
            textStack.Children.Add(urlText);

            if (!item.UsesHttps)
            {
                var warningText = new TextBlock
                {
                    Text = T("PreviewQrLinkNotSecure"),
                    TextWrapping = TextWrapping.Wrap
                };
                warningText.Classes.Add("secondary");
                textStack.Children.Add(warningText);
            }

            contentGrid.Children.Add(indexText);
            Grid.SetColumn(indexText, 0);
            contentGrid.Children.Add(textStack);
            Grid.SetColumn(textStack, 1);

            var openButton = new Button
            {
                MinHeight = 36,
                Padding = new Thickness(10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SetWrappedButtonContent(openButton, T("PreviewQrOpenLink"));
            openButton.Click += (_, _) =>
            {
                OpenQrUrl(item.Url);
            };

            var copyButton = new Button
            {
                MinHeight = 36,
                Padding = new Thickness(10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SetWrappedButtonContent(copyButton, T("PreviewQrCopyLink"));
            copyButton.Click += async (_, _) =>
            {
                await CopyTextToClipboardAsync(item.Url);
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

            grid.Children.Add(contentGrid);
            Grid.SetRow(contentGrid, 0);
            grid.Children.Add(actionsGrid);
            Grid.SetRow(actionsGrid, 1);

            border.Child = grid;
            return border;
        }

        private Control BuildQrUrlRow(PreviewToolLinkItem item, int displayIndex)
        {
            var border = new Border
            {
                Background = CreateQrRowBackgroundBrush(),
                BorderBrush = CreateQrRowBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10)
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                RowSpacing = 10
            };

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8
            };

            var indexText = new TextBlock
            {
                Text = $"{displayIndex}.",
                VerticalAlignment = VerticalAlignment.Top,
                FontWeight = FontWeight.SemiBold
            };

            var textStack = new StackPanel
            {
                Spacing = 3
            };

            var urlText = new TextBlock
            {
                Text = item.Url,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold
            };

            textStack.Children.Add(urlText);

            if (!item.UsesHttps)
            {
                var warningText = new TextBlock
                {
                    Text = T("PreviewQrLinkNotSecure"),
                    TextWrapping = TextWrapping.Wrap
                };
                warningText.Classes.Add("secondary");
                textStack.Children.Add(warningText);
            }

            var openButton = new Button
            {
                MinHeight = 38,
                Padding = new Thickness(10, 6),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SetWrappedButtonContent(openButton, T("PreviewQrOpenLink"));

            openButton.Click += (_, _) =>
            {
                OpenQrUrl(item.Url);
            };

            var copyButton = new Button
            {
                MinHeight = 38,
                Padding = new Thickness(10, 6),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SetWrappedButtonContent(copyButton, T("PreviewQrCopyLink"));

            copyButton.Click += async (_, _) =>
            {
                await CopyTextToClipboardAsync(item.Url);
            };

            contentGrid.Children.Add(indexText);
            Grid.SetColumn(indexText, 0);

            contentGrid.Children.Add(textStack);
            Grid.SetColumn(textStack, 1);

            var actionsGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                ColumnSpacing = 8
            };

            actionsGrid.Children.Add(openButton);
            Grid.SetColumn(openButton, 0);

            actionsGrid.Children.Add(copyButton);
            Grid.SetColumn(copyButton, 1);

            grid.Children.Add(contentGrid);
            Grid.SetRow(contentGrid, 0);

            grid.Children.Add(actionsGrid);
            Grid.SetRow(actionsGrid, 1);

            border.Child = grid;
            return border;
        }

        private async Task SetQrLinksExpandedAsync(bool expanded)
        {
            if (_isClosed || !HasQrUrls)
            {
                return;
            }

            if (_isQrLinksExpanded == expanded &&
                (expanded ? QrLinksPanel.IsVisible : !QrLinksPanel.IsVisible))
            {
                return;
            }

            CancelQrLinksAnimation();
            var animationCts = new CancellationTokenSource();
            _qrLinksAnimationCts = animationCts;

            var startHeight = QrLinksPanel.Height;
            var targetHeight = expanded ? GetTargetQrLinksPanelHeight() : 0d;
            var startOpacity = QrLinksPanel.Opacity;
            var targetOpacity = expanded ? 1d : 0d;
            _isQrLinksExpanded = expanded;

            if (expanded)
            {
                QrLinksPanel.IsVisible = true;
            }

            RefreshQrLinksView();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                while (stopwatch.ElapsedMilliseconds < QrLinksAnimationDurationMilliseconds)
                {
                    animationCts.Token.ThrowIfCancellationRequested();

                    var progress = stopwatch.Elapsed.TotalMilliseconds / QrLinksAnimationDurationMilliseconds;
                    var eased = EaseInOut(Math.Clamp(progress, 0d, 1d));
                    QrLinksPanel.Height = Lerp(startHeight, targetHeight, eased);
                    QrLinksPanel.Opacity = Lerp(startOpacity, targetOpacity, eased);

                    await Task.Delay(15, animationCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                if (ReferenceEquals(_qrLinksAnimationCts, animationCts))
                {
                    _qrLinksAnimationCts = null;
                }

                animationCts.Dispose();
            }

            QrLinksPanel.Height = targetHeight;
            QrLinksPanel.Opacity = targetOpacity;
            QrLinksPanel.IsVisible = expanded;
            RefreshQrLinksView();
        }

        private void CancelQrLinksAnimation()
        {
            var cancellationSource = Interlocked.Exchange(ref _qrLinksAnimationCts, null);
            if (cancellationSource is null)
            {
                return;
            }

            try
            {
                cancellationSource.Cancel();
            }
            catch
            {
                // Ignore races while replacing link panel animations.
            }

            cancellationSource.Dispose();
        }

        private double GetTargetQrLinksPanelHeight()
        {
            var estimatedHeight = 32d + (_qrUrlItems.Count * 108d);
            var sectionMaxHeight = GetQrLinksSectionMaxHeight(GetRecognitionContentAvailableHeight());
            var availableHeight = GetAvailableQrLinksPanelHeight(sectionMaxHeight);
            return Math.Min(availableHeight, Math.Max(QrLinksPanelMinHeight, Math.Min(QrLinksPanelMaxHeight, estimatedHeight)));
        }

        private IBrush CreateQrRowBackgroundBrush()
        {
            return new SolidColorBrush(IsDarkTheme()
                ? Color.Parse("#242F3D")
                : Color.Parse("#F1F5FA"));
        }

        private IBrush CreateQrRowBorderBrush()
        {
            return new SolidColorBrush(IsDarkTheme()
                ? Color.Parse("#2E3B4C")
                : Color.Parse("#CBD5E2"));
        }

        private bool IsDarkTheme()
        {
            return ActualThemeVariant == ThemeVariant.Dark;
        }

        private void OpenQrUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to open QR link '{url}': {ex}");
            }
        }

        private async Task CopyTextToClipboardAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            await clipboard.SetTextAsync(text);
            ShowToast();
        }

        private async void OnQrClick(object? sender, RoutedEventArgs e)
        {
            await RecognizeCurrentQrPreviewAsync(revealBusyPanelImmediately: true);
        }

        private async void OnToggleQrLinksClick(object? sender, RoutedEventArgs e)
        {
            await SetQrLinksExpandedAsync(!_isQrLinksExpanded);
        }

        private void OnOpenQrLinkClick(object? sender, RoutedEventArgs e)
        {
            if (_qrUrlItems.Count == 1)
            {
                OpenQrUrl(_qrUrlItems[0].Url);
            }
        }
    }
}
