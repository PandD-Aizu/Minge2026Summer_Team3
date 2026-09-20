using System;
using UnityEngine;

namespace Dialogue
{
    // 人負のセリフを記録する
    [Serializable]
    public class DialogueLine
    {
        [SerializeField] private Speaker _speaker;

        [SerializeField, TextArea(2, 5)]
        private string _text;

        public Speaker Speaker => _speaker;
        public string Text => _text;
    }
}
