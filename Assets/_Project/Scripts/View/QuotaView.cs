using System;
using System.Threading;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace Quota
{
    /// <summary>現在のノルマと進捗を画面に表示する</summary>
    public sealed class QuotaView : MonoBehaviour
    {
        [SerializeField] private GameObject _progressPanel;
        [SerializeField] private TMP_Text _goal;
        [SerializeField] private TMP_Text _deadline;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private UnityEngine.UI.Slider _progressSlider;
        private Canvas _canvas;
        private RectTransform _panelRectTransform;

        /// <summary>初期表示を閉じ、シーンごとの表示を監視する</summary>
        /// <example>UnityがCanvasを生成したときに呼ぶ</example>
        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            Hide();
            UpdateSceneVisibility(default, SceneManager.GetActiveScene());
            SceneManager.activeSceneChanged += UpdateSceneVisibility;
            _panelRectTransform = _progressPanel.GetComponent<RectTransform>();

            UIAnimation();
        }

        /// <summary>タイトルや設定画面でゲーム中のノルマ表示を隠す</summary>
        /// <example>SceneManagerが拠点からタイトルへ切り替わったときに呼ぶ</example>
        private void UpdateSceneVisibility(Scene previous, Scene current)
        {
            _canvas.enabled = current.name is not ("Title" or "Option" or "IntroScene");
        }

        /// <summary>Canvasを破棄するときにシーンの監視を解除する</summary>
        /// <example>UnityがPlayモード終了時に呼ぶ</example>
        private void OnDestroy() => SceneManager.activeSceneChanged -= UpdateSceneVisibility;

        /// <summary>新しい目標と期限を現在のノルマとして表示する</summary>
        public void Present(string goal, string deadline)
        {
            _goal.text = goal;
            _deadline.text = $"期限　{deadline}";

            UIAnimation().Forget();
        }

        /// <summary>進捗の数字とバーを更新する</summary>
        /// <param name="current">達成済みの数量</param>
        /// <param name="target">目標数量</param>
        /// <example>SetProgress(1, 1)で達成を表示する</example>
        public void SetProgress(int current, int target)
        {
            _progress.text = current >= target ? $"{current} / {target}　達成" : $"{current} / {target}";
            _progressSlider.normalizedValue = Mathf.Clamp01((float)current / target);
        }

        /// <summary>現在のノルマ表示を閉じる</summary>
        /// <example>チュートリアル完了時に呼ぶ</example>
        public void Hide() => _progressPanel.SetActive(false);

        private async UniTaskVoid UIAnimation()
        {
            var start = new Vector3(0, 140, 0);
            var end = new Vector3(400, 450, 0);

            var startScale = new Vector3(2, 2, 1);
            var endScale = new Vector3(1, 1, 1);

            _progressPanel.SetActive(true);


            LMotion.Create(startScale, endScale, 0.5f)
                .WithEase(Ease.InOutBounce)
                .BindToLocalScale(_panelRectTransform);

            await UniTask.Delay(5000, cancellationToken:destroyCancellationToken);

            LSequence.Create()
                .Append(LMotion.Create(start, end, 1)
                    .WithEase(Ease.InQuad)
                    .Bind(position => _panelRectTransform.anchoredPosition = position))
                .Join(LMotion.Create(startScale, endScale, 1)
                    .WithEase(Ease.InQuad)
                    .BindToLocalScale(_panelRectTransform))
                .Run();

            // X:410 Y:80
            // X:0 Y:-400 Scale:2
        }


    }
}
