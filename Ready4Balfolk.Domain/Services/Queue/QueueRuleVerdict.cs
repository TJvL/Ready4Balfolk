namespace Ready4Balfolk.Domain.Services.Queue;

public sealed record QueueRuleVerdict(bool Allowed, string? Reason = null);
