using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TORISOUP.SequentialTaskExecutors
{
    public sealed class SequentialTaskExecutor<T> : IDisposable
    {
        private readonly Channel<AsyncAction<T>> _channel;
        private readonly ChannelWriter<AsyncAction<T>> _writer;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly object _gate = new();

        private bool _isDisposed;
        private bool _isExecuting;

        public SequentialTaskExecutor()
        {
            _channel = Channel.CreateSingleConsumerUnbounded<AsyncAction<T>>();
            _writer = _channel.Writer;
        }

        public void Execute()
        {
            lock (_gate)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SequentialTaskExecutor<T>));
                if (_isExecuting) return;
                _isExecuting = true;
            }

            ExecuteLoopAsync(_cancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid ExecuteLoopAsync(CancellationToken ct)
        {
            await foreach (var action in _channel.Reader.ReadAllAsync())
            {
                if (ct.IsCancellationRequested)
                {
                    action.AutoResetUniTaskCompletionSource.TrySetCanceled(ct);
                    continue;
                }

                try
                {
                    if (action.CancellationToken.IsCancellationRequested) continue;

                    var token = ct;
                    if (action.CancellationToken != CancellationToken.None)
                    {
                        token = CancellationTokenSource.CreateLinkedTokenSource(action.CancellationToken, ct).Token;
                    }

                    var result = await action.Action(token);
                    action.AutoResetUniTaskCompletionSource.TrySetResult(result);
                }
                catch (OperationCanceledException ex)
                {
                    action.AutoResetUniTaskCompletionSource.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception e)
                {
                    action.AutoResetUniTaskCompletionSource.TrySetException(e);
                }
            }
        }

        public UniTask<T> RegisterAsync(Func<CancellationToken, UniTask<T>> func,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SequentialTaskExecutor<T>));
            }

            var autoResetUniTaskCompletionSource = AutoResetUniTaskCompletionSource<T>.Create();
            cancellationToken.Register(() => autoResetUniTaskCompletionSource.TrySetCanceled(cancellationToken));
            _writer.TryWrite(new AsyncAction<T>(func, autoResetUniTaskCompletionSource, cancellationToken));
            return autoResetUniTaskCompletionSource.Task;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_isDisposed) return;

                try
                {
                    _writer.TryComplete();
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                }
                finally
                {
                    _isDisposed = true;
                }
            }
        }


        private readonly struct AsyncAction<TU>
        {
            public Func<CancellationToken, UniTask<TU>> Action { get; }
            public AutoResetUniTaskCompletionSource<TU> AutoResetUniTaskCompletionSource { get; }
            public CancellationToken CancellationToken { get; }

            public AsyncAction(Func<CancellationToken, UniTask<TU>> action,
                AutoResetUniTaskCompletionSource<TU> autoResetUniTaskCompletionSource,
                CancellationToken cancellationToken)
            {
                Action = action;
                AutoResetUniTaskCompletionSource = autoResetUniTaskCompletionSource;
                CancellationToken = cancellationToken;
            }
        }
    }
}