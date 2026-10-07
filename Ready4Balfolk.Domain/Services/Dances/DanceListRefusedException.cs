namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>A dance list that was read and refused, with why in both of the texts that is said in.</summary>
/// <remarks>
/// The message is English and goes to the log, like every exception's. What the DJ reads is
/// <see cref="ScreenText" />, which comes from the resx files: a refusal is shown on screen, and the
/// log and the screen are kept apart rather than one being made to serve as the other.
/// </remarks>
public sealed class DanceListRefusedException : Exception
{
    public DanceListRefusedException()
    {
        ScreenText = "";
    }

    public DanceListRefusedException(string message) : base(message)
    {
        ScreenText = "";
    }

    public DanceListRefusedException(string message, Exception innerException) : base(message, innerException)
    {
        ScreenText = "";
    }

    public DanceListRefusedException(string logLine, string screenText, Exception? innerException = null)
        : base(logLine, innerException)
    {
        ScreenText = screenText;
    }

    /// <summary>Why the list was refused, in the DJ's language.</summary>
    public string ScreenText { get; }
}
