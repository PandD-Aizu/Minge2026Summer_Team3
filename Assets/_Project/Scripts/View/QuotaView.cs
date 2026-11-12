using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Project.Scripts.View
{
    /// <summary>ノルマ発表パネルと進捗表示を更新する</summary>
    public sealed class QuotaView : MonoBehaviour
    {
        [SerializeField] private GameObject _announcementPanel;
        [SerializeField] private GameObject _progressPanel;
        [SerializeField] private TMP_Text _announcementGoal;
        [SerializeField] private TMP_Text _announcementDeadline;
        [SerializeField] private TMP_Text _goal;
        [SerializeField] private TMP_Text _deadline;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private Image _fill;

        private int _announcementVersion;
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
        /// <param name="previous">切り替え前のシーン</param>
        /// <param name="current">切り替え後のシーン</param>
        /// <example>SceneManagerが拠点からタイトルへ切り替わったときに呼ぶ</example>
        private void UpdateSceneVisibility(Scene previous, Scene current)
        {
            _canvas.enabled = current.name is not ("Title" or "Option" or "IntroScene");
        }

        /// <summary>Canvasを破棄するときにシーンの監視を解除する</summary>
        /// <example>UnityがPlayモード終了時に呼ぶ</example>
        private void OnDestroy() => SceneManager.activeSceneChanged -= UpdateSceneVisibility;

        /// <summary>新しい目標と期限を発表し、常設表示に反映する</summary>
        /// <param name="goal">ノルマの内容</param>
        /// <param name="deadline">プレイヤーに表示する期限</param>
        /// <example>Present("魚を1匹釣る", "今夜 0:00")</example>
        public void Present(string goal, string deadline)
        {
            _announcementGoal.text = goal;
            _announcementDeadline.text = $"期限　{deadline}";
            _goal.text = goal;
            _deadline.text = _announcementDeadline.text;
            _progressPanel.SetActive(true);
            _announcementPanel.SetActive(true);
            DismissAnnouncementAsync(++_announcementVersion).Forget();
        }

        /// <summary>進捗の数字とバーを更新する</summary>
        /// <param name="current">達成済みの数量</param>
        /// <param name="target">目標数量</param>
        /// <example>SetProgress(1, 1)で達成を表示する</example>
        public void SetProgress(int current, int target)
        {
            _progress.text = current >= target ? $"{current} / {target}　達成" : $"{current} / {target}";
            _fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)current / target), 1f);
        }

        /// <summary>発表パネルと常設表示を閉じる</summary>
        /// <example>チュートリアル完了時に呼ぶ</example>
        public void Hide()
        {
            _announcementVersion++;
            _announcementPanel.SetActive(false);
            _progressPanel.SetActive(false);
        }

        /// <summary>発表を読める時間を確保してからパネルを閉じる</summary>
        /// <param name="version">新しい発表を誤って閉じないための発表番号</param>
        /// <returns>閉じるまでの待機処理</returns>
        /// <example>Presentから発表番号を渡す</example>
        private async UniTask DismissAnnouncementAsync(int version)
        {
            bool cancelled = await UniTask.Delay(4500, DelayType.Realtime,
                cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
            if (!cancelled && version == _announcementVersion)
                _announcementPanel.SetActive(false);
        }
    }
}
