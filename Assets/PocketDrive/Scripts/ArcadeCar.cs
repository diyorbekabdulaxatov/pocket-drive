using UnityEngine;

namespace PocketDrive
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeCar : MonoBehaviour
    {
        [SerializeField] float acceleration = 14f;
        [SerializeField] float maxSpeed = 24f;
        [SerializeField] float steeringRate = 95f;
        Rigidbody body;
        Vector3 spawn;
        Quaternion spawnRotation;
        float throttle;
        float steering;
        public float SpeedKph => body == null ? 0 : body.linearVelocity.magnitude * 3.6f;

        public static Rect ControlRect(int index)
        {
            Rect safe = Screen.safeArea;
            float size = Mathf.Min(110f, safe.width / 7f);
            float left = safe.x + 16f;
            float right = safe.xMax - 16f;
            float bottom = Screen.height - safe.y - size - 16f;
            float x = index < 2 ? left + index * (size + 12f)
                : right - (4 - index) * size - (3 - index) * 12f;
            return new Rect(x, bottom, size, size);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void Update()
        {
            steering = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            throttle = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                ApplyPointer(new Vector2(touch.position.x, Screen.height - touch.position.y));
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                ApplyPointer(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            steering = Mathf.Clamp(steering, -1, 1);
            throttle = Mathf.Clamp(throttle, -1, 1);
            if (Input.GetKeyDown(KeyCode.R) || transform.position.y < -5) ResetCar();
        }

        void ApplyPointer(Vector2 point)
        {
            if (ControlRect(0).Contains(point)) steering -= 1;
            if (ControlRect(1).Contains(point)) steering += 1;
            if (ControlRect(2).Contains(point)) throttle -= 1;
            if (ControlRect(3).Contains(point)) throttle += 1;
        }

        void FixedUpdate()
        {
            // Cast from beneath the chassis to avoid hitting this car's own collider.
            if (!Physics.Raycast(body.position + Vector3.down * .31f, Vector3.down, .45f)) return;
            Vector3 local = transform.InverseTransformDirection(body.linearVelocity);
            local.x = Mathf.MoveTowards(local.x, 0, 18f * Time.fixedDeltaTime);
            local.z = Mathf.MoveTowards(local.z, throttle * (throttle < 0 ? maxSpeed * .35f : maxSpeed),
                (Mathf.Abs(throttle) > .01f ? acceleration : 5f) * Time.fixedDeltaTime);
            body.linearVelocity = transform.TransformDirection(local);
            float turn = steering * steeringRate * Mathf.Clamp01(Mathf.Abs(local.z) / 5f) * Mathf.Sign(local.z);
            body.MoveRotation(body.rotation * Quaternion.Euler(0, turn * Time.fixedDeltaTime, 0));
        }

        public void ResetCar()
        {
            body.position = spawn;
            body.rotation = spawnRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
