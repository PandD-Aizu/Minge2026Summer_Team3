using _Project.Scripts.Data.Enum;
using MiniGame;
using UnityEngine;

[CreateAssetMenu(fileName = "TimingMiniGameSettings", menuName = "Data/MiniGame/Timing Settings")]
public class TimingMiniGameSettings : MiniGameSettings
{
    public override MiniGameType GameType => MiniGameType.Timing;
}
