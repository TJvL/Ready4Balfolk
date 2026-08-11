using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ready4Balfolk.Web.Security;

/// <summary>
/// Guards the remote: a PIN is exchanged once for a connection token, and the hub checks the token.
/// </summary>
/// <remarks>
/// Checking the PIN only on the page that serves the form would leave the hub open, since anyone on
/// the network can open a socket directly without ever loading the page.
/// </remarks>
public sealed class RemoteAccessService(TimeProvider? timeProvider = null)
{
    /// <summary>Failed attempts from one address before it is locked out.</summary>
    private const int MaxAttempts = 5;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Attempts> _attempts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private string _pin = string.Empty;

    /// <summary>Whether the remote is switched on at all.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Applies the current settings, dropping every issued token if the PIN changed.</summary>
    public void Configure(bool enabled, string pin)
    {
        var pinChanged = !string.Equals(_pin, pin, StringComparison.Ordinal);
        IsEnabled = enabled;
        _pin = pin;

        if (pinChanged || !enabled)
        {
            _tokens.Clear();
            _attempts.Clear();
        }
    }

    /// <summary>Generates a PIN. Six digits, from a cryptographic source rather than Random.</summary>
    public static string GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>Exchanges a PIN for a token, or explains why not.</summary>
    public RemoteLoginResult TryLogin(string? pin, string clientKey)
    {
        if (!IsEnabled)
        {
            return RemoteLoginResult.Disabled;
        }

        var now = _timeProvider.GetUtcNow();
        var attempts = _attempts.GetOrAdd(clientKey, _ => new Attempts());

        lock (attempts)
        {
            if (attempts.LockedUntil > now)
            {
                return RemoteLoginResult.LockedOut((attempts.LockedUntil - now).TotalSeconds);
            }

            // Fixed-time comparison: a PIN is short enough that a timing side channel is not
            // theoretical, and the comparison costs nothing.
            var ok = _pin.Length > 0 && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(_pin),
                Encoding.UTF8.GetBytes(pin ?? string.Empty));

            if (!ok)
            {
                attempts.Failed++;
                if (attempts.Failed >= MaxAttempts)
                {
                    attempts.Failed = 0;
                    attempts.LockedUntil = now + LockoutDuration;
                    return RemoteLoginResult.LockedOut(LockoutDuration.TotalSeconds);
                }

                return RemoteLoginResult.Rejected;
            }

            attempts.Failed = 0;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

        _tokens[token] = now;
        return RemoteLoginResult.Granted(token);
    }

    /// <summary>Whether a hub connection may proceed.</summary>
    public bool IsTokenValid(string? token) =>
        IsEnabled && token is not null && _tokens.ContainsKey(token);

    private sealed class Attempts
    {
        public int Failed;
        public DateTimeOffset LockedUntil;
    }
}

/// <summary>The outcome of exchanging a PIN for a token.</summary>
public sealed record RemoteLoginResult(bool IsGranted, string? Token, string Status, double RetryAfterSeconds)
{
    public static readonly RemoteLoginResult Disabled = new(false, null, "disabled", 0);
    public static readonly RemoteLoginResult Rejected = new(false, null, "rejected", 0);

    public static RemoteLoginResult LockedOut(double seconds) => new(false, null, "locked", seconds);

    public static RemoteLoginResult Granted(string token) => new(true, token, "granted", 0);
}
