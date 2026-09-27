namespace Dialogue
{
    using System;
    using R3;

    public class DialogueService : IDisposable
    {
        private DialogueData _dialogue;
        private int _currentIndex;
        private readonly ReactiveProperty<DialogueLine> _currentLine = new(null);
        private readonly Subject<Unit> _completed = new();

        public ReadOnlyReactiveProperty<DialogueLine> CurrentLine => _currentLine;
        public Observable<Unit> Completed => _completed;
        public bool IsPlaying => _dialogue != null;

        /// <summary>会話を先頭から開始し、現在のセリフを通知する</summary>
        /// <param name="dialogue">再生する会話データ、未設定または空なら何もしない</param>
        /// <example>service.StartDialogue(introDialogue)</example>
        public void StartDialogue(DialogueData dialogue)
        {
            if (dialogue == null || dialogue.Count == 0) return;

            _dialogue = dialogue;
            _currentIndex = 0;
            _currentLine.Value = _dialogue.GetLine(_currentIndex);
        }

        /// <summary>次のセリフへ進み、最後のセリフの後で完了を通知する</summary>
        /// <example>テキストボックスのクリック時にservice.Advance()を呼ぶ</example>
        public void Advance()
        {
            if (!IsPlaying) return;

            _currentIndex++;
            if (_currentIndex < _dialogue.Count)
            {
                _currentLine.Value = _dialogue.GetLine(_currentIndex);
                return;
            }

            // 完了先が次の会話を開始できるよう、状態と表示を先に終了する
            _dialogue = null;
            _currentIndex = 0;
            _currentLine.Value = null;
            _completed.OnNext(Unit.Default);
        }

        /// <summary>会話データへの参照と状態通知を解放する</summary>
        /// <example>LifetimeScopeの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            _dialogue = null;
            _currentLine.Dispose();
            _completed.Dispose();
        }
    }
}
