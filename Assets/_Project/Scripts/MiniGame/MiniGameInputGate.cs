namespace MiniGame
{
    /// <summary>前段階から持ち越した押下を捨て、解放後の新しい入力だけを受け付ける</summary>
    public sealed class MiniGameInputGate
    {
        private bool _released;
        private bool _held;
        public bool Held => _released && _held;

        /// <summary>押下状態を記録し、解放後の押し始めだけtrueを返す</summary>
        /// <param name="held">現在の物理的な押下状態</param>
        /// <returns>新しい押下ならtrue</returns>
        /// <example>各フレームにgate.Sample(action.IsPressed())を呼ぶ</example>
        public bool Sample(bool held)
        {
            bool pressed = _released && held && !_held;
            if (!held) _released = true;
            _held = held;
            return pressed;
        }
    }
}
