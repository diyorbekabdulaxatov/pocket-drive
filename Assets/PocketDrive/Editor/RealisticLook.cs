using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PocketDrive.Editor
{
    // Photo-real surfaces (CC0 textures from Poly Haven) and the Porsche model for generated scenes.
    public static class RealisticLook
    {
        const string TextureFolder = "Assets/PocketDrive/Textures/";
        public const string PorschePath = "Assets/PocketDrive/Vehicles/Porsche911/porsche_mobile.glb";

        sealed class Surface
        {
            public string texture;
            public Color tint = Color.white;
            public float smoothness = .15f;
            public float metresPerTile = 3f;
        }

        // Keyed by the generator's material names.
        static readonly Dictionary<string, Surface> Surfaces = new()
        {
            ["asphalt"] = new Surface { texture = "asphalt_02", tint = new Color(.78f, .78f, .8f), smoothness = .25f, metresPerTile = 4f },
            ["concrete"] = new Surface { texture = "concrete_pavement", metresPerTile = 2.5f },
            ["parking"] = new Surface { texture = "concrete_floor_worn_001", tint = new Color(.85f, .85f, .85f), metresPerTile = 4f },
            ["stucco"] = new Surface { texture = "plastered_wall_04", tint = new Color(1f, .92f, .8f), smoothness = .1f, metresPerTile = 3f },
            ["terracotta"] = new Surface { texture = "red_brick_03", smoothness = .1f, metresPerTile = 2f },
        };

        public static bool HasSurface(string material) => Surfaces.ContainsKey(material);

        // Metres covered by one texture repeat, or 0 when the material keeps the generator's own UVs.
        public static float TileSize(string material) =>
            material != null && Surfaces.TryGetValue(material, out var s) ? s.metresPerTile : 0f;

        // World-space planar UVs for one quad, so textures keep a real-world scale on any size of surface.
        public static Vector2[] QuadUVs(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float metresPerTile)
        {
            Vector3 n = Vector3.Cross(b - a, d - a);
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 Project(Vector3 p) =>
                (ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y))
                / metresPerTile;
            return new[] { Project(a), Project(b), Project(c), Project(d) };
        }

        public static void ApplySurface(Material material, string name)
        {
            if (!Surfaces.TryGetValue(name, out var surface)) return;
            var albedo = LoadTexture($"{surface.texture}_diff_1k.jpg", false);
            var normal = LoadTexture($"{surface.texture}_nor_gl_1k.jpg", true);
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.color = surface.tint;
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            RenderPipelineSetup.SetSmoothness(material, surface.smoothness);
            EditorUtility.SetDirty(material);
        }

        static Texture2D LoadTexture(string file, bool normalMap)
        {
            string path = TextureFolder + file;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
            var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.maxTextureSize != 1024 || importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.textureType = type;
                importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Replaces the car's visual children with the Porsche and fits the collider to it,
        // keeping the collider's bottom (and so the car's ride height) where it was.
        public static bool AttachPorsche(Transform car) =>
            AttachModel(car, PorschePath, "Porsche 911 Carrera 4S (Karol Miklas, CC BY-SA 4.0)", "bumper_front");

        public static bool AttachModel(Transform car, string modelPath, string label, string frontPartName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var box = car.GetComponent<BoxCollider>();
            if (prefab == null || box == null) return false;

            for (int i = car.childCount - 1; i >= 0; i--) Object.DestroyImmediate(car.GetChild(i).gameObject);
            // The model keeps its own root transform (Sketchfab exports turn Z-up to Y-up there); we orient a pivot.
            var model = new GameObject(label);
            model.transform.SetParent(car, false);
            PrefabUtility.InstantiatePrefab(prefab, model.transform);
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);

            // Stand the car upright and face it along +Z. Some exports arrive lying on their side or backwards,
            // so try the likely orientations: length along Z, height smallest, front bumper ahead and low.
            var bumper = model.GetComponentsInChildren<Renderer>()
                .FirstOrDefault(r => r.name.ToLowerInvariant().Contains(frontPartName.ToLowerInvariant()));
            Bounds bounds = LocalBounds(car, model);
            foreach (var rotation in new[] { Vector3.zero, new Vector3(0, 180, 0), new Vector3(-90, 0, 0), new Vector3(90, 0, 0),
                                             new Vector3(-90, 180, 0), new Vector3(90, 180, 0) })
            {
                model.transform.localRotation = Quaternion.Euler(rotation);
                bounds = LocalBounds(car, model);
                bool lengthAlongZ = bounds.size.z > bounds.size.x && bounds.size.z > bounds.size.y;
                bool flat = bounds.size.y < bounds.size.x * 1.2f;
                if (!lengthAlongZ || !flat) continue;
                if (bumper == null) break;
                Vector3 front = car.InverseTransformPoint(bumper.bounds.center);
                if (front.z > bounds.center.z && front.y < bounds.center.y + .1f) break;
            }

            float bottom = box.center.y - box.size.y / 2;
            model.transform.localPosition = new Vector3(-bounds.center.x, bottom - bounds.min.y, -bounds.center.z);
            box.size = new Vector3(bounds.size.x * .96f, bounds.size.y * .9f, bounds.size.z * .97f);
            box.center = new Vector3(0, bottom + box.size.y / 2, 0);
            return true;
        }

        static Bounds LocalBounds(Transform root, GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                Bounds b = r.bounds;
                bounds.Encapsulate(root.InverseTransformPoint(b.min));
                bounds.Encapsulate(root.InverseTransformPoint(b.max));
            }
            return bounds;
        }
    }
}
