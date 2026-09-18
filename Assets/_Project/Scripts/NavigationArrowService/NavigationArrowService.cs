using UnityEngine;

namespace NavigationArrowServices
{
    public class NavigationArrowService
    {
        public void UpdateArrowTransform()
        {

        }

        /// <summary>
        /// XZ平面上のfromからtoへの方向を計算し、角度を返す
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private float CalculateArrowDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.Normalize();
            return Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        }
    }
}
