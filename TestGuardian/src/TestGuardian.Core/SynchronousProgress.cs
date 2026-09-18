namespace TestGuardian.Core;

/// <summary>
/// Reports progress synchronously on the calling thread. Unlike <see cref="System.Progress{T}"/>,
/// which marshals <c>Report</c> calls through a captured <see cref="System.Threading.SynchronizationContext"/>
/// (or the thread pool when none exists), this makes no such guarantee-breaking hop — needed both for
/// tests asserting immediately after a call returns, and for the console app's live progress output,
/// which has no <see cref="System.Threading.SynchronizationContext"/> to marshal through anyway.
/// </summary>
public sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
