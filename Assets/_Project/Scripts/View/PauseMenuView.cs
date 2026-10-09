using R3;
using SceneLoadServices;
using UnityEngine;
using UnityEngine.UI;

namespace PauseMenu
{
    /// <summary>タイトルへ戻る確認パネルを表示する</summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _returnToTitleButton;
        [SerializeField] private SceneReference _titleScene = new();

        public bool IsConfigured => _panel != null && _resumeButton != null && _returnToTitleButton != null;
        public SceneReference TitleScene => _titleScene;
        public Observable<Unit> OnResumeClicked => _resumeButton.OnClickAsObservable();
        public Observable<Unit> OnReturnToTitleClicked => _returnToTitleButton.OnClickAsObservable();

        /// <summary>開始時は確認パネルを隠す</summary>
        private void Awake() => Hide();

        /// <summary>確認パネルを表示してゲームに戻るボタンを選ぶ</summary>
        /// <example>通常操作中にEscを押したときに呼ぶ</example>
        public void Show()
        {
            _panel.SetActive(true);
            _resumeButton.Select();
        }

        /// <summary>確認パネルを閉じる</summary>
        /// <example>ゲームへ戻るときやSceneを離れるときに呼ぶ</example>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>シーン読み込み中のボタン連打を防ぐ</summary>
        /// <param name="interactable">操作を受け付ける場合はtrue</param>
        /// <example>Title読み込み中はfalseにする</example>
        public void SetInteractable(bool interactable)
        {
            _resumeButton.interactable = interactable;
            _returnToTitleButton.interactable = interactable;
        }
    }
}
