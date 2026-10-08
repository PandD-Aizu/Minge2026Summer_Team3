using TMPro;
using UnityEngine;

namespace _Project.Scripts.View
{
    public class MiniGameResultView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TextMeshProUGUI _resultText;

        void Start()
        {
            _canvas.enabled = false;
        }

        public void GreatResult()
        {
            BringToFront();
            _canvas.enabled = true;
            _resultText.enabled = true;
            _resultText.text = "Great!";
        }

        public void GoodResult()
        {
            BringToFront();
            _canvas.enabled = true;
            _resultText.enabled = true;
            _resultText.text = "Good!";
        }

        public void MissResult()
        {
            BringToFront();
            _canvas.enabled = true;
            _resultText.enabled = true;
            _resultText.text = "Miss!";
        }

        public void HideResult()
        {
            _resultText.enabled = false;
        }

        private void BringToFront()
        {
            transform.SetAsLastSibling();
            _resultText.transform.SetAsLastSibling();
        }
    }


}
