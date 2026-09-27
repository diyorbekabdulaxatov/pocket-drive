using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketDrive.Editor
{
    // Adds AI traffic and pedestrians to a generated city scene.
    // Pedestrians appear only once a character model (for example a Mixamo FBX) is in CharactersFolder.
    public static class CityPopulation
    {
        public const string CharactersFolder = "Assets/PocketDrive/Characters";
        const string ControllerPath = CharactersFolder + "/Walker.controller";

        public static void Add(Transform player)
        {
            var root = new GameObject("City population");
            var traffic = new GameObject("Traffic").AddComponent<TrafficSystem>();
            traffic.transform.SetParent(root.transform);
            traffic.player = player;
            var trafficSettings = new SerializedObject(traffic);
            trafficSettings.FindProperty("carTemplate").objectReferenceValue = CarTemplate(traffic.transform);
            trafficSettings.ApplyModifiedPropertiesWithoutUndo();

            var pedestrians = new GameObject("Pedestrians").AddComponent<PedestrianSystem>();
            pedestrians.transform.SetParent(root.transform);
            pedestrians.player = player;
            var pedestrianSettings = new SerializedObject(pedestrians);
            pedestrianSettings.FindProperty("traffic").objectReferenceValue = traffic;
            pedestrianSettings.FindProperty("walkerTemplate").objectReferenceValue = WalkerTemplate(pedestrians.transform);
            pedestrianSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CarTemplate(Transform parent)
        {
            var car = new GameObject("Traffic car template");
            car.transform.SetParent(parent, false);
            var box = car.AddComponent<BoxCollider>();
            box.size = new Vector3(1.7f, .6f, 3.4f);
            box.center = new Vector3(0, .3f, 0);
            var body = car.AddComponent<Rigidbody>();
            body.mass = 1400;
            body.isKinematic = true;
            if (!RealisticLook.AttachPorsche(car.transform))
            {
                var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(shell.GetComponent<Collider>());
                shell.transform.SetParent(car.transform, false);
                shell.transform.localPosition = box.center;
                shell.transform.localScale = box.size;
            }
            car.SetActive(false);
            return car;
        }

        // Uses the first rigged character model in CharactersFolder and the first clip whose name contains "walk".
        static GameObject WalkerTemplate(Transform parent)
        {
            if (!Directory.Exists(CharactersFolder)) return null;
            var models = AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            if (models.Length == 0) return null;

            foreach (string path in models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool isWalk = Path.GetFileName(path).ToLowerInvariant().Contains("walk");
                bool changed = importer.animationType != ModelImporterAnimationType.Human;
                importer.animationType = ModelImporterAnimationType.Human;
                if (isWalk)
                {
                    var clips = importer.defaultClipAnimations;
                    foreach (var c in clips) { c.loopTime = true; c.lockRootPositionXZ = true; }
                    importer.clipAnimations = clips;
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }

            var walk = models
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
                .FirstOrDefault(c => !c.name.StartsWith("__preview") && c.name.ToLowerInvariant().Contains("walk")
                                     || !c.name.StartsWith("__preview") && c.name.ToLowerInvariant().Contains("mixamo"));
            string characterPath = models.FirstOrDefault(p => !Path.GetFileName(p).ToLowerInvariant().Contains("walk")) ?? models[0];
            var character = AssetDatabase.LoadAssetAtPath<GameObject>(characterPath);
            if (character == null) return null;

            var walker = new GameObject("Pedestrian template");
            walker.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(character, walker.transform);
            model.transform.localPosition = Vector3.zero;
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            if (walk != null)
            {
                var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, walk);
                animator.runtimeAnimatorController = controller;
            }
            var capsule = walker.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = .3f;
            capsule.center = new Vector3(0, .9f, 0);
            walker.AddComponent<Rigidbody>().isKinematic = true;
            walker.SetActive(false);
            return walker;
        }

        // Headless check: traffic drives the grid for 60 simulated seconds without leaving the roads or overlapping.
        public static void Checks()
        {
            EditorSceneManager.OpenScene(CoastalCityBuilder.ScenePath);
            var traffic = Object.FindAnyObjectByType<TrafficSystem>(FindObjectsInactive.Include);
            Require(traffic != null, "City has no traffic system");
            var player = Object.FindAnyObjectByType<ArcadeCar>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var carAwake = typeof(ArcadeCar).GetMethod("Awake", flags);
            carAwake.Invoke(player, null);
            typeof(TrafficSystem).GetMethod("Start", flags).Invoke(traffic, null);
            Require(traffic.enabled && traffic.ActiveCars > 0, "Traffic did not start");
            var tick = typeof(TrafficSystem).GetMethod("FixedUpdate", flags);

            var cars = traffic.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject.activeInHierarchy).ToArray();
            var starts = cars.Select(c => c.position).ToArray();
            float[] travelled = new float[cars.Length];
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();
                for (int step = 0; step < 3000; step++)
                {
                    var before = cars.Select(c => c.position).ToArray();
                    tick.Invoke(traffic, null);
                    Physics.Simulate(.02f);
                    for (int i = 0; i < cars.Length; i++)
                    {
                        float moved = Vector3.Distance(before[i], cars[i].position);
                        if (moved < 5f) travelled[i] += moved; // ignore respawn jumps
                        Vector3 p = cars[i].position;
                        Require(OnRoad(p), $"{cars[i].name} left the road at {p}");
                        for (int j = i + 1; j < cars.Length; j++)
                            Require(Vector3.Distance(p, cars[j].position) > 3f, $"{cars[i].name} overlaps {cars[j].name} at {p}");
                    }
                }
            }
            finally { Physics.simulationMode = previousMode; }

            int moving = travelled.Count(d => d > 150f);
            Require(moving >= cars.Length * 3 / 4, $"Only {moving} of {cars.Length} cars kept moving");
            Debug.Log($"POCKET_DRIVE_TRAFFIC_CHECKS_OK: {cars.Length} cars, {moving} drove over 150 m in 60 s, stayed on roads, no overlaps");
        }

        // Renders traffic after 20 simulated seconds, looking down Palm Boulevard from above the player.
        public static void Preview()
        {
            EditorSceneManager.OpenScene(CoastalCityBuilder.ScenePath);
            var traffic = Object.FindAnyObjectByType<TrafficSystem>(FindObjectsInactive.Include);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TrafficSystem).GetMethod("Start", flags).Invoke(traffic, null);
            var tick = typeof(TrafficSystem).GetMethod("FixedUpdate", flags);
            for (int i = 0; i < 1000; i++) tick.Invoke(traffic, null);
            Physics.SyncTransforms();
            var cam = Camera.main;
            cam.GetComponent<FollowCamera>().enabled = false;
            var nearest = traffic.GetComponentsInChildren<Rigidbody>()
                .Where(b => b.gameObject.activeInHierarchy)
                .OrderBy(b => Vector3.Distance(b.position, traffic.player.position)).First();
            cam.transform.position = nearest.position + new Vector3(9, 5, -14);
            cam.transform.LookAt(nearest.position + Vector3.up);
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            File.WriteAllBytes("outputs/coastal-city-traffic.png", image.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
            Debug.Log("POCKET_DRIVE_TRAFFIC_PREVIEW_OK");
        }

        static bool OnRoad(Vector3 p)
        {
            const float half = 13.5f;
            bool nearLine(float v) => Enumerable.Range(0, 5).Any(i => Mathf.Abs(v - (-300 + i * 150)) < half);
            return (nearLine(p.x) || nearLine(p.z)) && p.y > -.5f && p.y < 1f;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
