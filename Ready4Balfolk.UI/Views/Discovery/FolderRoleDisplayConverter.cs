using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Discovery;

public sealed class FolderRoleDisplayConverter : IValueConverter
{
    public static readonly FolderRoleDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FolderRole role
            ? role switch
            {
                FolderRole.Artist => UiStrings.Discovery_RoleArtist,
                FolderRole.Album => UiStrings.Discovery_RoleAlbum,
                FolderRole.Dance => UiStrings.Discovery_RoleDance,
                FolderRole.Ignore => UiStrings.Discovery_RoleIgnore,
                _ => UiStrings.Discovery_RoleUnknown
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
