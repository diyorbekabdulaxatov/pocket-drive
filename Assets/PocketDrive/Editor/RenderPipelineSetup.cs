using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketDrive.Editor
{
    // Moves the project onto URP: pipeline asset, material conversion, and a post-processing volume per scene.
    public static class RenderPipelineSetup
    {
        const string Folder = "Assets/PocketDrive/Settings/Rendering";
        const string PipelinePath = Folder + "/PocketDriveURP.asset";
        const string ProfilePath = Folder + "/PocketDrivePostProcess.asset";
        const string LitShader = "Universal Render Pipeline/Lit";

        public static Shader Lit => Shader.Find(LitShader) ?? Shader.Find("Standard");

        public static Material NewLitMaterial(Color color, float smoothness)
        {
            var material = new Material(Lit) { color = color, enableInstancing = true };
            SetSmoothness(material, smoothness);
            return material;
        }

        public static void SetSmoothness(Material material, float smoothness)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        }

        [MenuItem("Pocket Drive/Switch to URP")]
        public static void Apply()
        {
            var pipeline = PipelineAsset();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null; // use the default pipeline on every level
            }
            QualitySettings.SetQualityLevel(current, false);

            int converted = ConvertMaterials();
            var profile = PostProcessProfile();
            foreach (string scene in new[] { ProjectSetup.ScenePath, "Assets/PocketDrive/Scenes/CoastalCity.unity" })
                if (System.IO.File.Exists(scene)) AddVolume(scene, profile);

            AssetDatabase.SaveAssets();
            Debug.Log($"POCKET_DRIVE_URP_OK: {converted} materials converted");
        }

        static UniversalRenderPipelineAsset PipelineAsset()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline != null) return pipeline;
            System.IO.Directory.CreateDirectory(Folder);
            // URP's own "Create > URP Asset" path; it is internal, and it sets up the default post-process data.
            var createRenderer = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset",
                BindingFlags.NonPublic | BindingFlags.Static);
            var renderer = (ScriptableRendererData)createRenderer.Invoke(null,
                new object[] { PipelinePath, RendererType.UniversalRenderer, true, "Renderer" });
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 120f;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            return pipeline;
        }

        // Standard → URP Lit. Material.color and mainTexture follow URP's [MainColor]/[MainTexture] attributes.
        static int ConvertMaterials()
        {
            int count = 0;
            var lit = Shader.Find(LitShader);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/PocketDrive" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name != "Standard") continue;
                Color color = material.GetColor("_Color");
                Texture texture = material.GetTexture("_MainTex");
                Vector2 scale = material.GetTextureScale("_MainTex");
                Vector2 offset = material.GetTextureOffset("_MainTex");
                float smoothness = material.GetFloat("_Glossiness");
                float metallic = material.GetFloat("_Metallic");
                material.shader = lit;
                material.SetColor("_BaseColor", color);
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                count++;
            }
            return count;
        }

        static VolumeProfile PostProcessProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(.35f);
            bloom.highQualityFiltering.Override(false);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.15f);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void AddVolume(string scenePath, VolumeProfile profile)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            if (Object.FindFirstObjectByType<Volume>() == null)
            {
                var volume = new GameObject("Post Processing").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None; // MSAA from the pipeline asset is cheaper on phones
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
