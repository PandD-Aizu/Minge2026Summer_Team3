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
            if (GUILayout.Button("朝7:00まで進める")) SetTimeOfDay(scope, TimeOfDay.Day);
            if (GUILayout.Button("夕方17:00まで進める")) SetTimeOfDay(scope, TimeOfDay.Evening);
            if (GUILayout.Button("夜19:00まで進める")) SetTimeOfDay(scope, TimeOfDay.Night);
            if (GUILayout.Button("明け方4:00まで進める")) SetTimeOfDay(scope, TimeOfDay.Dawn);
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("時間帯の切り替えはPlay中のみ有効", MessageType.Info);
        else
            EditorGUILayout.HelpBox("時計も指定時刻まで進む 過ぎた時刻は翌日、同時刻は変更なし", MessageType.Info);
    }

    /// <summary>共有の時計を指定時間帯の開始時刻まで進め、表示と敵の時間帯を一致させる</summary>
    /// <param name="scope">FishingStageのLifetimeScope</param>
    /// <param name="timeOfDay">切り替え先の時間帯</param>
    /// <example>Inspectorの朝・夕方・夜・明け方ボタンから呼ぶ</example>
    private static void SetTimeOfDay(LifetimeScope scope, TimeOfDay timeOfDay)
    {
        if (!scope.Container.TryResolve<GameProgress>(out var progress)) return;

        progress.AdvanceToTimeOfDay(timeOfDay);
    }
}

/// <summary>キャンプでも釣り場と同じ時間帯の確認ボタンを表示する</summary>
[CustomEditor(typeof(CampStageLifetimeScope))]
public sealed class CampStageLifetimeScopeEditor : FishingSceneLifetimeScopeEditor
{
}
