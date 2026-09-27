namespace Input
{
    /// <summary>操作中のメニューを1つに限定し、背後のゲーム操作との競合を防ぐ</summary>
    public sealed class MenuInputService : System.IDisposable
    {
        private object _owner;
        private UnityEngine.GameObject _eventSystem;
        public bool IsOpen => _owner != null;

        /// <summary>ほかのメニューが閉じていれば入力の所有権を得る</summary>
        /// <param name="owner">開こうとしているPresenter</param>
        /// <returns>このメニューが入力を所有できた場合はtrue</returns>
        /// <example>画面を開く前にTryAcquire(this)を呼ぶ</example>
        public bool TryAcquire(object owner)
        {
            if (_owner != null && !ReferenceEquals(_owner, owner)) return false;
            _owner = owner;
            // キーボード操作に加え、既存のボタンをマウスでも操作できるようにする
            if (UnityEngine.EventSystems.EventSystem.current == null && _eventSystem == null)
            {
                _eventSystem = new UnityEngine.GameObject("MenuEventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
            return true;
        }

        /// <summary>自分の所有権だけを解除する</summary>
        /// <param name="owner">閉じるメニューのPresenter</param>
        /// <example>閉じる処理とDisposeからRelease(this)を呼ぶ</example>
        public void Release(object owner)
        {
            if (!ReferenceEquals(_owner, owner)) return;
            Dispose();
        }

        /// <summary>入力所有権と、このサービスが生成したEventSystemを解放する</summary>
        /// <example>メニューを閉じるとき、またはVContainerのRoot Scope破棄時に呼ぶ</example>
        public void Dispose()
        {
            _owner = null;
            if (_eventSystem == null) return;
            _eventSystem.SetActive(false);
            UnityEngine.Object.Destroy(_eventSystem);
            _eventSystem = null;
        }
    }
}
