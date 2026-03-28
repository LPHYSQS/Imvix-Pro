namespace ImvixPro.Models
{
    public readonly record struct PdfSecuritySnapshot(
        bool IsEncrypted,
        bool IsUnlocked,
        PdfDocumentInfo DocumentInfo);
}
