using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TORISOUP.SequentialTaskExecutors
{
    public sealed class SequentialTaskExecutor : IDisposable
    {
        private readonly Channel<AsyncAction> _channel;
        private readonly ChannelWriter<AsyncAction> _writer;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly object _gate = new();

        private bool _isDisposed;
        private bool _isExecuting;

        public SequentialTaskExecutor()
        {
            _channel = Channel.CreateSingleConsumerUnbounded<AsyncAction>();
            _writer = _channel.Writer;
        }

        public void Execute()
        {
            lock (_gate)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SequentialTaskExecutor));
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

                    await action.Action(token);
                    action.AutoResetUniTaskCompletionSource.TrySetResult();
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

        public UniTask RegisterAsync(Func<CancellationToken, UniTask> func,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SequentialTaskExecutor));
            }

            var autoResetUniTaskCompletionSource = AutoResetUniTaskCompletionSource.Create();
            cancellationToken.Register(() => autoResetUniTaskCompletionSource.TrySetCanceled(cancellationToken));
            _writer.TryWrite(new AsyncAction(func, autoResetUniTaskCompletionSource, cancellationToken));
            return autoResetUniTaskCompletionSource.Task;
        }

        public UniTask RegisterAsync<TV>(Func<TV, CancellationToken, UniTask> func,
            TV value,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(SequentialTaskExecutor));
            }

            var autoResetUniTaskCompletionSource = AutoResetUniTaskCompletionSource.Create();
            cancellationToken.Register(() => autoResetUniTaskCompletionSource.TrySetCanceled(cancellationToken));
            _writer.TryWrite(new AsyncAction(c => func(value, c), autoResetUniTaskCompletionSource,
                cancellationToken));
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


        private readonly struct AsyncAction
        {
            public Func<CancellationToken, UniTask> Action { get; }
            public AutoResetUniTaskCompletionSource AutoResetUniTaskCompletionSource { get; }
            public CancellationToken CancellationToken { get; }

            public AsyncAction(Func<CancellationToken, UniTask> action,
                AutoResetUniTaskCompletionSource autoResetUniTaskCompletionSource,
                CancellationToken cancellationToken)
            {
                Action = action;
                AutoResetUniTaskCompletionSource = autoResetUniTaskCompletionSource;
                CancellationToken = cancellationToken;
            }
        }
    }
}