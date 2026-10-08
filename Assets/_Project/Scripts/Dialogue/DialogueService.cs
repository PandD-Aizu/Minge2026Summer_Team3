namespace Dialogue
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using R3;

    public class DialogueService : IDisposable
    {
        private DialogueData _dialogue;
        private int _currentIndex;
        private bool _disposed;
        private UniTaskCompletionSource<DialogueResult> _completion;
        private readonly ReactiveProperty<DialogueLine> _currentLine = new(null);
        private readonly Subject<Unit> _completed = new();

        public ReadOnlyReactiveProperty<DialogueLine> CurrentLine => _currentLine;
        public Observable<Unit> Completed => _completed;
        public bool IsPlaying => _dialogue != null;

        /// <summary>会話を先頭から開始し、現在のセリフを通知する</summary>
        /// <param name="dialogue">再生する会話データ</param>
        /// <returns>開始できた場合はtrue、再生中や不正なデータの場合はfalse</returns>
        /// <example>service.StartDialogue(introDialogue)</example>
        public bool StartDialogue(DialogueData dialogue)
        {
            return TryStart(dialogue, out _);
        }

        /// <summary>会話を開始し、この会話の終了結果を待つ</summary>
        /// <param name="dialogue">再生する会話データ</param>
        /// <param name="cancellationToken">呼び出し元の破棄やイベント中断でキャンセルするトークン</param>
        /// <returns>正常終了、中断、または開始拒否</returns>
        /// <example>var result = await service.PlayAsync(dialogue, cancellationToken)</example>
        public async UniTask<DialogueResult> PlayAsync(DialogueData dialogue, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return DialogueResult.Canceled;
            if (!TryStart(dialogue, out var completion)) return DialogueResult.Rejected;

            try
            {
                return await completion.Task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 別スレッドからのキャンセルでも、Unityの表示更新はメインスレッドで行う
                await UniTask.SwitchToMainThread();
                if (ReferenceEquals(_completion, completion)) CancelDialogue();
                return DialogueResult.Canceled;
            }
        }

        /// <summary>データを検証し、会話ごとの完了待機を作って再生する</summary>
        /// <param name="dialogue">検証する会話データ</param>
        /// <param name="completion">開始した会話の完了待機、開始できなければnull</param>
        /// <returns>会話を開始した場合はtrue</returns>
        /// <example>StartDialogueとPlayAsyncから共通で使用する</example>
        private bool TryStart(DialogueData dialogue, out UniTaskCompletionSource<DialogueResult> completion)
        {
            completion = null;
            if (_disposed || IsPlaying || dialogue == null || dialogue.Count == 0) return false;

            // 途中のnull行でUIだけが消え、進めなくなる状態を防ぐ
            for (var index = 0; index < dialogue.Count; index++)
            {
                if (dialogue.GetLine(index) == null) return false;
            }

            completion = new UniTaskCompletionSource<DialogueResult>();
            _completion = completion;
            _dialogue = dialogue;
            _currentIndex = 0;
            _currentLine.Value = _dialogue.GetLine(_currentIndex);
            return true;
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

            Finish(DialogueResult.Completed);
        }

        /// <summary>再生中の会話を中断し、正常終了通知を発行せずにUIを閉じる</summary>
        /// <example>イベントを取り消すときにservice.CancelDialogue()を呼ぶ</example>
        public void CancelDialogue()
        {
            if (IsPlaying) Finish(DialogueResult.Canceled);
        }

        /// <summary>状態と表示を片付けてから、この会話の結果を通知する</summary>
        /// <param name="result">正常終了または中断</param>
        /// <example>最後のセリフから進む場合はCompletedを渡す</example>
        private void Finish(DialogueResult result)
        {
            var completion = _completion;
            _completion = null;
            _dialogue = null;
            _currentIndex = 0;
            _currentLine.Value = null;

            // 待機先が次の会話を始めても、その会話の完了待機は変更しない
            if (result == DialogueResult.Completed) _completed.OnNext(Unit.Default);
            completion.TrySetResult(result);
        }

        /// <summary>会話データへの参照と状態通知を解放する</summary>
        /// <example>LifetimeScopeの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelDialogue();
            _currentLine.Dispose();
            _completed.Dispose();
        }
    }
}
