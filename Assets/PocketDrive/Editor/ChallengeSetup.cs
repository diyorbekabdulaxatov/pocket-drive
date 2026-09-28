using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketDrive.Editor
{
    // Adds the parking challenge to the Coastal City car park. The lot geometry comes from the same constants.
    public static class ChallengeSetup
    {
        public const float LotCentreZ = -370f, LotHalfWidth = 40f, LotHalfDepth = 25f, BayWidth = 2.7f, BayDepth = 5.5f;
        const float FirstLineX = -37.8f;
        const int LineCount = 29;
        const string MarkerMaterialPath = "Assets/PocketDrive/Settings/Rendering/TargetMarker.mat";

        // Row centres and the heading a car faces when parked nose-in from the adjacent aisle.
        static readonly (float z, float heading)[] Rows =
        {
            (LotCentreZ + LotHalfDepth - BayDepth / 2, 0f),   // north row, entered from the aisle to its south
            (LotCentreZ + BayDepth / 2, 180f),                 // middle rows, back to back
            (LotCentreZ - BayDepth / 2, 0f),
            (LotCentreZ - LotHalfDepth + BayDepth / 2, 180f),  // south row
        };

        public static ParkingChallenge.Bay[] Bays()
        {
            var bays = new List<ParkingChallenge.Bay>();
            for (int r = 0; r < Rows.Length; r++)
                for (int i = 0; i < LineCount - 1; i++)
                {
                    float x = FirstLineX + (i + .5f) * BayWidth;
                    if (r == 0 && Mathf.Abs(x) < 9f) continue; // entrance gap in the north row
                    bays.Add(new ParkingChallenge.Bay { centre = new Vector3(x, .07f, Rows[r].z), heading = Rows[r].heading });
                }
            return bays.ToArray();
        }

        public static void AddParking(ArcadeCar car, FollowCamera camera) =>
            AddParking(car, camera, Bays(), new Vector3(0, .42f, LotCentreZ + LotHalfDepth + 3f), 180f,
                new Vector2(BayWidth, BayDepth));

        public static ParkingChallenge AddParking(ArcadeCar car, FollowCamera camera, ParkingChallenge.Bay[] bays,
            Vector3 start, float startHeading, Vector2 baySize)
        {
            var challenge = new GameObject("Parking challenge").AddComponent<ParkingChallenge>();
            var settings = new SerializedObject(challenge);
            settings.FindProperty("car").objectReferenceValue = car;
            settings.FindProperty("followCamera").objectReferenceValue = camera;
            settings.FindProperty("marker").objectReferenceValue = Marker(challenge.transform, baySize);
            settings.FindProperty("startPosition").vector3Value = start;
            settings.FindProperty("startHeading").floatValue = startHeading;
            settings.FindProperty("baySize").vector2Value = baySize;
            var list = settings.FindProperty("bays");
            list.arraySize = bays.Length;
            for (int i = 0; i < bays.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("centre").vector3Value = bays[i].centre;
                element.FindPropertyRelative("heading").floatValue = bays[i].heading;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            return challenge;
        }

        // Green outline on the ground plus a floating diamond; the last child is the diamond (it bobs at runtime).
        static Transform Marker(Transform parent, Vector2 baySize)
        {
            var marker = new GameObject("Target bay marker").transform;
            marker.SetParent(parent, false);
            var material = MarkerMaterial();
            void Part(string name, Vector3 position, Vector3 scale, Quaternion rotation)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(marker, false);
                part.transform.localPosition = position;
                part.transform.localRotation = rotation;
                part.transform.localScale = scale;
                var renderer = part.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            float w = baySize.x - .2f, d = baySize.y - .2f;
            Part("Left edge", new Vector3(-w / 2, .01f, 0), new Vector3(.18f, .02f, d), Quaternion.identity);
            Part("Right edge", new Vector3(w / 2, .01f, 0), new Vector3(.18f, .02f, d), Quaternion.identity);
            Part("Back edge", new Vector3(0, .01f, d / 2), new Vector3(w, .02f, .18f), Quaternion.identity);
            Part("Front edge", new Vector3(0, .01f, -d / 2), new Vector3(w, .02f, .18f), Quaternion.identity);
            Part("Arrow", new Vector3(0, 2.6f, 0), new Vector3(.55f, .55f, .55f), Quaternion.Euler(45, 0, 45));
            return marker;
        }

        static Material MarkerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
            if (material != null) return material;
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerMaterialPath));
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(unlit) { color = new Color(.2f, 1f, .35f) };
            AssetDatabase.CreateAsset(material, MarkerMaterialPath);
            return material;
        }

        // Headless check: a car placed squarely in the target bay wins with 3 stars; one parked crooked or
        // in the wrong bay does not; running out of time fails.
        public static void Checks() => RunChecks(CoastalCityBuilder.ScenePath);

        public static void RunChecks(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);
            var challenge = Object.FindAnyObjectByType<ParkingChallenge>(FindObjectsInactive.Include);
            Require(challenge != null, "City has no parking challenge");
            var car = Object.FindAnyObjectByType<ArcadeCar>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ArcadeCar).GetMethod("Awake", flags).Invoke(car, null);
            typeof(ParkingChallenge).GetMethod("Awake", flags).Invoke(challenge, null);
            typeof(ParkingChallenge).GetMethod("OnEnable", flags).Invoke(challenge, null);
            var carTick = typeof(ArcadeCar).GetMethod("FixedUpdate", flags);
            var challengeTick = typeof(ParkingChallenge).GetMethod("FixedUpdate", flags);
            var bays = challenge.Bays;
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                void Run(int steps)
                {
                    for (int i = 0; i < steps; i++)
                    {
                        carTick.Invoke(car, null);
                        challengeTick.Invoke(challenge, null);
                        Physics.Simulate(.02f);
                    }
                }
                void ParkIn(ParkingChallenge.Bay bay, float yawOffset)
                {
                    car.PlaceAt(bay.centre + Vector3.up * .4f, Quaternion.Euler(0, bay.heading + yawOffset, 0));
                    Physics.SyncTransforms();
                }

                // The start spot must be inside the lot entrance and on the ground.
                int target = bays.Length / 3, other = bays.Length - 2;
                challenge.Begin(target);
                Run(60);
                Require(challenge.Current == ParkingChallenge.State.Running, "Challenge ended while waiting at the start");
                Require(car.Grounded, "Car is not on the ground at the challenge start");

                ParkIn(bays[target], 0);
                Run(100);
                Require(challenge.Current == ParkingChallenge.State.Won && challenge.Stars == 3,
                    $"Clean park in the target bay gave {challenge.Current} with {challenge.Stars} stars");

                challenge.Begin(target);
                ParkIn(bays[target], 40);
                Run(100);
                Require(challenge.Current == ParkingChallenge.State.Running, "A crooked car counted as parked");

                ParkIn(bays[other], 0);
                Run(100);
                Require(challenge.Current == ParkingChallenge.State.Running, "Parking in the wrong bay counted");

                Run(3100);
                Require(challenge.Current == ParkingChallenge.State.Failed, "Challenge did not time out");
                challenge.Stop();
            }
            finally { Physics.simulationMode = previousMode; }
            Debug.Log($"POCKET_DRIVE_PARKING_CHECKS_OK: {bays.Length} bays; clean park 3 stars, crooked and wrong bay rejected, timeout fails");
        }

        // Renders the challenge start: the view from the car park entrance with the target bay highlighted.
        public static void Preview()
        {
            EditorSceneManager.OpenScene(CoastalCityBuilder.ScenePath);
            var challenge = Object.FindAnyObjectByType<ParkingChallenge>(FindObjectsInactive.Include);
            var car = Object.FindAnyObjectByType<ArcadeCar>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ArcadeCar).GetMethod("Awake", flags).Invoke(car, null);
            typeof(ParkingChallenge).GetMethod("Awake", flags).Invoke(challenge, null);
            challenge.Begin(35);
            var cam = Camera.main;
            cam.GetComponent<FollowCamera>().enabled = false;
            Vector3 back = -car.transform.forward;
            CityPopulation.Capture(cam, car.transform.position + back * 8f + Vector3.up * 4.5f,
                car.transform.position - back * 12f, "outputs/coastal-city-parking-challenge.png");
            Debug.Log("POCKET_DRIVE_PARKING_PREVIEW_OK");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
