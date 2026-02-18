namespace Ready4Balfolk.Domain;

public static class SupportedAudioFormats
{
    public static IReadOnlySet<string> Extensions { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path));

    public static void Initialize(IReadOnlySet<string> extensions) =>
        Extensions = extensions;
}
