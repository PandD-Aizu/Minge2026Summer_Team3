using UnityEngine;

/// <summary>落水中のカメラにだけ水膜、気泡、濁りの状態を渡す</summary>
[DisallowMultipleComponent]
public sealed class OceanCaptureScreenView : MonoBehaviour
{
    public float Immersion { get; private set; }
    public float SubmergedTime { get; private set; }
    public float Impact { get; private set; }
    public bool IsVisible => Immersion > 0f || Impact > 0f;

    /// <summary>水面通過の割合と潜水後の時間を反映する</summary>
    /// <param name="immersion">0で水上、1で完全に水中</param>
    /// <param name="elapsed">入水からの実時間秒数</param>
    /// <example>落下中にSetWater(immersion, elapsed)を呼ぶ</example>
    public void SetWater(float immersion, float elapsed)
    {
        Immersion = Mathf.Clamp01(immersion);
        SubmergedTime = Mathf.Max(0f, elapsed);
        Impact = immersion > 0f ? Mathf.Exp(-SubmergedTime * 4f) : 0f;
    }

    /// <summary>中断や帰還時に水中表示を解除する</summary>
    /// <example>カメラを通常視点へ戻す直前に呼ぶ</example>
    public void Clear()
    {
        Immersion = 0f;
        Impact = 0f;
        SubmergedTime = 0f;
    }
}
