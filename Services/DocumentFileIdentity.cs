namespace Inlay;

internal static class DocumentFileIdentity
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool Matches(IDocumentFile? file, string identity) =>
        file is not null && Comparer.Equals(file.Identity, identity);
}
