using System;
using R3;
using UnityEngine.InputSystem;

namespace Input
{
    /// <summary>Input Systemのイベントを、購読解除と対になるR3の通知へ変換する</summary>
    public static class InputActionObservables
    {
        /// <summary>入力アクションの実行を通知するObservableを返す</summary>
        /// <param name="action">監視する入力アクション</param>
        /// <returns>購読のDispose時にイベントハンドラーを解除する通知</returns>
        /// <example>action.OnPerformedAsObservable().Subscribe(...).AddTo(this)</example>
        public static Observable<InputAction.CallbackContext> OnPerformedAsObservable(this InputAction action)
            => Observable.FromEvent<Action<InputAction.CallbackContext>, InputAction.CallbackContext>(
                handler => handler, handler => action.performed += handler, handler => action.performed -= handler);

        /// <summary>入力アクションのキャンセルを通知するObservableを返す</summary>
        /// <param name="action">監視する入力アクション</param>
        /// <returns>購読のDispose時にイベントハンドラーを解除する通知</returns>
        /// <example>Moveのキャンセルを購読し、キーを離したら移動値をゼロに戻す</example>
        public static Observable<InputAction.CallbackContext> OnCanceledAsObservable(this InputAction action)
            => Observable.FromEvent<Action<InputAction.CallbackContext>, InputAction.CallbackContext>(
                handler => handler, handler => action.canceled += handler, handler => action.canceled -= handler);
    }
}
