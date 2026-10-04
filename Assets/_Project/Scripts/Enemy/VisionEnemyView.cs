using _Project.Scripts.Data.Enemy;
using UnityEngine;

/// <summary>敵の見た目と移動を反映する</summary>
public sealed class VisionEnemyView : MonoBehaviour
{
    [SerializeField] private EnemyDefinition _enemyDefinition;
    [SerializeField] private Renderer _visualRenderer;

    public Vector3 Position => transform.position;
    public float MoveSpeed => _enemyDefinition != null ? _enemyDefinition.MoveSpeed : 0f;

    /// <summary>見た目のRendererを取得する</summary>
    /// <example>Unityがコンポーネント生成時に呼ぶ</example>
    private void Awake()
    {
        if (_visualRenderer == null) _visualRenderer = GetComponentInChildren<Renderer>(true);
    }

    /// <summary>見た目の表示を切り替える</summary>
    /// <param name="visible">表示する場合はtrue</param>
    /// <example>昼はSetVisible(false)で姿を消す</example>
    public void SetVisible(bool visible)
    {
        if (_visualRenderer != null) _visualRenderer.enabled = visible;
    }
}
