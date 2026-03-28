using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PsdModule;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.ViewModels
{
    public sealed partial class FileDetailViewModel : ViewModelBase, IDisposable
    {
        private readonly ImageItemViewModel _item;
        private readonly FileDetailService _detailService;
        private readonly AppLogger _logger;
        private readonly SemaphoreSlim _loadGate = new(1, 1);
        private FileDetailDocument? _document;
        private bool _isDisposed;

        public FileDetailViewModel(ImageItemViewModel item, FileDetailViewModelServices services)
            : this(
                item,
                (services ?? throw new ArgumentNullException(nameof(services))).DetailService,
                services.Logger)
        {
        }

        internal FileDetailViewModel(ImageItemViewModel item, FileDetailService detailService, AppLogger logger)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _detailService = detailService ?? throw new ArgumentNullException(nameof(detailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            FileName = item.FileName;
            FilePath = item.FilePath;
            FileTypeText = $".{item.Extension}";
            _item.PropertyChanged += OnItemPropertyChanged;
        }

        public string PreviewTitleText => T("FileDetailPreviewTitle");

        public string LoadingText => T("FileDetailLoading");

        public string NoPreviewText => T("FileDetailNoPreview");

        public string CopyInfoText => T("FileDetailCopyInfo");

        public string CloseText => T("Close");

        public string LoadFailedText => T("FileDetailLoadFailed");

        public bool IsPsdDetail => PsdImportService.IsPsdFile(_item.FilePath);

        public bool ShowPsdSections => IsPsdDetail;

        public bool ShowGenericSections => !IsPsdDetail;

        public bool HasPreview => PreviewImage is not null;

        public bool IsPreviewMissing => !IsLoading && PreviewImage is null;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool CanCopyInfo => _document is not null;

        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private string fileTypeText = string.Empty;

        [ObservableProperty]
        private Bitmap? previewImage;

        [ObservableProperty]
        private string previewDescription = string.Empty;

        [ObservableProperty]
        private IReadOnlyList<FileDetailSection> sections = [];

        [ObservableProperty]
        private bool isLoading = true;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        public async Task LoadAsync()
        {
            await LoadCoreAsync(forceReload: false);
        }

        public string? BuildCopyText()
        {
            return _document is null ? null : _detailService.BuildCopyText(_document);
        }

        public void Dispose()
        {
            _isDisposed = true;
            _item.PropertyChanged -= OnItemPropertyChanged;
            PreviewImage?.Dispose();
            PreviewImage = null;
        }

        partial void OnPreviewImageChanged(Bitmap? value)
        {
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(IsPreviewMissing));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsPreviewMissing));
        }

        partial void OnErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasError));
        }

        private string T(string key)
        {
            return _detailService.Translate(key);
        }

        private async Task LoadCoreAsync(bool forceReload)
        {
            await _loadGate.WaitAsync();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                if (!forceReload && _document is not null && !IsLoading)
                {
                    return;
                }

                IsLoading = true;
                ErrorMessage = string.Empty;

                try
                {
                    var document = await Task.Run(() => _detailService.Load(_item));
                    if (_isDisposed)
                    {
                        document.PreviewImage?.Dispose();
                        return;
                    }

                    var previousPreview = PreviewImage;

                    _document = document;
                    FileName = document.FileName;
                    FilePath = document.FilePath;
                    FileTypeText = document.FileTypeText;
                    PreviewDescription = document.PreviewDescription;
                    Sections = document.Sections;
                    PreviewImage = document.PreviewImage;

                    if (previousPreview is not null && !ReferenceEquals(previousPreview, PreviewImage))
                    {
                        previousPreview.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(nameof(FileDetailViewModel), $"Failed to load details for '{_item.FilePath}'.", ex);
                    _document = null;
                    PreviewDescription = string.Empty;
                    Sections = [];
                    PreviewImage?.Dispose();
                    PreviewImage = null;
                    ErrorMessage = LoadFailedText;
                }
                finally
                {
                    IsLoading = false;
                    OnPropertyChanged(nameof(HasPreview));
                    OnPropertyChanged(nameof(IsPreviewMissing));
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(CanCopyInfo));
                }
            }
            finally
            {
                _loadGate.Release();
            }
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isDisposed || !_item.IsPdfDocument)
            {
                return;
            }

            if (e.PropertyName is not (nameof(ImageItemViewModel.IsEncrypted) or nameof(ImageItemViewModel.IsUnlocked) or nameof(ImageItemViewModel.PdfPageCount)))
            {
                return;
            }

            _ = LoadCoreAsync(forceReload: true);
        }
    }
}
