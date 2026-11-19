using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary>初期表示を閉じ、シーンごとの表示を監視する</summary>
        /// <example>UnityがCanvasを生成したときに呼ぶ</example>
        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            Hide();
            UpdateSceneVisibility(default, SceneManager.GetActiveScene());
            SceneManager.activeSceneChanged += UpdateSceneVisibility;
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
            _progressPanel.SetActive(true);
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
    }
}
