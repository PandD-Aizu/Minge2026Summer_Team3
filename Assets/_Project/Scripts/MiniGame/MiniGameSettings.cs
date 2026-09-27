using _Project.Scripts.Data.Enum;
using UnityEngine;

namespace MiniGame
{
    public abstract class MiniGameSettings : ScriptableObject
    {
        public abstract MiniGameType GameType { get; }
    }
}
