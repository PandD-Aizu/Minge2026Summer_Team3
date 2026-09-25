using UnityEngine.InputSystem;

namespace Input
{
    public class InputModeService
    {
        private readonly PlayerInputAction _inputAction;

        public InputModeService(PlayerInputAction inputAction)
        {
            _inputAction = inputAction;
        }

        /// <summary>
        /// 操作入力を通常のwasd左右移動、J,F,Escに変更
        /// </summary>
        public void SwitchToPlayer()
        {
            SwitchMap(_inputAction.Player.Get());
        }

        /// <summary>
        /// ミニゲームで使う操作入力に切り替える
        /// </summary>
        public void SwitchToMiniGame()
        {
            SwitchMap(_inputAction.MiniGame.Get());
        }

        /// <summary>
        /// インベントリの操作入力に切り替える
        /// </summary>
        /// <param name="targetMap"></param>
        /*
        public void SwitchToPanel()
        {
           SwitchMap(_inputAction.Panel.Get());
        }
        */

        /// <summary>
        /// 全ての入力を受け付けなくする
        /// </summary>
        public void DisableAllInputs()
        {
            _inputAction.Disable();
        }



        private void SwitchMap(InputActionMap targetMap)
        {
            _inputAction.Disable();

            targetMap.Enable();
        }
}


}
