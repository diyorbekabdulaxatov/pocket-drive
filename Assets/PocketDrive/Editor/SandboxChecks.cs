using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketDrive.Editor
{
    // Headless integration check for the generated scene, contacts, driving and reset.
    public static class SandboxChecks
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCar>();
            var camera = UnityEngine.Object.FindFirstObjectByType<FollowCamera>();
            var hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHud>();
            Require(car != null && camera != null && hud != null, "Scene components missing");
            Require(camera.target == car.transform && hud.car == car, "Scene references missing");
            var body = car.GetComponent<Rigidbody>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ArcadeCar).GetMethod("Awake", flags).Invoke(car, null);
            var tick = typeof(ArcadeCar).GetMethod("FixedUpdate", flags);
            var throttle = typeof(ArcadeCar).GetField("throttle", flags);
            Vector3 spawn = body.position;
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();
                for (int i = 0; i < 50; i++) Physics.Simulate(.02f);
                Require(body.position.y > .2f && body.position.y < 1f, "Car did not settle on driving pad");
                throttle.SetValue(car, 1f);
                for (int i = 0; i < 150; i++)
                {
                    tick.Invoke(car, null);
                    Physics.Simulate(.02f);
                }
                Require(body.position.z > spawn.z + 8f, "Car did not accelerate along the pad");
                for (int i = 0; i < 250; i++)
                {
                    tick.Invoke(car, null);
                    Physics.Simulate(.02f);
                }
                Require(body.position.z < 46f, "Car passed through perimeter barrier");
                car.ResetCar();
                Require(Vector3.Distance(body.position, spawn) < .01f && body.linearVelocity.sqrMagnitude < .01f,
                    "Reset did not restore spawn and clear velocity");
                Debug.Log("POCKET_DRIVE_SANDBOX_CHECKS_OK: references, ground contact, acceleration, barrier collision, reset");
            }
            finally { Physics.simulationMode = previousMode; }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
