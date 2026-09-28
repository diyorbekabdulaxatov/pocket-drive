using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PocketDrive.Editor
{
    // Import settings for ready-made models in Assets/PocketDrive/Imported, and overview renders of them.
    public static class ImportedAssets
    {
        public const string Root = "Assets/PocketDrive/Imported";
        public const string CityPath = Root + "/City/scene.gltf";
        public const string ParkingLotPath = Root + "/ParkingLot/scene.gltf";
        public const string LotusPath = Root + "/LotusExige/scene.gltf";

        // Phone-sized textures: 2048 for the city atlases, 1024 for everything else; normal maps flagged as such.
        [MenuItem("Pocket Drive/Imported/Configure Textures")]
        public static void ConfigureTextures()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                int max = path.Contains("/City/") ? 2048 : 1024;
                bool normal = Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains("normal");
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.maxTextureSize == max && importer.textureType == type) continue;
                importer.maxTextureSize = max;
                importer.textureType = type;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                changed++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"POCKET_DRIVE_IMPORTED_TEXTURES_OK: {changed} textures configured");
        }

        // Top-down and street-level renders of the city and the car park, to plan roads, spawns and bays.
        public static void Preview()
        {
            ConfigureTextures();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var city = Place(CityPath, Vector3.zero);
            var lot = Place(ParkingLotPath, new Vector3(0, 0, 1000));
            var cam = Camera.main;
            cam.farClipPlane = 3000;
            Bounds cityBounds = BoundsOf(city);
            Bounds lotBounds = BoundsOf(lot);
            Debug.Log($"POCKET_DRIVE_CITY_BOUNDS: centre {cityBounds.center} size {cityBounds.size}");
            Debug.Log($"POCKET_DRIVE_LOT_BOUNDS: centre {lotBounds.center} size {lotBounds.size}");

            cam.orthographic = true;
            cam.orthographicSize = cityBounds.extents.z * 1.02f;
            TopDown(cam, cityBounds, "outputs/imported-city-top.png", 2048);
            cam.orthographicSize = lotBounds.extents.x * .6f;
            TopDown(cam, lotBounds, "outputs/imported-parking-top.png", 1600);
            cam.orthographic = false;
            cam.fieldOfView = 60;
            CityPopulation.Capture(cam, cityBounds.center + new Vector3(0, 60, -cityBounds.extents.z * 1.1f),
                cityBounds.center, "outputs/imported-city-view.png");
            Debug.Log("POCKET_DRIVE_IMPORTED_PREVIEW_OK");
        }

        // Street-level renders: the car park from both ends and a few city streets, with ground heights found by raycast.
        public static void StreetPreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var city = Place(CityPath, Vector3.zero);
            var lot = Place(ParkingLotPath, new Vector3(0, 0, 1000));
            foreach (var f in city.GetComponentsInChildren<MeshFilter>()) f.gameObject.AddComponent<MeshCollider>();
            foreach (var f in lot.GetComponentsInChildren<MeshFilter>()) f.gameObject.AddComponent<MeshCollider>();
            Physics.SyncTransforms();
            var cam = Camera.main;
            cam.farClipPlane = 3000;
            cam.fieldOfView = 65;
            CityPopulation.Capture(cam, new Vector3(-24, 3, 1000), new Vector3(0, 0, 1000), "outputs/imported-parking-west.png");
            CityPopulation.Capture(cam, new Vector3(24, 3, 1000), new Vector3(0, 0, 1000), "outputs/imported-parking-east.png");
            var spots = new[] { new Vector3(212, 0, -72), new Vector3(120, 0, -208), new Vector3(300, 0, -300), new Vector3(30, 0, -250) };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = spots[i];
                float y = Physics.Raycast(p + Vector3.up * 300, Vector3.down, out var hit, 600) ? hit.point.y : float.NaN;
                Debug.Log($"POCKET_DRIVE_GROUND {p.x},{p.z}: y={y} hit={hit.collider?.name}");
                CityPopulation.Capture(cam, new Vector3(p.x, y + 2.5f, p.z), new Vector3(p.x + 40, y + 2, p.z + 5), $"outputs/imported-street-{i}.png");
            }
            // Ground height across the city on a 20 m grid, to find where the drivable surface is.
            for (float z = -400; z <= -10; z += 40)
            {
                var line = new System.Text.StringBuilder();
                for (float x = 15; x <= 410; x += 40)
                    line.Append(Physics.Raycast(new Vector3(x, 300, z), Vector3.down, out var h, 600) ? $"{h.point.y,6:0.0}" : "   ---");
                Debug.Log($"POCKET_DRIVE_HEIGHTS z={z}: {line}");
            }
            Debug.Log("POCKET_DRIVE_STREET_PREVIEW_OK");
        }

        static GameObject Place(string path, Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.position = position;
            return go;
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static void TopDown(Camera cam, Bounds b, string path, int size)
        {
            cam.transform.position = b.center + Vector3.up * (b.size.y + 200);
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.aspect = 1;
            var rt = new RenderTexture(size, size, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            File.WriteAllBytes(path, image.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            cam.ResetAspect();
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
        }
    }
}
