using TagLib;

namespace Ready4Balfolk.Domain.Helpers;

/// <summary>Reads the free-form tags a file carries beyond the standard fields.</summary>
/// <remarks>
/// ID3v2 keeps these as TXXX frames with a description, Xiph/Vorbis as arbitrarily named fields.
/// They are gathered whole rather than looked up by one name, because which name means anything is
/// the user's to declare, and gathering must not depend on settings.
/// </remarks>
public static class CustomTagExtractor
{
    /// <summary>Every custom tag the file carries, keyed case-insensitively by its name.</summary>
    /// <remarks>The first value wins when a name appears in both tag formats.</remarks>
    public static IReadOnlyDictionary<string, string> GetCustomTags(this TagLib.File file)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (file.GetTag(TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3v2)
        {
            foreach (var frame in id3v2.GetFrames<TagLib.Id3v2.UserTextInformationFrame>())
            {
                var value = frame.Text.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
                if (!string.IsNullOrWhiteSpace(frame.Description) && value is not null)
                {
                    tags.TryAdd(frame.Description, value);
                }
            }
        }

        if (file.GetTag(TagTypes.Xiph, false) is TagLib.Ogg.XiphComment xiph)
        {
            foreach (var name in xiph)
            {
                var value = xiph.GetField(name).FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
                if (value is not null)
                {
                    tags.TryAdd(name, value);
                }
            }
        }

        return tags;
    }
}
