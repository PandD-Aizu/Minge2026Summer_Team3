using UnityEngine;
using UnityEngine.InputSystem;
using System;
using R3;
using Dialogue;

public class RadioTalkingAnimation : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveAmplitude=0.04f;
    [SerializeField] private float moveFrequency=8f;
    [SerializeField] private float tiltAngle = 3f;
    [SerializeField] private float returnSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugKey = true;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isTalking;
    private float elapsedTime;

    private IDisposable lineSubscription;

    private void Awake()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(enableDebugKey && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            SetTalking(!isTalking);
        }

        if (isTalking)
        {
            elapsedTime+= Time.deltaTime;
            
            float y = Mathf.Sin(elapsedTime * moveFrequency) * moveAmplitude;

            float z = Mathf.Sin(elapsedTime * moveFrequency * 0.7f) * tiltAngle;

            Vector3 targetPosition = initialPosition + new Vector3(0f, y, 0f);

            Quaternion targetRotation = initialRotation * Quaternion.Euler(0f, 0f, z);

            transform.localPosition = targetPosition;
            transform.localRotation = targetRotation;

        }
        else
        {
            float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);

            transform.localPosition = Vector3.Lerp(transform.localPosition, initialPosition, t);

            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialRotation,t);
        }
    }

    public void ConnectDialogueService(DialogueService service)
    {
        lineSubscription?.Dispose();
        lineSubscription = null;

        if(service == null)
        {
            SetTalking(false);
            return;
        }

        lineSubscription = service.CurrentLine.Subscribe(line =>
        {
            bool radioSpeaking = line != null && line.Speaker == Speaker.Radio;

            SetTalking(radioSpeaking);
        });
    }

    private void OnDestroy()
    {
        lineSubscription?.Dispose();
        lineSubscription = null;

        SetTalking(false);
    }

    public void SetTalking(bool talking)
    {
        if (isTalking == talking) return;

        isTalking = talking;

        if (isTalking)
        {
            elapsedTime = 0f;
        }
    }
}
