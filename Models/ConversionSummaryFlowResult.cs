namespace ImvixPro.Models
{
    public sealed record ConversionSummaryFlowResult(
        CompletionSummaryModel Summary,
        ConversionHistoryEntry HistoryEntry,
        ConversionSummaryDialogRequest? DialogRequest)
    {
        public bool HasDialogRequest => DialogRequest is not null;
    }
}
