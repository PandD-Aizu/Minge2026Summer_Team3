using SceneLoadServices;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace EditorTools
{
    /// <summary>Scene参照をSceneアセット専用の選択欄として表示する</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        /// <summary>Scene選択欄とAddressablesの登録状態を表示する</summary>
        /// <param name="position">描画領域</param>
        /// <param name="property">Scene参照のシリアライズ情報</param>
        /// <param name="label">Inspectorの項目名</param>
        /// <example>出口のDestination欄へSceneをドラッグして設定する</example>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var guid = property.FindPropertyRelative("_guid");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid.stringValue));
            var field = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Scene以外を選べない欄にし、参照は名前ではなくGUIDで保存する
            EditorGUI.BeginChangeCheck();
            var selected = (SceneAsset)EditorGUI.ObjectField(field, label, scene, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                guid.stringValue = selected == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selected));

            string warning = GetWarning(guid.stringValue);
            if (warning != null)
            {
                var help = new Rect(position.x, field.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width, EditorGUIUtility.singleLineHeight * 2);
                EditorGUI.HelpBox(help, warning, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>警告の有無に合わせて項目の高さを返す</summary>
        /// <param name="property">Scene参照のシリアライズ情報</param>
        /// <param name="label">Inspectorの項目名</param>
        /// <returns>選択欄と必要な警告欄を含む高さ</returns>
        /// <example>未登録のSceneを選んだ場合は警告用に二行追加する</example>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight +
                (GetWarning(property.FindPropertyRelative("_guid").stringValue) == null
                    ? 0 : EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight * 2);
        }

        /// <summary>未設定や読み込めないSceneの設定理由を返す</summary>
        /// <param name="guid">選択されたSceneのGUID</param>
        /// <returns>設定に問題があれば警告文、正常ならnull</returns>
        /// <example>Addressables未登録の場合は登録を促す警告を表示する</example>
        private static string GetWarning(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "遷移先のSceneを選択してください";
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return "参照先のSceneが見つかりません";
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.FindAssetEntry(guid, true) == null)
                return "このSceneをAddressablesへ登録してください";
            return null;
        }
    }
}
