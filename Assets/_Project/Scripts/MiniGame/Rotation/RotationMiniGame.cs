using UnityEngine;

namespace MiniGame
{
    public class RotationMiniGame : IMiniGame
    {
        [SerializeField] private RotationMiniGameSettings _rotationMiniGameSettings;
        [SerializeField] private float _startAngle = 0f;
        /// <summary>
        /// 回転するタイプのミニゲームを開始する
        /// </summary>
        public void StartGame()
        {
            
        }

        /// <summary>
        /// 回転するタイプのミニゲームを停止する
        /// </summary>
        public void StopGame()
        {

        }
    }
}
