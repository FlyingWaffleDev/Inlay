namespace Inlay.Models;

internal static class DocumentPartValidation
{
    public static string? FindError(IReadOnlyList<DocumentPart>? content)
    {
        if (content is null)
        {
            return "The document is missing its content collection.";
        }

        foreach (var part in content)
        {
            if (part is null)
            {
                return "A document part cannot be null.";
            }

            if (!Enum.IsDefined(part.Type))
            {
                return "A document part has an invalid type.";
            }

            if (part.Type == DocumentPartKind.Text && part.Text is null)
            {
                return "A text part is missing its text value.";
            }

            if (part.Type == DocumentPartKind.Template)
            {
                var count = part.Options?.Count ?? 0;
                var selectedIndex = part.SelectedIndex ?? -1;
                if (selectedIndex < -1 || selectedIndex >= count)
                {
                    return "A template has an invalid selectedIndex value.";
                }

                if (part.Options?.Exists(string.IsNullOrEmpty) == true)
                {
                    return "A template choice cannot be empty.";
                }
            }
        }

        return null;
    }
}
