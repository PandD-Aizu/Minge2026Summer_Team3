using UnityEngine;
using System;
using R3;
using Dialogue;

/// <summary>会話中のラジオの拡縮と音符、イベント待機中の接近ノイズを表示する</summary>
public class RadioTalkingAnimation : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveAmplitude = 0.04f;
    [SerializeField] private float moveFrequency = 8f;
    [SerializeField] private float tiltAngle = 3f;
    [SerializeField] private float returnSpeed = 8f;
    [SerializeField, Range(0f, 0.3f)] private float scaleAmplitude = 0.09f;
    [Header("Signal")]
    [SerializeField, Min(0.1f)] private float noiseDistance = 5f;
    [SerializeField, Range(0f, 0.2f)] private float noiseVolume = 0.035f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private bool isTalking;
    private float elapsedTime;
    private IDisposable lineSubscription;
    private DialogueService dialogue;
    private Transform player;
    private Func<bool> hasEvent;
    private RadioSignalEffects effects;

    /// <summary>演出の基準姿勢と再利用する音符を準備する</summary>
    private void Awake()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        initialScale = transform.localScale;
        effects = new RadioSignalEffects(transform);
    }

    /// <summary>接近距離とイベント有無の取得元を接続する</summary>
    /// <param name="target">距離を測るプレイヤー</param>
    /// <param name="available">未消化イベントがある場合にtrueを返す関数</param>
    /// <example>GameplayDialogueLifetimeScopeの構築後に呼ぶ</example>
    public void ConfigureSignal(Transform target, Func<bool> available)
    {
        player = target;
        hasEvent = available;
    }

    /// <summary>話者に合わせて姿勢と音符を更新し、接近距離に応じてノイズを調整する</summary>
    private void Update()
    {
        bool speaking = isTalking && Time.timeScale > 0f;
        if (speaking)
        {
            elapsedTime += Time.deltaTime;
            float pulse = Mathf.Sin(elapsedTime * moveFrequency);
            transform.localPosition = initialPosition + Vector3.up * (pulse * moveAmplitude);
            transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f,
                Mathf.Sin(elapsedTime * moveFrequency * 0.7f) * tiltAngle);
            transform.localScale = Vector3.Scale(initialScale,
                new Vector3(1f - pulse * scaleAmplitude * 0.5f, 1f + pulse * scaleAmplitude, 1f));
        }
        else
        {
            float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialPosition, t);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialRotation, t);
            transform.localScale = Vector3.Lerp(transform.localScale, initialScale, t);
        }

        effects.UpdateNotes(speaking, Time.deltaTime);
        float volume = 0f;
        if (Time.timeScale > 0f && player != null && dialogue != null && !dialogue.IsPlaying
            && hasEvent != null && hasEvent())
        {
            float proximity = 1f - Mathf.Clamp01(Vector3.Distance(player.position, transform.position) / noiseDistance);
            volume = noiseVolume * proximity * proximity;
        }
        effects.UpdateNoise(volume);
    }

    /// <summary>会話の話者を購読し、ラジオの行だけ演出を有効にする</summary>
    /// <param name="service">会話サービス、nullなら接続を解除する</param>
    public void ConnectDialogueService(DialogueService service)
    {
        lineSubscription?.Dispose();
        lineSubscription = null;
        dialogue = service;
        SetTalking(false);
        if (service != null)
            lineSubscription = service.CurrentLine.Subscribe(line => SetTalking(line != null && line.Speaker == Speaker.Radio));
    }

    /// <summary>会話中の拡縮と音符の再生状態を変更する</summary>
    /// <param name="talking">ラジオが発話している場合はtrue</param>
    public void SetTalking(bool talking)
    {
        if (isTalking == talking) return;
        isTalking = talking;
        if (talking) elapsedTime = 0f;
    }

    /// <summary>無効化時に姿勢と音を即座に元へ戻す</summary>
    private void OnDisable()
    {
        transform.localPosition = initialPosition;
        transform.localRotation = initialRotation;
        transform.localScale = initialScale;
        effects?.UpdateNotes(false, 0f);
        effects?.StopNoise();
    }

    /// <summary>購読と生成した描画・音声リソースを解放する</summary>
    private void OnDestroy()
    {
        lineSubscription?.Dispose();
        effects?.Dispose();
    }
}
