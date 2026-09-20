using R3;
using UnityEngine;

namespace _Project.Scripts.InteractableObject
{
    /// <summary>
    /// InteractableObjectの接続用コンポーネント
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class InteractableConnector : MonoBehaviour
    {
        public readonly Subject<Unit> OnTriggerStayObservable = new Subject<Unit>();
        public readonly Subject<Unit> OnTriggerExitObservable = new Subject<Unit>();

        public void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                OnTriggerStayObservable?.OnNext(Unit.Default);
            }
        }

        public void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                OnTriggerExitObservable?.OnNext(Unit.Default);
            }
        }
    }
}
