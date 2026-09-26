using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketDrive.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/PocketDrive/Scenes/DrivingSandbox.unity";

        [MenuItem("Pocket Drive/Configure Android Project")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Pocket Drive";
            PlayerSettings.productName = "Pocket Drive";
            PlayerSettings.bundleVersion = "0.1.0";
            // Development placeholder: choose your owned identifier before store registration.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pocketdrive.prototype");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            QualitySettings.vSyncCount = 0;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            if (!File.Exists(ScenePath)) CreateSandbox();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("POCKET_DRIVE_SETUP_OK");
        }

        static Material Material(string name, Color color)
        {
            string path = $"Assets/PocketDrive/Art/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", .15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        static void CreateSandbox()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var grass = Material("Grass", new Color(.23f, .42f, .34f));
            var road = Material("Asphalt", new Color(.12f, .16f, .20f));
            var white = Material("Ivory", new Color(.9f, .89f, .79f));
            var orange = Material("CarOrange", new Color(1f, .36f, .12f));
            var dark = Material("Rubber", new Color(.04f, .06f, .08f));
            var glass = Material("Glass", new Color(.15f, .34f, .44f));
            Box("Ground", new Vector3(0, -.5f, 0), new Vector3(100, 1, 140), grass);
            Box("Driving pad", new Vector3(0, .025f, 0), new Vector3(50, .05f, 90), road);
            for (int z = -40; z <= 40; z += 8)
                Box("Lane marking", new Vector3(0, .065f, z), new Vector3(.2f, .02f, 3), white);
            Box("Left barrier", new Vector3(-26, .5f, 0), new Vector3(1, 1, 92), white);
            Box("Right barrier", new Vector3(26, .5f, 0), new Vector3(1, 1, 92), white);
            Box("North barrier", new Vector3(0, .5f, 46), new Vector3(53, 1, 1), white);
            Box("South barrier", new Vector3(0, .5f, -46), new Vector3(53, 1, 1), white);
            for (int i = 0; i < 6; i++)
                Box("Practice obstacle", new Vector3(i % 2 == 0 ? -10 : 10, .6f, -20 + i * 9), new Vector3(2, 1.2f, 2), orange);
            var vehicle = new GameObject("Player Car");
            vehicle.transform.position = new Vector3(-5, .65f, -32);
            var collider = vehicle.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.7f, .6f, 3.4f);
            vehicle.AddComponent<Rigidbody>().mass = 1000;
            var car = vehicle.AddComponent<ArcadeCar>();
            var chassis = Box("Body", Vector3.zero, new Vector3(1.7f, .6f, 3.4f), orange, vehicle.transform);
            UnityEngine.Object.DestroyImmediate(chassis.GetComponent<Collider>());
            var cabin = Box("Cabin", new Vector3(0, .55f, -.15f), new Vector3(1.4f, .6f, 1.6f), glass, vehicle.transform);
            UnityEngine.Object.DestroyImmediate(cabin.GetComponent<Collider>());
            foreach (float x in new[] { -.86f, .86f })
                foreach (float z in new[] { -1.05f, 1.05f })
                {
                    var wheel = Box("Wheel", new Vector3(x, -.15f, z), new Vector3(.25f, .55f, .65f), dark, vehicle.transform);
                    UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
                }
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.backgroundColor = new Color(.53f, .72f, .8f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.farClipPlane = 200;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = vehicle.transform.position + new Vector3(0, 6, -9);
            cameraObject.transform.LookAt(vehicle.transform.position);
            cameraObject.AddComponent<FollowCamera>().target = vehicle.transform;
            new GameObject("Prototype HUD").AddComponent<PrototypeHud>().car = car;
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .7f, .76f);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        }

        [MenuItem("Pocket Drive/Build Android Development APK")]
        public static void BuildAndroid()
        {
            Configure();
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/PocketDrive-development.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Android build failed: " + report.summary.result);
            Debug.Log("POCKET_DRIVE_ANDROID_BUILD_OK");
        }
    }
}
