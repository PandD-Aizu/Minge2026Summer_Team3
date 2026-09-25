using UnityEngine;

namespace Dialogue
{
    [CreateAssetMenu(fileName = "DialogueData", menuName = "Data/Dialogue/DialogueData")]
    public class DialogueData : ScriptableObject
    {
        [SerializeField] private DialogueLine[] _lines;

        public int Count => _lines.Length;

        public DialogueLine GetLine(int index) {

            return _lines[index];
        }
    }
}
