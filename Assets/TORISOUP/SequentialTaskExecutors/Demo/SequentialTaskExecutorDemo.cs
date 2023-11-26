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
                .ForEachAsync( _ =>
                {
                    var text = _urlInputField.text;
                    var urls = text.Split('\n');
                    
                    // Texture表示をリセット
                    foreach (var rawImage in _rawImages)
                    {
                        rawImage.texture =null;
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

        private void OnDestroy()
        {
            _executor.Dispose();
        }

        private async UniTask DownloadAndSetTextureAsync(string url, RawImage rawImage, CancellationToken ct)
        {
            // SequentialTaskExecutorを用いて一度にダウンロードされるのを防ぐ
            var texture = await _executor.RegisterAsync(DownloadTextureAsync, url, ct);
            rawImage.texture = texture;
        }

        private static async UniTask<Texture> DownloadTextureAsync(string url, CancellationToken ct)
        {
            var uwr = UnityWebRequestTexture.GetTexture(url);
            await uwr.SendWebRequest().ToUniTask(cancellationToken: ct);
            var texture = DownloadHandlerTexture.GetContent(uwr);
            return texture;
        }
    }
}