using UnityEngine;

namespace _Project.Scripts.Fishing
{
    /// <summary>全ミニゲームの前段で共有する食いつき設定</summary>
    [CreateAssetMenu(menuName = "Data/Fishing/Bite Settings")]
    public sealed class FishingBiteSettings : ScriptableObject
    {
        public Vector2 WaitSeconds = new(1.5f, 4f);
        [Min(0.1f), Tooltip("遊泳を終えてから頭が浮きに届くまでの秒数")]
        public float ApproachSeconds = 0.7f;
        [Min(0f), Tooltip("突っついた後に頭を浮きから離す距離、ワールド座標の単位")]
        public float NibbleDistance = 0.18f;
        [Min(0.1f)] public float HookWindow = 1.2f;
        [Min(0.01f)] public float BobHeight = 0.14f;
        [Min(0.01f)] public float BobPeriod = 0.23f;

        /// <summary>待機時間の逆転と負数を補正する</summary>
        /// <example>Inspectorの編集時にUnityから呼ばれる</example>
        private void OnValidate()
        {
            WaitSeconds = new Vector2(Mathf.Max(0.1f, Mathf.Min(WaitSeconds.x, WaitSeconds.y)),
                Mathf.Max(0.1f, Mathf.Max(WaitSeconds.x, WaitSeconds.y)));
            HookWindow = Mathf.Max(0.1f, HookWindow);
            ApproachSeconds = Mathf.Max(0.1f, ApproachSeconds);
            NibbleDistance = Mathf.Max(0f, NibbleDistance);
            BobHeight = Mathf.Max(0.01f, BobHeight);
            BobPeriod = Mathf.Max(0.01f, BobPeriod);
        }
    }
}
