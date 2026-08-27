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

    /// <summary>How long a token stays usable without being seen again.</summary>
    /// <remarks>
    /// Slid forward on every use rather than fixed from issue, and long enough to cover a whole
    /// evening including the reconnects a phone makes whenever it sleeps: being asked for the PIN
    /// again mid-bal is the interruption this is meant to avoid. What it does end is the token from
    /// some other night still opening the queue because nobody thought to change the PIN.
    /// </remarks>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, Attempts> _attempts = new(StringComparer.Ordinal);
    // The value is when the token stops being usable, not when it was issued. It used to be the
    // issue time, which nothing ever read, so a token lived until the PIN changed.
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

        _tokens[token] = now + TokenLifetime;
        Prune(now);
        return RemoteLoginResult.Granted(token);
    }

    /// <summary>Whether a hub connection may proceed.</summary>
    public bool IsTokenValid(string? token)
    {
        if (!IsEnabled || token is null)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();

        if (!_tokens.TryGetValue(token, out var expiresAt) || expiresAt <= now)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }

        _tokens[token] = now + TokenLifetime;
        return true;
    }

    /// <summary>
    /// Drops what has aged out of both dictionaries.
    /// </summary>
    /// <remarks>
    /// Called from the login path only, which is the rare one, and safe against a login racing it:
    /// an attempts entry is only ever removed while it holds no failures and no live lockout, so
    /// there is nothing a race could reset. Without this, both grew for the life of the process,
    /// one entry per login and one per address that ever guessed wrong.
    /// </remarks>
    private void Prune(DateTimeOffset now)
    {
        foreach (var (token, expiresAt) in _tokens)
        {
            if (expiresAt <= now)
            {
                _tokens.TryRemove(token, out _);
            }
        }

        foreach (var (client, attempts) in _attempts)
        {
            lock (attempts)
            {
                if (attempts.Failed == 0 && attempts.LockedUntil <= now)
                {
                    _attempts.TryRemove(client, out _);
                }
            }
        }
    }

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
