using _Project.Scripts.MiniGame;
using UnityEngine;

[CreateAssetMenu(fileName = "MiniGameSettings", menuName = "Data/MiniGameSettings")]
public class RotationMiniGameSettings : ScriptableObject
{
    [SerializeField] private Difficulty _difficulty;
    [Header("ポインタの回転速度")][Min(0f)]
    [SerializeField] private float _rotationSpeed = 180f;

    [Header("判定エリアの角度幅")]
    [Range(0f, 360f)]
    [SerializeField] private float _greatAngle = 30f;

    [Range(0f, 360f)]
    [SerializeField] private float _goodAngle = 10f;

    public Difficulty Difficulty => _difficulty;
    public float RotationSpeed => _rotationSpeed;
    public float GreatAngle => _greatAngle;
    public float GoodAngle => _goodAngle;
}
