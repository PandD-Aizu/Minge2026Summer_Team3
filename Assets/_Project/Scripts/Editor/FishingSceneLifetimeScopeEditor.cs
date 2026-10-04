using _Project.Scripts.Core;
using LifetimeScopes;
using UnityEditor;
using UnityEngine;
using VContainer;

[CustomEditor(typeof(FishingSceneLifetimeScope))]
public sealed class FishingSceneLifetimeScopeEditor : UnityEditor.Editor
{
    /// <summary>通常の設定とPlay中の時間帯切り替えを表示する</summary>
    /// <example>FishingStageのLifetimeScopeをInspectorで選択する</example>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("時間帯の動作確認", EditorStyles.boldLabel);

        var scope = (FishingSceneLifetimeScope)target;
        bool available = Application.isPlaying && scope.Container != null &&
                         scope.Container.TryResolve<GameProgress>(out _);

        using (new EditorGUI.DisabledScope(!available))
        {
            if (GUILayout.Button("昼に切り替える")) SetTimeOfDay(scope, TimeOfDay.Day);
            if (GUILayout.Button("夜に切り替える")) SetTimeOfDay(scope, TimeOfDay.Night);
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("時間帯の切り替えはPlay中のみ有効", MessageType.Info);
    }

    /// <summary>共有の進行状態を更新し、購読中の画面と敵へ通知する</summary>
    /// <param name="scope">FishingStageのLifetimeScope</param>
    /// <param name="timeOfDay">切り替え先の時間帯</param>
    /// <example>Inspectorの昼・夜ボタンから呼ぶ</example>
    private static void SetTimeOfDay(FishingSceneLifetimeScope scope, TimeOfDay timeOfDay)
    {
        if (!scope.Container.TryResolve<GameProgress>(out var progress)) return;

        if (timeOfDay == TimeOfDay.Night) progress.StartNight();
        else progress.StartDay();
    }
}
