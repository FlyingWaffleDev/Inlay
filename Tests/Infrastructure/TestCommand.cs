namespace Inlay.Tests;

internal static class TestCommand
{
    public static void Execute<T>(IObservable<T> command) =>
        command.Subscribe(new ImmediateObserver<T>());

    public static Task ExecuteAsync<T>(
        IObservable<T> command,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        command.Subscribe(new CompletionObserver<T>(completion, registration));
        return completion.Task;
    }

    private sealed class ImmediateObserver<T> : IObserver<T>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void OnNext(T value) { }
    }

    private sealed class CompletionObserver<T>(
        TaskCompletionSource completion,
        CancellationTokenRegistration registration) : IObserver<T>
    {
        public void OnCompleted()
        {
            registration.Dispose();
            completion.TrySetResult();
        }

        public void OnError(Exception error)
        {
            registration.Dispose();
            completion.TrySetException(error);
        }

        public void OnNext(T value) { }
    }
}
