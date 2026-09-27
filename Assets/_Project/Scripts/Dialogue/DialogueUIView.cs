using UnityEngine;
using R3;
using TMPro;
using UnityEngine.UI;

namespace Dialogue
{
    public class DialogueUIView : MonoBehaviour
    {
        [SerializeField, Tooltip("ダイアローグ全体のButton")] private Button _button;
        [SerializeField, Tooltip("ダイアローグのテキスト")] private TextMeshProUGUI _text;
        [SerializeField, Tooltip("ダイアローグの名前")] private TextMeshProUGUI _name;

        private readonly Subject<Unit> _textBoxClicked = new Subject<Unit>();

        public Observable<Unit> TextBoxClicked => _textBoxClicked;

        /// <summary>ボタンのクリックを会話入力へ接続する</summary>
        /// <example>Viewの初回有効化時にUnityが呼ぶ</example>
        private void Awake()
        {
            _button.onClick.AddListener(OnTextBoxClicked);
        }

        /// <summary>
        /// ダイアローグのテキストを設定する
        /// </summary>
        /// <param name="text">表示する本文</param>
        /// <example>SetText("こんにちは")</example>
        public void SetText(string text)
        {
            _text.text = text ?? string.Empty;
        }

        /// <summary>
        /// ダイアローグの名前を設定する
        /// </summary>
        /// <param name="speakerName">表示する名前</param>
        /// <example>SetName("主人公")</example>
        public void SetName(string speakerName)
        {
            _name.text = speakerName ?? string.Empty;
        }

        /// <summary>
        /// ダイアローグUIを表示する
        /// </summary>
        /// <example>会話開始時にPresenterが呼ぶ</example>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// ダイアローグUIを非表示にする
        /// </summary>
        /// <example>会話終了時にPresenterが呼ぶ</example>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// ダイアローグUIのボタンがクリックされたときに通知する
        /// </summary>
        /// <example>ButtonのonClickから呼ばれる</example>
        private void OnTextBoxClicked()
        {
            _textBoxClicked.OnNext(Unit.Default);
        }

        /// <summary>ボタンのリスナーとクリック通知を解放する</summary>
        /// <example>シーンのアンロード時にUnityが呼ぶ</example>
        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnTextBoxClicked);
            _textBoxClicked.Dispose();
        }
    }
}
