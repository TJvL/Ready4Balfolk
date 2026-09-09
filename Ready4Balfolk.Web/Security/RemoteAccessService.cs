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

    /// <summary>
    /// Applies the current settings, dropping every issued token when the PIN changed or the remote
    /// went off.
    /// </summary>
    /// <returns>
    /// Whether the tokens were dropped, which is the caller's cue to close the sockets that were
    /// opened with them. Clearing the dictionary only stops the next command being let through:
    /// a phone that sends nothing goes on being pushed the queue until it happens to press
    /// something, and getting that phone out is why the PIN was changed.
    /// </returns>
    public bool Configure(bool enabled, string pin)
    {
        var pinChanged = !string.Equals(_pin, pin, StringComparison.Ordinal);
        IsEnabled = enabled;
        _pin = pin;

        if (!pinChanged && enabled)
        {
            return false;
        }

        _tokens.Clear();
        _attempts.Clear();
        return true;
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
        var attempts = _attempts.GetValueOrDefault(clientKey);

        if (attempts is { } held && held.LockedUntil > now)
        {
            return RemoteLoginResult.LockedOut((held.LockedUntil - now).TotalSeconds);
        }

        // Fixed-time comparison: a PIN is short enough that a timing side channel is not
        // theoretical, and the comparison costs nothing.
        var ok = _pin.Length > 0 && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(_pin),
            Encoding.UTF8.GetBytes(pin ?? string.Empty));

        if (!ok)
        {
            // Counted against whatever the entry holds by the time the count lands, not against
            // the one read above. A prune can have taken the entry in between, and the strike then
            // starts a fresh one rather than landing on a record nothing holds any more.
            var struck = _attempts.AddOrUpdate(
                clientKey,
                _ => Attempts.None.Struck(now),
                (_, current) => current.Struck(now));

            return struck.LockedUntil > now
                ? RemoteLoginResult.LockedOut((struck.LockedUntil - now).TotalSeconds)
                : RemoteLoginResult.Rejected;
        }

        // The right PIN wipes the strikes, and only the strikes that were read: an address locked
        // out by another guess in the meantime stays locked.
        if (attempts is not null)
        {
            _attempts.TryRemove(KeyValuePair.Create(clientKey, attempts));
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
    /// Called from the login path only, which is the rare one. An attempts entry is removed only
    /// while it is still the one that was judged, holding no strikes and no live lockout: a strike
    /// makes a new entry rather than changing the old one, so a guess that lands between the
    /// judging and the removing leaves the remove with nothing to match and the strike stands.
    /// Without this, both grew for the life of the process, one entry per login and one per
    /// address that ever guessed wrong.
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
            if (attempts.IsIdle(now))
            {
                _attempts.TryRemove(KeyValuePair.Create(client, attempts));
            }
        }
    }

    /// <summary>
    /// What an address has done wrong lately. Never changed in place: a strike makes a new one,
    /// which is what lets the prune remove only an entry that is still the one it judged.
    /// </summary>
    private sealed class Attempts(int failed, DateTimeOffset lockedUntil)
    {
        public static readonly Attempts None = new(0, DateTimeOffset.MinValue);

        public int Failed { get; } = failed;

        public DateTimeOffset LockedUntil { get; } = lockedUntil;

        public bool IsIdle(DateTimeOffset now) => Failed == 0 && LockedUntil <= now;

        /// <summary>One more wrong guess, or the lockout it tipped the address into.</summary>
        public Attempts Struck(DateTimeOffset now)
        {
            if (LockedUntil > now)
            {
                // A guess during a lockout is refused before it is counted, and one that slipped in
                // beside the guess that locked the address must not restart the count.
                return this;
            }

            return Failed + 1 >= MaxAttempts
                ? new Attempts(0, now + LockoutDuration)
                : new Attempts(Failed + 1, LockedUntil);
        }
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
