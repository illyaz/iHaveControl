namespace iHaveControl
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Source: https://stackoverflow.com/a/64867741
    /// </summary>
    public static class Debouncer
    {
        static ConcurrentDictionary<string, CancellationTokenSource> _tokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        public static void Debounce(string uniqueKey, Action action, int millisecondsDelay)
        {
            var token = _tokens.AddOrUpdate(uniqueKey,
                (key) => //key not found - create new
            {
                    return new CancellationTokenSource();
                },
                (key, existingToken) => //key found - cancel task and recreate
            {
                    existingToken.Cancel(); //cancel previous
                return new CancellationTokenSource();
                }
            );

            Task.Delay(millisecondsDelay, token.Token).ContinueWith(task =>
            {
                if (!task.IsCanceled)
                {
                    action();
                    _tokens.TryRemove(uniqueKey, out _);
                }
            }, token.Token);
        }
    }
}
