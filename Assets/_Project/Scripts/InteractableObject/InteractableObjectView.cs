using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.InteractableObject
{
    /// <summary>
    /// InteractableObjectのViewクラス
    /// </summary>
    public class InteractableObjectView : MonoBehaviour
    {
        [SerializeField] private Image _interactionImage;

        /// <summary>
        /// InteractionImageの表示/非表示を切り替える
        /// </summary>
        /// <param name="isShow">trueなら表示、falseなら非表示</param>
        public void ShowInteractionImage(bool isShow)
        {
            _interactionImage.DOFade(isShow ? 1f : 0f, 0.5f);
        }
    }
}
