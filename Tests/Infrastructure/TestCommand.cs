namespace Inlay.Tests;

internal static class TestCommand
{
    /// <summary>
    /// Runs a command that finishes without yielding. Commands that resume on the
    /// thread pool are not finished when this returns, so those are rejected here
    /// rather than left for the test to race.
    /// </summary>
    public static void Execute<T>(IObservable<T> command)
    {
        var observer = new ImmediateObserver<T>();
        using var subscription = command.Subscribe(observer);
        if (!observer.IsCompleted)
        {
            throw new InvalidOperationException(
                "The command did not complete synchronously. Await ExecuteAsync instead.");
        }
    }

    public static async Task ExecuteAsync<T>(
        IObservable<T> command,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        using var subscription = command.Subscribe(new CompletionObserver<T>(completion));
        await completion.Task;
    }

    private sealed class ImmediateObserver<T> : IObserver<T>
    {
        public bool IsCompleted { get; private set; }

        public void OnCompleted() => IsCompleted = true;
        public void OnError(Exception error) => throw error;
        public void OnNext(T value) { }
    }

    private sealed class CompletionObserver<T>(TaskCompletionSource completion) : IObserver<T>
    {
        public void OnCompleted() => completion.TrySetResult();
        public void OnError(Exception error) => completion.TrySetException(error);
        public void OnNext(T value) { }
    }
}
