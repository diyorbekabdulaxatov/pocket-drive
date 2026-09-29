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
            var templates = WalkerTemplates(pedestrians.transform);
            var list = pedestrianSettings.FindProperty("walkerTemplates");
            list.arraySize = templates.Length;
            for (int i = 0; i < templates.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];
            pedestrianSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static GameObject CarTemplate(Transform parent)
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

        // One template per character model in CharactersFolder (glTF/GLB via glTFast, or FBX such as Mixamo).
        // Each uses a clip whose name contains "walk", from its own file or from a separate walk-only FBX.
        static GameObject[] WalkerTemplates(Transform parent)
        {
            if (!Directory.Exists(CharactersFolder)) return Array.Empty<GameObject>();
            var models = AssetDatabase.FindAssets("t:GameObject", new[] { CharactersFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".glb") || p.EndsWith(".gltf") || p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(p => p).ToArray();
            foreach (string path in models.Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)))
                PrepareFbx(path);

            AnimationClip sharedWalk = models.Where(IsWalkOnly).SelectMany(WalkClips).FirstOrDefault();
            var templates = new System.Collections.Generic.List<GameObject>();
            foreach (string path in models.Where(p => !IsWalkOnly(p)))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) continue;
                var walk = WalkClips(path).FirstOrDefault() ?? sharedWalk;
                string name = Path.GetFileNameWithoutExtension(path);

                var walker = new GameObject($"Pedestrian template ({name})");
                walker.transform.SetParent(parent, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, walker.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                FitHeight(walker.transform, model, 1.75f);

                var animator = model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                if (walk != null)
                    animator.runtimeAnimatorController = AnimatorController.CreateAnimatorControllerAtPathWithClip(
                        $"{CharactersFolder}/{name}_Walk.controller", walk);

                var capsule = walker.AddComponent<CapsuleCollider>();
                capsule.height = 1.8f;
                capsule.radius = .3f;
                capsule.center = new Vector3(0, .9f, 0);
                walker.AddComponent<Rigidbody>().isKinematic = true;
                walker.SetActive(false);
                templates.Add(walker);
            }
            return templates.ToArray();
        }

        // Plain "Walk" beats variants such as "Walk_Carry".
        static int WalkRank(string clip)
        {
            string n = clip.ToLowerInvariant();
            string last = n.Substring(n.LastIndexOf('|') + 1);
            return last == "walk" ? 0 : n.Contains("walk") && !n.Contains("carry") ? 1 : n.Contains("walk") ? 2 : 3;
        }

        static bool IsWalkOnly(string path) =>
            path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).ToLowerInvariant().Contains("walk");

        static System.Collections.Generic.IEnumerable<AnimationClip> WalkClips(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview"))
                .OrderBy(c => WalkRank(c.name))
                .Where(c => c.name.ToLowerInvariant().Contains("walk") || IsWalkOnly(path));

        // Mixamo FBX: humanoid rig; the walk clip loops in place.
        static void PrepareFbx(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.animationType != ModelImporterAnimationType.Human;
            importer.animationType = ModelImporterAnimationType.Human;
            if (IsWalkOnly(path))
            {
                var clips = importer.defaultClipAnimations;
                foreach (var c in clips) { c.loopTime = true; c.lockRootPositionXZ = true; }
                importer.clipAnimations = clips;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        // Scales the model so its height matches a real person and its feet sit at the template's origin.
        static void FitHeight(Transform root, GameObject model, float height)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > .01f) model.transform.localScale *= height / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.position += new Vector3(root.position.x - bounds.center.x, root.position.y - bounds.min.y, root.position.z - bounds.center.z);
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

        // Renders traffic and pedestrians after 20 simulated seconds.
        public static void Preview()
        {
            EditorSceneManager.OpenScene(CoastalCityBuilder.ScenePath);
            var traffic = Object.FindAnyObjectByType<TrafficSystem>(FindObjectsInactive.Include);
            var people = Object.FindAnyObjectByType<PedestrianSystem>(FindObjectsInactive.Include);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TrafficSystem).GetMethod("Start", flags).Invoke(traffic, null);
            typeof(PedestrianSystem).GetMethod("Start", flags).Invoke(people, null);
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            for (int i = 0; i < 1000; i++)
            {
                typeof(TrafficSystem).GetMethod("FixedUpdate", flags).Invoke(traffic, null);
                if (people.enabled) typeof(PedestrianSystem).GetMethod("FixedUpdate", flags).Invoke(people, null);
                Physics.Simulate(.02f);
            }
            Physics.simulationMode = previousMode;
            Physics.SyncTransforms();
            // Edit mode does not run animators, so pose each walker mid-stride.
            foreach (var animator in people.GetComponentsInChildren<Animator>())
            {
                var controller = animator.runtimeAnimatorController;
                if (controller != null && controller.animationClips.Length > 0)
                    controller.animationClips[0].SampleAnimation(animator.gameObject, UnityEngine.Random.Range(0f, .8f));
            }
            var cam = Camera.main;
            cam.GetComponent<FollowCamera>().enabled = false;
            Transform Nearest(Component system) => system.GetComponentsInChildren<Rigidbody>()
                .Where(b => b.gameObject.activeInHierarchy)
                .OrderBy(b => Vector3.Distance(b.position, traffic.player.position)).FirstOrDefault()?.transform;
            var car = Nearest(traffic);
            if (car != null) Capture(cam, car.position + new Vector3(9, 5, -14), car.position + Vector3.up, "outputs/coastal-city-traffic.png");
            var walker = people.enabled ? Nearest(people) : null;
            if (walker != null) Capture(cam, walker.position + walker.forward * 5 + new Vector3(2, 1.6f, 0), walker.position + Vector3.up, "outputs/coastal-city-people.png");
            Debug.Log("POCKET_DRIVE_TRAFFIC_PREVIEW_OK");
        }

        internal static void Capture(Camera cam, Vector3 position, Vector3 lookAt, string path)
        {
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            File.WriteAllBytes(path, image.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
        }

        // Headless check: pedestrians keep walking for 60 simulated seconds on pavements and crosswalks.
        public static void PedestrianChecks()
        {
            EditorSceneManager.OpenScene(CoastalCityBuilder.ScenePath);
            var traffic = Object.FindAnyObjectByType<TrafficSystem>(FindObjectsInactive.Include);
            var people = Object.FindAnyObjectByType<PedestrianSystem>(FindObjectsInactive.Include);
            Require(people != null, "City has no pedestrian system");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TrafficSystem).GetMethod("Start", flags).Invoke(traffic, null);
            typeof(PedestrianSystem).GetMethod("Start", flags).Invoke(people, null);
            Require(people.enabled && people.ActiveWalkers > 0, "Pedestrians did not start (no character templates?)");
            var trafficTick = typeof(TrafficSystem).GetMethod("FixedUpdate", flags);
            var walkTick = typeof(PedestrianSystem).GetMethod("FixedUpdate", flags);
            var walkers = people.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject.activeInHierarchy).ToArray();
            Require(walkers.All(w => w.GetComponentInChildren<Animator>()?.runtimeAnimatorController != null), "A walker has no walk animation");
            float[] travelled = new float[walkers.Length];
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            for (int step = 0; step < 3000; step++)
            {
                var before = walkers.Select(w => w.position).ToArray();
                trafficTick.Invoke(traffic, null);
                walkTick.Invoke(people, null);
                Physics.Simulate(.02f);
                for (int i = 0; i < walkers.Length; i++)
                {
                    Vector3 p = walkers[i].position;
                    float moved = Vector3.Distance(before[i], p);
                    if (moved < 1f) travelled[i] += moved;
                    Require(p.y > -.1f && p.y < .5f, $"{walkers[i].name} is at height {p.y}");
                }
            }
            Physics.simulationMode = previousMode;
            int walking = travelled.Count(d => d > 30f);
            Require(walking >= walkers.Length * 3 / 4, $"Only {walking} of {walkers.Length} pedestrians kept walking");
            Debug.Log($"POCKET_DRIVE_PEDESTRIAN_CHECKS_OK: {walkers.Length} walkers, {walking} walked over 30 m in 60 s");
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
