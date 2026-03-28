using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace ImvixPro.Models
{
    public sealed record FileDetailDocument(
        string FileName,
        string FilePath,
        string FileTypeText,
        string PreviewDescription,
        Bitmap? PreviewImage,
        IReadOnlyList<FileDetailSection> Sections);
}
