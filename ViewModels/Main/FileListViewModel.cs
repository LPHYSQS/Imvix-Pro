using CommunityToolkit.Mvvm.Input;
using ImvixPro.Models;
using ImvixPro.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ImvixPro.ViewModels
{
    public partial class MainWindowViewModel
    {
        public void AddFiles(IEnumerable<string> paths)
        {
            var candidates = ExpandInputPaths(paths, IncludeSubfoldersOnFolderImport, _logger)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreateReadyRuntimeStatus());
                RefreshConversionInsights();
                return;
            }

            QueueAddFiles(candidates);
        }

        private static IEnumerable<string> ExpandInputPaths(
            IEnumerable<string> paths,
            bool includeSubfolders,
            AppLogger logger)
        {
            foreach (var rawPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(rawPath);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(nameof(MainWindowViewModel), $"Ignored invalid input path '{rawPath}'.", ex);
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    yield return fullPath;
                    continue;
                }

                if (!Directory.Exists(fullPath))
                {
                    continue;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(
                        fullPath,
                        "*.*",
                        includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(nameof(MainWindowViewModel), $"Failed to enumerate files under '{fullPath}'.", ex);
                    continue;
                }

                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file);
                    if (ImageConversionService.SupportedInputExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        yield return file;
                    }
                }
            }
        }

        public void SetOutputDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            OutputDirectory = path;
            UseSourceFolder = false;
        }

        [RelayCommand(CanExecute = nameof(CanClearImages))]
        private void ClearImages()
        {
            InvalidatePendingImports();
            CancelPendingSelectedPsdPreviewRender();
            SelectedImage = null;
            _gifSpecificFrameSelections.Clear();
            _gifTrimSelections.Clear();
            _pdfPageSelections.Clear();
            _pdfPageRanges.Clear();

            foreach (var image in Images)
            {
                if (image.IsPdfDocument)
                {
                    _pdfSecurityService.ClearSession(image.FilePath);
                }

                image.Dispose();
            }

            Images.Clear();
            FailedConversions.Clear();
            NotificationState.ResetFailureLog();
            ApplyManualRuntimeStatus(_conversionStatusSummaryService.CreateReadyRuntimeStatus());
            RefreshConversionInsights();
        }

        [RelayCommand]
        private void RemoveImage(ImageItemViewModel? image)
        {
            if (image is null)
            {
                return;
            }

            var wasSelected = SelectedImage == image;
            if (!Images.Remove(image))
            {
                return;
            }

            _gifSpecificFrameSelections.Remove(image.FilePath);
            _gifTrimSelections.Remove(image.FilePath);
            _pdfPageSelections.Remove(image.FilePath);
            _pdfPageRanges.Remove(image.FilePath);
            if (image.IsPdfDocument)
            {
                _pdfSecurityService.ClearSession(image.FilePath);
            }

            image.Dispose();

            if (wasSelected)
            {
                SelectedImage = Images.FirstOrDefault();
            }

            RefreshConversionInsights();
        }

        [RelayCommand(CanExecute = nameof(CanSavePreset))]
        private void SavePreset()
        {
            var name = (PresetNameInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var preset = BuildPreset(name);
            var existing = Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Presets.Add(preset);
                SelectedPreset = preset;
            }
            else
            {
                var index = Presets.IndexOf(existing);
                if (index >= 0)
                {
                    Presets[index] = preset;
                    SelectedPreset = preset;
                }
            }

            PersistSettings();
        }

        [RelayCommand(CanExecute = nameof(CanApplyPreset))]
        private void ApplyPreset()
        {
            if (SelectedPreset is null)
            {
                return;
            }

            _isLoadingSettings = true;

            SelectedOutputFormat = SelectedPreset.OutputFormat;
            SelectedCompressionMode = SelectedPreset.CompressionMode;
            Quality = Math.Clamp(SelectedPreset.Quality, 1, 100);
            SelectedResizeMode = SelectedPreset.ResizeMode;
            ResizeWidth = Math.Max(1, SelectedPreset.ResizeWidth);
            ResizeHeight = Math.Max(1, SelectedPreset.ResizeHeight);
            ResizePercent = Math.Clamp(SelectedPreset.ResizePercent, 1, 1000);
            SelectedRenameMode = SelectedPreset.RenameMode;
            RenamePrefix = SelectedPreset.RenamePrefix;
            RenameSuffix = SelectedPreset.RenameSuffix;
            RenameStartNumber = Math.Max(0, SelectedPreset.RenameStartNumber);
            RenameNumberDigits = Math.Clamp(SelectedPreset.RenameNumberDigits, 1, 8);
            SelectedGifHandlingMode = SelectedPreset.GifHandlingMode;
            SelectedGifSpecificFrameIndex = Math.Max(0, SelectedPreset.GifSpecificFrameIndex);
            AiEnhancementEnabled = SelectedPreset.AiEnhancementEnabled;
            AiEnhancementScale = AiEnhancementModelCatalog.NormalizeRequestedOutputScale(SelectedPreset.AiEnhancementScale);
            SelectedAiEnhancementModel = SelectedPreset.AiEnhancementModel;
            SelectedAiExecutionMode = SelectedPreset.AiExecutionMode;
            OutputDirectory = SelectedPreset.OutputDirectory;
            UseSourceFolder = SelectedPreset.OutputDirectoryRule == OutputDirectoryRule.SourceFolder;
            AllowOverwrite = SelectedPreset.AllowOverwrite;
            SvgUseBackground = SelectedPreset.SvgUseBackground;
            SvgBackgroundColor = string.IsNullOrWhiteSpace(SelectedPreset.SvgBackgroundColor)
                ? "#FFFFFFFF"
                : SelectedPreset.SvgBackgroundColor;
            IconUseTransparency = SelectedPreset.IconUseTransparency;
            IconBackgroundColor = string.IsNullOrWhiteSpace(SelectedPreset.IconBackgroundColor)
                ? "#FFFFFFFF"
                : SelectedPreset.IconBackgroundColor;

            _isLoadingSettings = false;

            OnPropertyChanged(nameof(IsQualityEditable));
            OnPropertyChanged(nameof(IsResizeWidthVisible));
            OnPropertyChanged(nameof(IsResizeHeightVisible));
            OnPropertyChanged(nameof(IsResizePercentVisible));
            OnPropertyChanged(nameof(IsRenamePrefixVisible));
            OnPropertyChanged(nameof(IsRenameSuffixVisible));
            OnPropertyChanged(nameof(IsRenameNumberVisible));

            PersistSettings();
        }

        [RelayCommand(CanExecute = nameof(CanDeletePreset))]
        private void DeletePreset()
        {
            if (SelectedPreset is null)
            {
                return;
            }

            var target = SelectedPreset;
            SelectedPreset = null;
            Presets.Remove(target);
            PersistSettings();
        }

        [RelayCommand]
        private void OpenSettingsPanel()
        {
            RightPanelTabIndex = 1;
        }
    }
}


