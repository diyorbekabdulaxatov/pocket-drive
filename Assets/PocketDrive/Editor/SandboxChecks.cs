using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketDrive.Editor
{
    // Headless integration check for the generated scene, contacts, driving, braking, steering and reset.
    public static class SandboxChecks
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCar>();
            var camera = UnityEngine.Object.FindFirstObjectByType<FollowCamera>();
            var hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHud>();
            Require(car != null && camera != null && hud != null, "Scene components missing");
            Require(car.GetComponent<CarInput>() != null, "Car has no CarInput");
            Require(car.Tuning != null, "Car has no tuning asset");
            Require(camera.target == car.transform && hud.car == car, "Scene references missing");
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                Require(t.name != "Lane marking" || t.GetComponent<Collider>() == null, "Lane marking has a collider");

            var body = car.GetComponent<Rigidbody>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ArcadeCar).GetMethod("Awake", flags).Invoke(car, null);
            var tick = typeof(ArcadeCar).GetMethod("FixedUpdate", flags);
            void Step(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    tick.Invoke(car, null);
                    Physics.Simulate(.02f);
                }
            }

            Vector3 spawn = body.position;
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();
                for (int i = 0; i < 50; i++) Physics.Simulate(.02f);
                Require(body.position.y > .2f && body.position.y < 1f, "Car did not settle on driving pad");
                Step(1);
                Require(car.Grounded, "Car is not grounded on the driving pad");

                car.SetInput(1, 0);
                Step(150);
                Require(body.position.z > spawn.z + 8f, "Car did not accelerate along the pad");
                Require(car.ForwardSpeed > car.Tuning.maxSpeed * .9f, $"Car only reached {car.ForwardSpeed:0.0} m/s");

                car.SetInput(-1, 0);
                Step(10);
                Require(car.ForwardSpeed > 0, "Brake switched to reverse before stopping");
                Step(100);
                Require(car.ForwardSpeed < 0, "Car did not reverse after stopping");

                car.ResetCar();
                Physics.SyncTransforms();
                float startHeading = car.transform.eulerAngles.y;
                car.SetInput(1, 1);
                Step(100);
                Require(Mathf.Abs(Mathf.DeltaAngle(startHeading, car.transform.eulerAngles.y)) > 20f, "Car did not steer");

                car.ResetCar();
                Physics.SyncTransforms();
                car.SetInput(1, 0);
                Step(400);
                Require(body.position.z < 46f, "Car passed through perimeter barrier");

                car.SetInput(0, 0);
                car.ResetCar();
                Require(Vector3.Distance(body.position, spawn) < .01f && body.linearVelocity.sqrMagnitude < .01f,
                    "Reset did not restore spawn and clear velocity");
                Debug.Log("POCKET_DRIVE_SANDBOX_CHECKS_OK: references, ground contact, top speed, brake before reverse, steering, barrier collision, reset");
            }
            finally { Physics.simulationMode = previousMode; }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
