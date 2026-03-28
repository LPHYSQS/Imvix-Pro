using System.Collections.Generic;

namespace ImvixPro.Models
{
    public sealed record FileDetailSection(string Title, IReadOnlyList<FileDetailEntry> Entries);
}
