namespace OpsManager.Domain.Common;

public class DomainInvariantException(string message) : InvalidOperationException(message);

public sealed class InvalidStateTransitionException(string aggregate, string from, string to)
    : DomainInvariantException($"{aggregate} cannot transition from '{from}' to '{to}'.");
