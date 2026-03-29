using ImvixPro.Models;
using System;

namespace ImvixPro.Services
{
    public sealed class WatchProfilePlanningService
    {
        private readonly ConversionPlanningService _conversionPlanningService;

        public WatchProfilePlanningService(ConversionPlanningService conversionPlanningService)
        {
            _conversionPlanningService = conversionPlanningService ?? throw new ArgumentNullException(nameof(conversionPlanningService));
        }

        public WatchProfileSummary BuildSummary(WatchProfile watchProfile)
        {
            ArgumentNullException.ThrowIfNull(watchProfile);

            var options = BuildBaseOptions(watchProfile);
            var ruleSummary = _conversionPlanningService.BuildWatchRuleSummary(options);

            return new WatchProfileSummary(
                options.OutputFormat,
                watchProfile.OutputDirectory,
                options.AiEnhancementScale,
                options.ResizeMode,
                options.GifHandlingMode,
                options.AllowOverwrite,
                ruleSummary);
        }

        public ConversionOptions BuildExecutionOptions(WatchProfile watchProfile, ImageItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(watchProfile);
            ArgumentNullException.ThrowIfNull(item);

            var options = BuildBaseOptions(watchProfile);
            _conversionPlanningService.ApplyWatchScenarioRules(options, item);
            return options;
        }

        private static ConversionOptions BuildBaseOptions(WatchProfile watchProfile)
        {
            var options = watchProfile.JobDefinition?.ToConversionOptions() ?? new ConversionOptions();
            options.OutputDirectoryRule = OutputDirectoryRule.SpecificFolder;
            options.OutputDirectory = watchProfile.OutputDirectory;
            return options;
        }
    }
}
