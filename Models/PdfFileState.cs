namespace ImvixPro.Models
{
    public readonly record struct PdfFileState(bool IsEncrypted, bool IsUnlocked)
    {
        public bool NeedsUnlock => IsEncrypted && !IsUnlocked;
    }
}
