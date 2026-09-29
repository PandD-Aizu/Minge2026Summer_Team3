using UnityEngine;

namespace Dialogue
{
    [CreateAssetMenu(fileName = "DialogueData", menuName = "Data/Dialogue/DialogueData")]
    public class DialogueData : ScriptableObject
    {
        [SerializeField] private DialogueLine[] _lines;
        [SerializeField, Tooltip("表示開始時にセーブへ記録し、同じセーブでは再表示しない（本編用Controllerで有効）")]
        private bool _playOncePerSave;
        [SerializeField, HideInInspector] private string _saveId;

        public int Count => _lines?.Length ?? 0;
        public bool PlayOncePerSave => _playOncePerSave;
        public string SaveId => _saveId;

#if UNITY_EDITOR
        /// <summary>アセットGUIDを保存IDとして保持し、複製した会話には別のIDを割り当てる</summary>
        /// <example>アセットの作成・再読み込み時にUnityが呼ぶ</example>
        private void OnValidate()
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path)) _saveId = UnityEditor.AssetDatabase.AssetPathToGUID(path);
        }
#endif

        /// <summary>指定した位置のセリフを取得する</summary>
        /// <param name="index">0以上Count未満のセリフ番号</param>
        /// <returns>指定したセリフ</returns>
        /// <example>dialogue.GetLine(0)</example>
        public DialogueLine GetLine(int index)
        {
            return _lines[index];
        }
    }
}
