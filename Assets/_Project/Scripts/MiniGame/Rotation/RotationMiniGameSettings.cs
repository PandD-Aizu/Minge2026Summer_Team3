using _Project.Scripts.MiniGame;
using _Project.Scripts.Data.Enum;
using MiniGame;
using UnityEngine;

[CreateAssetMenu(fileName = "MiniGameSettings", menuName = "Data/MiniGameSettings")]
public class RotationMiniGameSettings : MiniGameSettings
{
    [SerializeField] private Difficulty _difficulty;
    [Header("ポインタの回転速度")][Min(0f)]
    [SerializeField] private float _rotationSpeed;

    [Header("判定エリアの角度幅")]
    [Range(0f, 360f)]
    [SerializeField] private float _greatAngle;

    [Range(0f, 360f)]
    [SerializeField] private float _goodAngle;

    public Difficulty Difficulty => _difficulty;
    public override MiniGameType GameType => MiniGameType.Rotation;
    public float RotationSpeed => _rotationSpeed;
    public float GreatAngle => _greatAngle;
    public float GoodAngle => _goodAngle;
}
