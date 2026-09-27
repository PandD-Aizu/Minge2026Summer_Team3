using _Project.Scripts.InteractableObject;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 魚影がインタラクトできる状態か判断する
    /// </summary>
    public class FishingShadowInteractableTarget : MonoBehaviour, IInteractableTarget
    {
        [SerializeField] private InteractableConnector _connector;

        public bool CanInteract => _connector != null && _connector.IsPlayerNearby;
    }
}
