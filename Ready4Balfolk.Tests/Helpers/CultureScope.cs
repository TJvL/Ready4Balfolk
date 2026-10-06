using System.Globalization;

namespace Ready4Balfolk.Tests.Helpers;

/// <summary>A machine set to one culture running the application in another, for one test.</summary>
/// <remarks>
/// The application's language setting moves the UI culture and leaves the machine's culture alone,
/// so a test of what follows the language sets the two apart on purpose: on a build agent they are
/// usually the same, and a test that only passes because they are proves nothing. Both are put
/// back on dispose. They belong to the running thread's flow, so a test in parallel is untouched.
/// </remarks>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public CultureScope(string machine, string application)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(machine);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(application);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
