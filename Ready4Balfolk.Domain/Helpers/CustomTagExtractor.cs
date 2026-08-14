using TagLib;

namespace Ready4Balfolk.Domain.Helpers;

public static class CustomTagExtractor
{
    public static string[]? GetCustomTag(this TagLib.File file, string key) => ExtractId3v2(file, key) ?? ExtractXiphTag(file, key);

    private static string[]? ExtractId3v2(TagLib.File file, string key)
    {
        if (file.GetTag(TagTypes.Id3v2, false) is not TagLib.Id3v2.Tag id3v2)
        {
            return null;
        }

        var frame = TagLib.Id3v2.UserTextInformationFrame.Get(id3v2, key, false);
        return frame?.Text;
    }

    private static string[]? ExtractXiphTag(TagLib.File file, string key)
    {
        var xiph = file.GetTag(TagTypes.Xiph, false) as TagLib.Ogg.XiphComment;
        var values = xiph?.GetField(key);
        return values switch
        {
            { Length: > 0 } => values,
            _ => null
        };
    }
}
