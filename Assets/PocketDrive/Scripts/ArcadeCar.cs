using System;
using UnityEngine;

namespace PocketDrive
{
    // Arcade handling: the collider has no friction and all grip, drive and braking happen here.
    // Input arrives through SetInput; this class never reads keys or touches.
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ArcadeCar : MonoBehaviour
    {
        [SerializeField] CarTuning tuning;
        [SerializeField] LayerMask groundMask = ~0;

        Rigidbody body;
        BoxCollider box;
        Vector3 spawn;
        Quaternion spawnRotation;
        float throttle;
        float steer;
        float smoothedSteer;
        float recoveryTimer;

        public event Action<float> Hit;
        public CarTuning Tuning => tuning;
        public bool Grounded { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SpeedKph => body == null ? 0 : body.linearVelocity.magnitude * 3.6f;

        public void SetInput(float throttleInput, float steerInput)
        {
            throttle = Mathf.Clamp(throttleInput, -1, 1);
            steer = Mathf.Clamp(steerInput, -1, 1);
        }

        void Awake()
        {
            if (tuning == null) tuning = ScriptableObject.CreateInstance<CarTuning>();
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            if (box.sharedMaterial == null)
                box.sharedMaterial = new PhysicsMaterial("Car (frictionless)")
                {
                    dynamicFriction = 0,
                    staticFriction = 0,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounciness = .15f,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (body.position.y < tuning.fallResetHeight) { ResetCar(); return; }
            recoveryTimer = Mathf.Max(0, recoveryTimer - dt);
            smoothedSteer = Mathf.MoveTowards(smoothedSteer, steer, tuning.steerResponse * dt);
            Grounded = CheckGround();

            Vector3 local = transform.InverseTransformDirection(body.linearVelocity);
            ForwardSpeed = local.z;
            if (!Grounded) return;

            float grip = recoveryTimer > 0 ? tuning.recoveryGripScale : 1f;
            local.x = Mathf.MoveTowards(local.x, 0, tuning.lateralGrip * grip * dt);
            DriveTarget(local.z, out float target, out float rate);
            local.z = Mathf.MoveTowards(local.z, target, rate * dt);
            body.linearVelocity = transform.TransformDirection(local);

            float speed01 = Mathf.Clamp01(Mathf.Abs(local.z) / tuning.maxSpeed);
            float turnRate = Mathf.Lerp(tuning.steerRateLow, tuning.steerRateHigh, speed01)
                * Mathf.Clamp01(Mathf.Abs(local.z) / tuning.fullSteerSpeed);
            float yaw = smoothedSteer * turnRate * Mathf.Sign(local.z) * Mathf.Deg2Rad;
            Vector3 spin = body.angularVelocity;
            spin.y = Mathf.MoveTowards(spin.y, yaw, tuning.yawResponse * Mathf.Deg2Rad * grip * dt);
            body.angularVelocity = spin;
        }

        // Pressing against the direction of travel brakes first; reverse only starts once nearly stopped.
        void DriveTarget(float forward, out float target, out float rate)
        {
            const float stopped = .5f;
            if (throttle > .01f)
            {
                bool braking = forward < -stopped;
                target = braking ? 0 : tuning.maxSpeed * throttle;
                rate = braking ? tuning.brakeDeceleration : tuning.acceleration;
            }
            else if (throttle < -.01f)
            {
                bool braking = forward > stopped;
                target = braking ? 0 : -tuning.maxReverseSpeed * -throttle;
                rate = braking ? tuning.brakeDeceleration : tuning.reverseAcceleration;
            }
            else
            {
                target = 0;
                rate = tuning.coastDeceleration;
            }
        }

        // A sphere cast starting inside the car's own collider never hits it, so no layer tricks are needed.
        bool CheckGround()
        {
            Vector3 halfSize = Vector3.Scale(box.size, transform.lossyScale) * .5f;
            float radius = Mathf.Min(halfSize.x, halfSize.y) * .8f;
            Vector3 origin = transform.TransformPoint(box.center);
            float distance = halfSize.y - radius + tuning.groundProbe;
            return Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance,
                       groundMask, QueryTriggerInteraction.Ignore)
                   && hit.normal.y >= tuning.minGroundNormalY;
        }

        void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (impulse < tuning.collisionImpulse) return;
            recoveryTimer = tuning.collisionRecovery;
            Hit?.Invoke(impulse);
        }

        public void ResetCar() => PlaceAt(spawn, spawnRotation);

        public void SetSpawn(Vector3 position, Quaternion rotation)
        {
            spawn = position;
            spawnRotation = rotation;
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            smoothedSteer = 0;
            recoveryTimer = 0;
            ForwardSpeed = 0;
        }
    }
}
