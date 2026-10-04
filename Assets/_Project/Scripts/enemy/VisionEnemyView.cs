using System;
using _Project.Scripts.Data.Enemy;
using UnityEngine;

/// <summary>敵の表示と移動を行い、プレイヤーとの接触を通知する</summary>
public sealed class VisionEnemyView : MonoBehaviour
{
    [SerializeField] private EnemyDefinition _enemyDefinition;
    [SerializeField] private Renderer _visualRenderer;
    [SerializeField] private SphereCollider _contactCollider;
    [SerializeField, Min(0f)] private float _stopDistance = 1f;

    public event Action<IPlayerPosition> PlayerTouched;
    public Vector3 Position => transform.position;
    public float MoveSpeed => _enemyDefinition != null ? _enemyDefinition.MoveSpeed : 0f;
    public float StopDistance => _stopDistance;

    /// <summary>Prefabと開発用Sceneの既存部品を取得する</summary>
    /// <example>Unityがコンポーネント生成時に呼ぶ</example>
    private void Awake()
    {
        if (_visualRenderer == null) _visualRenderer = GetComponentInChildren<Renderer>(true);
        if (_contactCollider == null) _contactCollider = GetComponent<SphereCollider>();
    }

    /// <summary>見た目と接触判定を同時に切り替える</summary>
    /// <param name="visible">夜に活動させる場合はtrue</param>
    /// <example>昼はSetVisible(false)で姿とTriggerを消す</example>
    public void SetVisible(bool visible)
    {
        if (_visualRenderer != null) _visualRenderer.enabled = visible;
        if (_contactCollider != null) _contactCollider.enabled = visible;
    }

    /// <summary>追跡の計算結果だけ敵の位置を動かす</summary>
    /// <param name="movement">このフレームのワールド移動量</param>
    /// <example>Controller.Tickから水平移動を適用する</example>
    public void Move(Vector3 movement) => transform.position += movement;

    /// <summary>接触したプレイヤーを通知する</summary>
    /// <param name="other">敵のTriggerへ入ったCollider</param>
    /// <example>敵に近づいたときにUnityが呼ぶ</example>
    private void OnTriggerEnter(Collider other) => NotifyPlayerTouch(other);

    /// <summary>会話や釣りの入力制限が終わった後の重なりも検出する</summary>
    /// <param name="other">敵のTrigger内にいるCollider</param>
    /// <example>重なったままプレイヤーが操作を再開したときに呼ぶ</example>
    private void OnTriggerStay(Collider other) => NotifyPlayerTouch(other);

    /// <summary>位置を提供するプレイヤーだけを接触イベントとして発行する</summary>
    /// <param name="other">接触したCollider</param>
    /// <example>TriggerのEnterとStayから呼ぶ</example>
    private void NotifyPlayerTouch(Collider other)
    {
        if (_contactCollider == null || !_contactCollider.enabled) return;
        var player = other.GetComponent<IPlayerPosition>();
        if (player != null) PlayerTouched?.Invoke(player);
    }
}
