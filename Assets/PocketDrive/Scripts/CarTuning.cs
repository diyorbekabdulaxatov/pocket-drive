using UnityEngine;

namespace PocketDrive
{
    // All handling numbers for one car. Speeds in m/s, rates in m/s² or degrees/s.
    [CreateAssetMenu(menuName = "Pocket Drive/Car Tuning", fileName = "CarTuning")]
    public sealed class CarTuning : ScriptableObject
    {
        [Header("Speed")]
        public float maxSpeed = 24f;
        public float maxReverseSpeed = 8f;
        public float acceleration = 14f;
        public float reverseAcceleration = 8f;
        public float brakeDeceleration = 26f;
        public float coastDeceleration = 5f;

        [Header("Steering")]
        [Tooltip("Turn rate at low speed.")] public float steerRateLow = 110f;
        [Tooltip("Turn rate at max speed.")] public float steerRateHigh = 55f;
        [Tooltip("Below this speed turning fades out, so a parked car cannot spin on the spot.")]
        public float fullSteerSpeed = 4f;
        [Tooltip("How fast the steering input follows the controls (per second).")]
        public float steerResponse = 6f;
        public float yawResponse = 900f;

        [Header("Grip")]
        public float lateralGrip = 18f;
        [Tooltip("Seconds of reduced grip after a hard hit, so the car slides off walls.")]
        public float collisionRecovery = .35f;
        public float collisionImpulse = 2500f;
        [Range(0, 1)] public float recoveryGripScale = .25f;

        [Header("Ground")]
        public float groundProbe = .12f;
        [Range(0, 1)] public float minGroundNormalY = .7f;
        public float fallResetHeight = -5f;
    }
}
