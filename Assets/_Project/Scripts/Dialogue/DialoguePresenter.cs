using System;
using R3;
using VContainer.Unity;

namespace Dialogue
{
    public class DialoguePresenter : IInitializable, IDisposable
    {
        private readonly DialogueUIView _view;
        private readonly DialogueService _service;
        private readonly CompositeDisposable _disposables = new();

        /// <summary>会話の状態とUIを接続する依存関係を受け取る</summary>
        /// <param name="view">会話の表示とクリック通知を担当するView</param>
        /// <param name="service">会話の進行を管理するService</param>
        /// <example>LifetimeScopeのEntryPoint登録から生成する</example>
        public DialoguePresenter(DialogueUIView view, DialogueService service)
        {
            _view = view;
            _service = service;
        }

        /// <summary>クリックと会話状態を購読し、現在の表示を同期する</summary>
        /// <example>VContainerの初期化時に呼ばれる</example>
        public void Initialize()
        {
            _view.TextBoxClicked.Subscribe(_ => _service.Advance()).AddTo(_disposables);
            _view.Disabled.Subscribe(_ => _service.CancelDialogue()).AddTo(_disposables);

            // 初回通知で、初期化より前に開始された会話も表示へ反映する
            _service.CurrentLine.Subscribe(ShowLine).AddTo(_disposables);
        }

        /// <summary>
        /// 現在のセリフを描画し、会話がなければUIを隠す
        /// </summary>
        /// <param name="line">表示するセリフ、nullなら非表示</param>
        /// <example>CurrentLineの変更通知から呼ぶ</example>
        private void ShowLine(DialogueLine line)
        {
            if (_view == null)
            {
                _service.CancelDialogue();
                return;
            }

            if (line == null)
            {
                _view.Hide();
                return;
            }

            _view.SetName(GetSpeakerName(line.Speaker));
            _view.SetText(line.Text);
            _view.Show();

            // 親Canvasなどが無効で表示できない場合、操作停止だけを残さない
            if (!_view.isActiveAndEnabled) _service.CancelDialogue();
        }

        /// <summary>
        /// 話者の名前を取得する
        /// </summary>
        /// <param name="speaker">話者の種類</param>
        /// <returns>話者の名前</returns>
        /// <example>GetSpeakerName(Speaker.Player)</example>
        private static string GetSpeakerName(Speaker speaker)
        {
            return speaker switch
            {
                Speaker.Player => "渚",
                Speaker.Radio => "無線機",
                _ => ""
            };
        }

        /// <summary>購読を解除し、会話UIを隠す</summary>
        /// <example>LifetimeScopeの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            _disposables.Dispose();
            if (_view != null) _view.Hide();
        }
    }
}
