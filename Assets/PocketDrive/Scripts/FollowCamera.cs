using UnityEngine;

namespace PocketDrive
{
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;
        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position - target.forward * 9f + Vector3.up * 6f;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-5f * Time.deltaTime));
            transform.LookAt(target.position + target.forward * 3f);
        }
    }
}
