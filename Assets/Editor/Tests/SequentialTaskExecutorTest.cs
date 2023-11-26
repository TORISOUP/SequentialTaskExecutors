using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace TORISOUP.SequentialTaskExecutors.Tests
{
    public class SequentialTaskExecutorTest
    {
        [Test, Timeout(10000)]
        public async Task 登録したタスクが順番に実行される()
        {
            var executor = new SequentialTaskExecutor<int>();

            var results = new List<int>();

            var t1 = executor.RegisterAsync(async ct =>
            {
                await Task.Delay(100, ct);
                results.Add(1);
                return 1;
            });

            var t2 = executor.RegisterAsync(async ct =>
            {
                await Task.Delay(100, ct);
                results.Add(2);
                return 2;
            });
            var t3 = executor.RegisterAsync(async ct =>
            {
                await Task.Delay(100, ct);
                results.Add(3);
                return 3;
            });

            executor.Execute();

            var r2 = await UniTask.WhenAll(t1, t2, t3);

            Assert.AreEqual(new[] { 1, 2, 3 }, results.ToArray());
            Assert.AreEqual((1, 2, 3), r2);
        }

        [Test]
        public void 同時に実行するタスクは常に1つである()
        {
            var executor = new SequentialTaskExecutor<int>();
            var counter = 0;

            var a1 = AutoResetUniTaskCompletionSource.Create();
            var a2 = AutoResetUniTaskCompletionSource.Create();
            var a3 = AutoResetUniTaskCompletionSource.Create();

            var t1 = executor.RegisterAsync(async _ =>
            {
                try
                {
                    counter++;
                    await a1.Task;
                    return 1;
                }
                finally
                {
                    counter--;
                }
            });

            var t2 = executor.RegisterAsync(async _ =>
            {
                try
                {
                    counter++;
                    await a2.Task;
                    return 2;
                }
                finally
                {
                    counter--;
                }
            });
  

            // 開始前はゼロ
            Assert.AreEqual(0, counter);

            executor.Execute();

            // 1個だけ実行中
            Assert.AreEqual(1, counter);
            Assert.AreEqual(UniTaskStatus.Pending, t1.Status);
            a1.TrySetResult();
            Assert.AreEqual(UniTaskStatus.Succeeded, t1.Status);

            Assert.AreEqual(1, counter);
            Assert.AreEqual(UniTaskStatus.Pending, t2.Status);
            a2.TrySetResult();
            Assert.AreEqual(UniTaskStatus.Succeeded, t2.Status);

            // すべてのタスクを完遂したので実行数は0
            Assert.AreEqual(0, counter);
            
            // 新しいタスクを追加
            var t3 = executor.RegisterAsync(async _ =>
            {
                try
                {
                    counter++;
                    await a3.Task;
                    return 3;
                }
                finally
                {
                    counter--;
                }
            });
            
            Assert.AreEqual(1, counter);
            Assert.AreEqual(UniTaskStatus.Pending, t3.Status);
            a3.TrySetResult();
            Assert.AreEqual(UniTaskStatus.Succeeded, t3.Status);

            Assert.AreEqual(0, counter);

            executor.Dispose();
        }

        [Test, Timeout(10000)]
        public async Task キャンセルされたら全体がキャンセルされる()
        {
            var executor = new SequentialTaskExecutor<int>();

            var results = new List<int>();

            var t1 = executor.RegisterAsync(async ct =>
            {
                await Task.Delay(10000, ct);
                results.Add(1);
                return 1;
            });

            var t2 = executor.RegisterAsync(async ct =>
            {
                await Task.Delay(10000, ct);
                results.Add(2);
                return 2;
            });
            var t3 = executor.RegisterAsync(async ct =>
            {
                await Task.Delay(10000, ct);
                results.Add(3);
                return 3;
            });

            executor.Execute();

            await Task.Delay(500);

            executor.Dispose();

            Assert.AreEqual(UniTaskStatus.Canceled, t1.Status);
            Assert.AreEqual(UniTaskStatus.Canceled, t2.Status);
            Assert.AreEqual(UniTaskStatus.Canceled, t3.Status);
            Assert.IsEmpty(results);
        }
    }
}