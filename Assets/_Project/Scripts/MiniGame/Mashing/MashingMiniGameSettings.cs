using _Project.Scripts.Data.Enum;
using MiniGame;
using UnityEngine;

[CreateAssetMenu(fileName = "MashingMiniGameSettings", menuName = "Data/MiniGame/Mashing Settings")]
public class MashingMiniGameSettings : MiniGameSettings
{
    public override MiniGameType GameType => MiniGameType.Mashing;
}
