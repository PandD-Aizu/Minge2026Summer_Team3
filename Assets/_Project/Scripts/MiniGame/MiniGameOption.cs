using System;
using _Project.Scripts.Data.Enum;
using UnityEngine;

namespace MiniGame
{
    /// <summary>魚が持つミニゲーム一種類の抽選重みと難易度候補</summary>
    [Serializable]
    public sealed class MiniGameOption
    {
        public MiniGameType Type;
        [Min(0f)] public float Weight = 1f;
        public MiniGameSettings[] Settings = Array.Empty<MiniGameSettings>();
    }
}
