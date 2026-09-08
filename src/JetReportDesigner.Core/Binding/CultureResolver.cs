using System.Globalization;

namespace JetReportDesigner.Core.Binding;

/// <summary>Turns a report's culture name into a <see cref="CultureInfo"/>.</summary>
public static class CultureResolver
{
    /// <summary>
    /// The named culture, or the render process's current culture when the name is
    /// unset or not a recognised culture.
    /// </summary>
    public static CultureInfo Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CultureInfo.CurrentCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(name.Trim());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
        }
    }
}
