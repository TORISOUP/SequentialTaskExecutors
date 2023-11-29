# SequentialTaskExecutor

登録したUniTaskを直列で順番に実行する機構です。

* 登録したUniTaskを順番に実行
* 登録したUniTaskごとに個別に`await`で待ち受け、例外処理が可能
* `SequentialTaskExecutor`を`Dispose`することで実行をすべてキャンセル可能
* `SequentialTaskExecutor`の再利用はできません

## 依存ライブラリ

* [UniTask](https://github.com/Cysharp/UniTask)

## 導入方法

### UPM Package

```
https://github.com/TORISOUP/SequentialTaskExecutors.git?path=Assets/TORISOUP/SequentialTaskExecutors
```

## 使い方

`SequentialTaskExecutor`または`SequentialTaskExecutor<T>`をインスタンス化して下さい。
`Execute()`を呼び出すと直列実行開始。`Dispose()`で全停止して破棄です。再利用はできません。

```cs
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Sample : MonoBehaviour
{
    void Start()
    {
        // Executorの作成
        var executor = new TORISOUP.SequentialTaskExecutors.SequentialTaskExecutor<int>();

        // Queueに登録
        executor.RegisterAsync(async _ =>
        {
            await UniTask.Delay(1000);
            return 1;
        });

        // CancellationTokenを指定する場合
        // この引数の「ct」はRegisterAsyncで渡したもの及びExecutorのDisposeの両方にリンクしている
        executor.RegisterAsync(async ct =>
        {
            await UniTask.Delay(1000, cancellationToken: ct);
            return 2;
        }, destroyCancellationToken);

        
        // 登録したタスクの実行完了をawait可能
        // 例外処理も可能
        UniTask.Void(async () =>
        {
            try
            {
                // この場合はRegisterAsyncで登録した非同期処理自体の完了を待機することになる
                var result = await executor.RegisterAsync(async ct =>
                {
                    await UniTask.Delay(1000, cancellationToken: ct);
                    return 3;
                }, destroyCancellationToken);

                // "3"が出力される
                Debug.Log(result);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Canceled");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        });

        // Execute()を呼び出すことで実行開始
        // 実行開始後にもRegisterAsync()で追加登録可能
        executor.Execute();

        destroyCancellationToken.Register(() =>
        {
            // Dispose()を呼び出すことで直列実行をすべて中止
            // RegisterAsync()をawaitしている場合はそれもキャンセルされる
            executor.Dispose();
        });
    }
}
```

## DEMO

テクスチャを順番にダウンロードするサンプル実装です。
InputFieldに入力されたURLリストを順番にダウンロードし、1個ずつ表示します。


![DEMO](https://media.githubusercontent.com/media/TORISOUP/SequentialTaskExecutors/master/DemoResources/Demo.gif)

```cs
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace TORISOUP.SequentialTaskExecutors.Demo
{
    public class SequentialTaskExecutorDemo : MonoBehaviour
    {
        [SerializeField] private GameObject _rootImages;
        [SerializeField] private Button _downloadButton;
        [SerializeField] private InputField _urlInputField;
        private RawImage[] _rawImages;
        readonly SequentialTaskExecutor<Texture> _executor = new SequentialTaskExecutor<Texture>();

        private void Start()
        {
            _executor.Execute();

            _rawImages = _rootImages.GetComponentsInChildren<RawImage>();

            _downloadButton.OnClickAsAsyncEnumerable(destroyCancellationToken)
                .ForEachAsync(_ =>
                {
                    var text = _urlInputField.text;
                    var urls = text.Split('\n');

                    // Texture表示をリセット
                    foreach (var rawImage in _rawImages)
                    {
                        rawImage.texture = null;
                    }

                    // 順番にダウンロードを実行する
                    for (var i = 0; i < _rawImages.Length; i++)
                    {
                        if (urls.Length <= i) break;
                        var url = urls[i];
                        DownloadAndSetTextureAsync(url, _rawImages[i], destroyCancellationToken).Forget();
                    }
                }, destroyCancellationToken);
            ;
        }

        private async UniTask DownloadAndSetTextureAsync(string url, RawImage rawImage, CancellationToken ct)
        {
            // SequentialTaskExecutorを用いて一度にダウンロードされるのを防ぐ
            try
            {
                var texture = await _executor.RegisterAsync(t => DownloadTextureAsync(url, t), ct);
                rawImage.texture = texture;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogError(ex);
            }
        }

        private static async UniTask<Texture> DownloadTextureAsync(string url, CancellationToken ct)
        {
            var uwr = UnityWebRequestTexture.GetTexture(url);
            await uwr.SendWebRequest().ToUniTask(cancellationToken: ct);
            var texture = DownloadHandlerTexture.GetContent(uwr);
            return texture;
        }

        private void OnDestroy()
        {
            _executor.Dispose();
        }
    }
}
```

## LICENSE

The MIT License (MIT)

## 権利表記

### UniTask

The MIT License (MIT)

Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
