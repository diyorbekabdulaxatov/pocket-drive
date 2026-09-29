using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketDrive.Editor
{
    // Downtown: the ready-made "City" model with the "Parking lot" model attached to its south edge,
    // the Lotus Exige as the player car, and the parking challenge on the lot's painted bays.
    public static class DowntownBuilder
    {
        public const string ScenePath = "Assets/PocketDrive/Scenes/Downtown.unity";
        const string OuterGroundPath = "Assets/PocketDrive/Settings/Rendering/OuterGround.mat";
        const string PostProcessPath = "Assets/PocketDrive/Settings/Rendering/PocketDrivePostProcess.asset";
        const string TuningPath = "Assets/PocketDrive/City/Generated/CoastalCarTuning.asset";
        const string SkyPath = "Assets/PocketDrive/City/Generated/CoastalSky.mat";

        const float GroundY = -.123f;                 // the city's ground plane, measured by raycast
        static readonly Rect CityArea = Rect.MinMaxRect(9f, -408.7f, 416f, -8.7f);
        static readonly Vector3 LotPosition = new(212f, GroundY, -428.7f);
        const float LotYaw = 90f;                      // turns the lot's west entrance to face the city (north)

        // Painted bays in the lot model's own frame, measured from a top-down render:
        // two rows of angled bays, 2.36 m apart along the row, lines slanted 16.8 degrees.
        static IEnumerable<(Vector3 local, float heading)> LotBays()
        {
            for (int k = -3; k <= 9; k++)
            {
                float x = -8.03f + k * 2.36f;
                yield return (new Vector3(x, 0, 5.7f), 16.8f);    // north row, nose towards the north fence
                yield return (new Vector3(x, 0, -6.1f), 163.2f);  // south row, nose towards the south fence
            }
        }

        [MenuItem("Pocket Drive/Downtown/Create or Rebuild Downtown")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImportedAssets.ConfigureTextures();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new GameObject("World").transform;
            var city = Instantiate(ImportedAssets.CityPath, world, Vector3.zero, 0);
            city.name = "City (Mateusz Wolinski, CC BY 4.0)";
            var lot = Instantiate(ImportedAssets.ParkingLotPath, world, LotPosition, LotYaw);
            lot.name = "Parking lot (Veterock, CC BY 4.0)";
            AddColliders(city);
            AddColliders(lot, skip: name => name.Contains("gate")); // barrier arms: drive under them
            OuterGround(world);
            Boundaries(world);
            Physics.SyncTransforms();
            var roads = RoadNetworkBuilder.Build();
            int cleared = ClearLanes(city, roads);
            Physics.SyncTransforms();

            var car = PlayerCar();
            var camera = Camera(car.transform);
            var hud = new GameObject("HUD").AddComponent<PrototypeHud>();
            hud.car = car;
            hud.title = "POCKET DRIVE  /  DOWNTOWN";

            var bays = LotBays().Select(b => new ParkingChallenge.Bay
            {
                centre = Ground(lot.transform.TransformPoint(b.local)) + Vector3.up * .02f,
                heading = b.heading + LotYaw
            }).ToArray();
            Vector3 start = Ground(lot.transform.TransformPoint(new Vector3(-24, 0, 0))) + Vector3.up * .45f;
            ChallengeSetup.AddParking(car, camera, bays, start, LotYaw + 90f, new Vector2(2.3f, 4.6f));

            var traffic = new GameObject("Traffic").AddComponent<GraphTrafficSystem>();
            traffic.player = car.transform;
            var trafficSettings = new SerializedObject(traffic);
            trafficSettings.FindProperty("graph").objectReferenceValue = roads;
            trafficSettings.FindProperty("carTemplate").objectReferenceValue = CityPopulation.CarTemplate(traffic.transform);
            trafficSettings.ApplyModifiedPropertiesWithoutUndo();

            Lighting();
            Debug.Log($"POCKET_DRIVE_DOWNTOWN_LANES: removed {cleared} parked model cars standing in driving lanes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"POCKET_DRIVE_DOWNTOWN_OK: {bays.Length} bays, spawn {car.transform.position}");
        }

        static GameObject Instantiate(string path, Transform parent, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception($"Missing model {path}");
            // Place a wrapper, not the model root: Sketchfab exports carry their Z-up to Y-up turn on the root.
            var go = new GameObject(Path.GetFileName(Path.GetDirectoryName(path)));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            PrefabUtility.InstantiatePrefab(asset, go.transform);
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return go;
        }

        // The city model has cars baked into its streets; remove the ones standing where traffic drives.
        static int ClearLanes(GameObject city, RoadGraph roads)
        {
            int removed = 0;
            foreach (var renderer in city.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.sharedMaterials.Any(m => m != null && m.name.ToLowerInvariant().Contains("vehicle"))) continue;
                Vector3 c = renderer.bounds.center;
                float nearest = roads.edges.Min(e => DistanceToSegment(c, roads.nodes[e.x], roads.nodes[e.y]));
                if (nearest > roads.laneOffset + 2.5f) continue;
                Object.DestroyImmediate(renderer.gameObject);
                removed++;
            }
            return removed;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new(p.x, p.z), A = new(a.x, a.z), B = new(b.x, b.z);
            Vector2 ab = B - A;
            float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
            return Vector2.Distance(P, A + ab * t);
        }

        static void AddColliders(GameObject root, Func<string, bool> skip = null)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                string materials = renderer == null ? "" : string.Join(" ", renderer.sharedMaterials.Where(m => m != null).Select(m => m.name.ToLowerInvariant()));
                if (skip != null && (skip(filter.name.ToLowerInvariant()) || skip(materials))) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        // A concrete plain around the city so the horizon is not empty, plus the apron joining the lot to the street.
        static void OuterGround(Transform parent)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OuterGroundPath);
            if (material == null)
            {
                // Plain grey close to the city's own ground; the photo textures read almost black at this scale.
                material = RenderPipelineSetup.NewLitMaterial(new Color(.5f, .5f, .49f), .08f);
                AssetDatabase.CreateAsset(material, OuterGroundPath);
            }
            var plain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plain.name = "Outer ground";
            plain.transform.SetParent(parent);
            plain.transform.position = new Vector3(212, GroundY - .05f, -208);
            plain.transform.localScale = new Vector3(240, 1, 240);
            plain.GetComponent<Renderer>().sharedMaterial = material;
            plain.isStatic = true;

            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "Car park apron";
            apron.transform.SetParent(parent);
            apron.transform.position = new Vector3(LotPosition.x, GroundY - .02f, -409.7f);
            apron.transform.localScale = new Vector3(14, .04f, 4);
            apron.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/PocketDrive/City/Generated/asphalt.mat");
            apron.isStatic = true;
        }

        // Invisible walls round the city, with an opening to the car park, which has its own walls.
        static void Boundaries(Transform parent)
        {
            var walls = new GameObject("Boundaries").transform;
            walls.SetParent(parent);
            void Wall(string name, Vector3 centre, Vector3 size)
            {
                var w = new GameObject(name);
                w.transform.SetParent(walls);
                w.transform.position = centre;
                w.AddComponent<BoxCollider>().size = size;
            }
            Rect c = CityArea;
            const float h = 6f, t = 2f;
            float lotHalf = 9.5f, gapLeft = LotPosition.x - lotHalf, gapRight = LotPosition.x + lotHalf;
            Wall("North", new Vector3(c.center.x, h / 2, c.yMax + t / 2), new Vector3(c.width + 2 * t, h, t));
            Wall("West", new Vector3(c.xMin - t / 2, h / 2, c.center.y), new Vector3(t, h, c.height));
            Wall("East", new Vector3(c.xMax + t / 2, h / 2, c.center.y), new Vector3(t, h, c.height));
            Wall("South (west part)", new Vector3((c.xMin + gapLeft) / 2, h / 2, c.yMin - t / 2), new Vector3(gapLeft - c.xMin, h, t));
            Wall("South (east part)", new Vector3((gapRight + c.xMax) / 2, h / 2, c.yMin - t / 2), new Vector3(c.xMax - gapRight, h, t));
            float lotSouth = LotPosition.z - 18.5f;
            Wall("Car park west", new Vector3(gapLeft - t / 2, h / 2, (c.yMin + lotSouth) / 2), new Vector3(t, h, c.yMin - lotSouth));
            Wall("Car park east", new Vector3(gapRight + t / 2, h / 2, (c.yMin + lotSouth) / 2), new Vector3(t, h, c.yMin - lotSouth));
            Wall("Car park south", new Vector3(LotPosition.x, h / 2, lotSouth - t / 2), new Vector3(2 * lotHalf + 2 * t, h, t));
        }

        static Vector3 Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 20, Vector3.down, out var hit, 60, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(p.x, GroundY, p.z);

        static ArcadeCar PlayerCar()
        {
            var go = new GameObject("Player Car");
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1.7f, .6f, 3.4f);
            var body = go.AddComponent<Rigidbody>();
            body.mass = 1100;
            var car = go.AddComponent<ArcadeCar>();
            var settings = new SerializedObject(car);
            settings.FindProperty("tuning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CarTuning>(TuningPath);
            settings.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<CarInput>();
            if (!RealisticLook.AttachModel(go.transform, ImportedAssets.LotusPath, "Lotus Exige 240 (David & 3D, Sketchfab Standard)", "frontbumper"))
                throw new Exception("Could not attach the Lotus model");
            go.transform.SetPositionAndRotation(FreeSpawn(box), Quaternion.Euler(0, 0, 0));
            return car;
        }

        // First clear spot on the street just north of the car park entrance.
        static Vector3 FreeSpawn(BoxCollider box)
        {
            float lift = -(box.center.y - box.size.y / 2) + .05f;
            for (float dz = 0; dz < 60; dz += 3)
                foreach (float dx in new[] { 0f, 6f, -6f, 12f, -12f })
                {
                    Vector3 ground = Ground(new Vector3(LotPosition.x + dx, 0, -400f + dz));
                    if (Mathf.Abs(ground.y - GroundY) > .3f) continue; // street level, not a rooftop or kerb
                    // Box a little above the ground and padded sideways, so only real obstacles count.
                    Vector3 centre = ground + Vector3.up * (lift + box.center.y + .15f);
                    if (!Physics.CheckBox(centre, box.size * .5f + new Vector3(.4f, 0, .6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        return ground + Vector3.up * lift;
                }
            {
                Vector3 g = Ground(new Vector3(LotPosition.x, 0, -400f));
                Vector3 c = g + Vector3.up * (lift + box.center.y + .15f);
                var hits = Physics.OverlapBox(c, box.size * .5f + new Vector3(.4f, 0, .6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                throw new Exception($"No clear spawn point near the car park; ground {g}, box centre {c}, size {box.size}, blocked by {string.Join(", ", hits.Select(h => h.name))}");
            }
        }

        static FollowCamera Camera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 1500;
            go.AddComponent<AudioListener>();
            var follow = go.AddComponent<FollowCamera>();
            follow.target = target;
            var so = new SerializedObject(follow);
            so.FindProperty("distance").floatValue = 6.5f;
            so.FindProperty("height").floatValue = 2.4f;
            so.FindProperty("lookAhead").floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            follow.Snap();
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderPostProcessing = true;
            return follow;
        }

        static void Lighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .93f, .82f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, -35, 0);
            RenderSettings.sun = sun;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .47f, .55f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .36f, .35f);
            RenderSettings.ambientGroundColor = new Color(.18f, .17f, .16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.66f, .72f, .78f);
            RenderSettings.fogDensity = .0022f;
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessPath);
        }

        // Headless check: the Lotus spawns on the ground, drives, and the parking challenge works on the lot's bays.
        public static void Checks()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var car = Object.FindAnyObjectByType<ArcadeCar>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ArcadeCar).GetMethod("Awake", flags).Invoke(car, null);
            var tick = typeof(ArcadeCar).GetMethod("FixedUpdate", flags);
            var body = car.GetComponent<Rigidbody>();
            Vector3 spawn = body.position;
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();
                for (int i = 0; i < 60; i++) { tick.Invoke(car, null); Physics.Simulate(.02f); }
                Require(car.Grounded, $"Lotus is not on the ground at {body.position}");
                car.SetInput(1, 0);
                for (int i = 0; i < 100; i++) { tick.Invoke(car, null); Physics.Simulate(.02f); }
                Require(Vector3.Distance(Flat(body.position), Flat(spawn)) > 10f, $"Lotus did not drive (at {body.position})");
                car.SetInput(0, 0);
                car.ResetCar();
            }
            finally { Physics.simulationMode = previousMode; }
            TrafficChecks();
            ChallengeSetup.RunChecks(ScenePath);
            Debug.Log("POCKET_DRIVE_DOWNTOWN_CHECKS_OK: spawn grounded, drives, parking challenge passes");
        }

        // Traffic drives the street network for 60 simulated seconds without leaving the roads or overlapping.
        static void TrafficChecks()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var traffic = Object.FindAnyObjectByType<GraphTrafficSystem>(FindObjectsInactive.Include);
            Require(traffic != null, "Downtown has no traffic");
            // Park the player in the car park, out of the traffic's way, so a stopped player doesn't hold up the test.
            traffic.player.position = LotPosition + Vector3.up * .3f;
            Physics.SyncTransforms();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GraphTrafficSystem).GetMethod("Start", flags).Invoke(traffic, null);
            Require(traffic.enabled && traffic.ActiveCars > 0, "Traffic did not start");
            var tick = typeof(GraphTrafficSystem).GetMethod("FixedUpdate", flags);
            var roads = traffic.Graph;
            var cars = traffic.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject.activeInHierarchy).ToArray();
            var travelled = new float[cars.Length];
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                for (int step = 0; step < 3000; step++)
                {
                    var before = cars.Select(c => c.position).ToArray();
                    tick.Invoke(traffic, null);
                    Physics.Simulate(.02f);
                    for (int i = 0; i < cars.Length; i++)
                    {
                        Vector3 p = cars[i].position;
                        float moved = Vector3.Distance(before[i], p);
                        if (moved < 5f) travelled[i] += moved;
                        float off = roads.edges.Min(e => DistanceToSegment(p, roads.nodes[e.x], roads.nodes[e.y]));
                        Require(off < roads.laneOffset + 6f, $"{cars[i].name} left the roads at {p} ({off:0.0} m from a road)");
                        for (int j = i + 1; j < cars.Length; j++)
                            Require(Vector3.Distance(p, cars[j].position) > 3f, $"{cars[i].name} overlaps {cars[j].name} at {p}");
                    }
                }
            }
            finally { Physics.simulationMode = previousMode; }
            for (int i = 0; i < cars.Length; i++)
                if (travelled[i] <= 120f) Debug.Log($"POCKET_DRIVE_STUCK {cars[i].name} at {cars[i].position} travelled {travelled[i]:0}");
            int moving = travelled.Count(d => d > 120f);
            Require(moving >= cars.Length * 3 / 4, $"Only {moving} of {cars.Length} cars kept moving");
            Debug.Log($"POCKET_DRIVE_DOWNTOWN_TRAFFIC_OK: {cars.Length} cars, {moving} drove over 120 m in 60 s");
        }

        // Street and car park renders for review.
        public static void Preview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var car = Object.FindAnyObjectByType<ArcadeCar>();
            var challenge = Object.FindAnyObjectByType<ParkingChallenge>();
            var cam = UnityEngine.Camera.main;
            cam.GetComponent<FollowCamera>().enabled = false;
            Vector3 p = car.transform.position;
            CityPopulation.Capture(cam, p + new Vector3(0, 2.3f, -6.5f), p + new Vector3(0, 1, 6), "outputs/downtown-driving.png");
            CityPopulation.Capture(cam, p + new Vector3(4, 1.4f, 5), p + Vector3.up * .5f, "outputs/downtown-car.png");
            CityPopulation.Capture(cam, p + new Vector3(0, 3, -10), p + new Vector3(0, 6, 120), "outputs/downtown-street.png");
            typeof(ParkingChallenge).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(challenge, null);
            typeof(ArcadeCar).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(car, null);
            challenge.Begin(4);
            Vector3 back = -car.transform.forward;
            CityPopulation.Capture(cam, car.transform.position + back * 7 + Vector3.up * 4, car.transform.position - back * 14,
                "outputs/downtown-parking.png");
            var traffic = Object.FindAnyObjectByType<GraphTrafficSystem>(FindObjectsInactive.Include);
            if (traffic != null)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                car.PlaceAt(p, Quaternion.identity);
                typeof(GraphTrafficSystem).GetMethod("Start", flags).Invoke(traffic, null);
                var previousMode = Physics.simulationMode;
                Physics.simulationMode = SimulationMode.Script;
                for (int i = 0; i < 600; i++) { typeof(GraphTrafficSystem).GetMethod("FixedUpdate", flags).Invoke(traffic, null); Physics.Simulate(.02f); }
                Physics.simulationMode = previousMode;
                var nearest = traffic.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject.activeInHierarchy)
                    .OrderBy(b => Vector3.Distance(b.position, p)).First().transform;
                CityPopulation.Capture(cam, nearest.position - nearest.forward * 9 + Vector3.up * 3.5f + nearest.right * 3,
                    nearest.position + nearest.forward * 10, "outputs/downtown-traffic.png");
            }
            Debug.Log("POCKET_DRIVE_DOWNTOWN_PREVIEW_OK");
        }

        [MenuItem("Pocket Drive/Downtown/Build Downtown Android APK")]
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/PocketDrive-downtown.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Downtown Android build failed");
            Debug.Log("POCKET_DRIVE_DOWNTOWN_ANDROID_OK");
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
