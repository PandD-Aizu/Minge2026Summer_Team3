using _Project.Scripts.Core;
using LifetimeScopes;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

[CustomEditor(typeof(FishingSceneLifetimeScope))]
public class FishingSceneLifetimeScopeEditor : UnityEditor.Editor
{
    /// <summary>通常の設定とPlay中の時間帯切り替えを表示する</summary>
    /// <example>FishingStageのLifetimeScopeをInspectorで選択する</example>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("時間帯の動作確認", EditorStyles.boldLabel);

        var scope = (LifetimeScope)target;
        bool available = Application.isPlaying && scope.Container != null &&
                         scope.Container.TryResolve<GameProgress>(out _);

        using (new EditorGUI.DisabledScope(!available))
        {
            if (GUILayout.Button("朝に切り替える")) SetTimeOfDay(scope, TimeOfDay.Day);
            if (GUILayout.Button("夕方に切り替える")) SetTimeOfDay(scope, TimeOfDay.Evening);
            if (GUILayout.Button("夜に切り替える")) SetTimeOfDay(scope, TimeOfDay.Night);
            if (GUILayout.Button("明け方に切り替える")) SetTimeOfDay(scope, TimeOfDay.Dawn);
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("時間帯の切り替えはPlay中のみ有効", MessageType.Info);
    }

    /// <summary>共有の進行状態を更新し、購読中の画面と敵へ通知する</summary>
    /// <param name="scope">FishingStageのLifetimeScope</param>
    /// <param name="timeOfDay">切り替え先の時間帯</param>
    /// <example>Inspectorの朝・夕方・夜・明け方ボタンから呼ぶ</example>
    private static void SetTimeOfDay(LifetimeScope scope, TimeOfDay timeOfDay)
    {
        if (!scope.Container.TryResolve<GameProgress>(out var progress)) return;

        if (timeOfDay == TimeOfDay.Night) progress.StartNight();
        else if (timeOfDay == TimeOfDay.Evening) progress.StartEvening();
        else if (timeOfDay == TimeOfDay.Dawn) progress.StartDawn();
        else progress.StartDay();
    }
}

/// <summary>キャンプでも釣り場と同じ時間帯の確認ボタンを表示する</summary>
[CustomEditor(typeof(CampStageLifetimeScope))]
public sealed class CampStageLifetimeScopeEditor : FishingSceneLifetimeScopeEditor
{
}
