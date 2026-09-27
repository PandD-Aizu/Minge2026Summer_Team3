using UnityEngine;

namespace Dialogue
{
    [CreateAssetMenu(fileName = "DialogueData", menuName = "Data/Dialogue/DialogueData")]
    public class DialogueData : ScriptableObject
    {
        [SerializeField] private DialogueLine[] _lines;

        public int Count => _lines?.Length ?? 0;

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
