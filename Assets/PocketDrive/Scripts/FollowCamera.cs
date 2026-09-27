using UnityEngine;

namespace PocketDrive
{
    // Chase camera that swings round smoothly, so spins and reversing don't whip the view.
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;
        [SerializeField] float distance = 9f;
        [SerializeField] float height = 6f;
        [SerializeField] float lookAhead = 3f;
        [SerializeField] float positionSharpness = 5f;
        [SerializeField] float headingSharpness = 3f;

        float heading;
        bool initialized;

        void LateUpdate()
        {
            if (target == null) return;
            float targetHeading = target.eulerAngles.y;
            if (!initialized)
            {
                heading = targetHeading;
                initialized = true;
            }
            heading = Mathf.LerpAngle(heading, targetHeading, 1f - Mathf.Exp(-headingSharpness * Time.deltaTime));
            Vector3 back = Quaternion.Euler(0, heading, 0) * Vector3.back;
            Vector3 desired = target.position + back * distance + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSharpness * Time.deltaTime));
            transform.LookAt(target.position - back * lookAhead);
        }

        public void Snap()
        {
            initialized = false;
            if (target == null) return;
            heading = target.eulerAngles.y;
            initialized = true;
            Vector3 back = Quaternion.Euler(0, heading, 0) * Vector3.back;
            transform.position = target.position + back * distance + Vector3.up * height;
            transform.LookAt(target.position - back * lookAhead);
        }
    }
}
