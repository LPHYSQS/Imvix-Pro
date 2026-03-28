namespace ImvixPro.Models
{
    public readonly record struct PdfUnlockAttemptResult(bool Succeeded, string ErrorMessage)
    {
        public static PdfUnlockAttemptResult Success()
        {
            return new PdfUnlockAttemptResult(true, string.Empty);
        }

        public static PdfUnlockAttemptResult Failure(string errorMessage)
        {
            return new PdfUnlockAttemptResult(false, errorMessage ?? string.Empty);
        }
    }
}
