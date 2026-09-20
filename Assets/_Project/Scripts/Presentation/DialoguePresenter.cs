using System;
using Dialogue;
using R3;
using VContainer.Unity;

namespace Presentation
{
    public class DialoguePresenter : IInitializable, IDisposable
    {
        private readonly DialogueUIView _view;
        private IDisposable _nextSubscription;

        private DialogueData _dialogue;
        private int _currentIndex;
        private bool _isPlaying;

        private readonly Subject<Unit> _completed = new();
        public Observable<Unit> Completed => _completed;

        public DialoguePresenter(DialogueUIView view)
        {
            this._view = view;
        }

        public void Initialize()
        {
            _nextSubscription = _view.TextBoxClicked.Subscribe(_ => ShowNextMessage());
        }

        // 会話開始時にSOを受け取る
        public void StartDialogue(DialogueData dialogue)
        {
            if (dialogue == null || dialogue.Count == 0) return;

            _dialogue = dialogue;
            _currentIndex = 0;
            _isPlaying = true;

            ShowCurrent();
            _view.Show();
        }

        // セリフを表示する
        private void ShowCurrent()
        {
            DialogueLine line = _dialogue.GetLine(_currentIndex);
            string name = GetSpeakerName(line.Speaker);

            _view.SetName(name);
            _view.SetText(line.Text);

        }

        // 次に行けるか判断
        private void ShowNextMessage()
        {
            if (!_isPlaying) return;

            _currentIndex++;

            if (_currentIndex >= _dialogue.Count)
            {
                _isPlaying = false;
                _view.Hide();

                // 会話が終了したことを通知
                _completed.OnNext(Unit.Default);
                return;
            }

            // 全部問題なければ次のセリフ
            ShowCurrent();

        }

        private string GetSpeakerName(Speaker speaker)
        {
            return speaker switch
            {
                Speaker.Player => "主人公",
                Speaker.Radio => "無線機からの声",
                _ => ""
            };
        }

        public void Dispose()
        {
            _nextSubscription?.Dispose();
            _completed.Dispose();
        }

    }
}
