namespace Ready4Balfolk.Domain.Exceptions;

public class TrackInformationDiscoveryException : Exception
{
    public TrackInformationDiscoveryException()
    {
    }

    public TrackInformationDiscoveryException(string? message) : base(message)
    {
    }

    public TrackInformationDiscoveryException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
