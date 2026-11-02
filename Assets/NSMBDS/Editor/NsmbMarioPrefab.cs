using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NsmbDs.Editor {
    /// <summary>Builds a model-only copy of the base project's Mario, with no Quantum components.</summary>
    public static class NsmbMarioPrefab {
        private const string SourcePath = "Assets/QuantumUser/Resources/EntityPrototypes/Player/PlayerMario.prefab";
        private const string TargetFolder = "Assets/NSMBDS/Resources";
        private const string TargetPath = TargetFolder + "/NsmbMario.prefab";
        private const string SmallModelPath = "Assets/Models/Players/mario_small/mario_small_exported.fbx";
        private const string SmallControllerPath = "Assets/Animations/Player/Mario/SmallMario.overrideController";
        private const string ModelParent = "ModelParent";
        private const string Models = "Models";
        private const string SmallModel = "SmallMario";
        private const string LargeModel = "LargeMario";
        private const string LargeModelPath = "Assets/Models/Players/mario_big/mario_big_exported.fbx";
        private const string LargeControllerPath = "Assets/Animations/Player/Mario/LargeMario.controller";
        private const string GoombaSheet = "Assets/Sprites/Atlases/Entity/goomba.png";
        private const string ItemAssetsPath = TargetFolder + "/NsmbItems.asset";
        private const string PowerupFolder = "Assets/QuantumUser/Resources/AssetObjects/Items/";
        private const string ShaderPath = "Assets/NSMBDS/Shaders/NsmbMario.shader";
        private const string MaterialFolder = "Assets/NSMBDS/Materials";
        private const string MainTexture = "_MainTex";
        private const string IsEyeTexture = "_IsEyeTexture";
        private static readonly string[] HiddenParts = { "head_nocap", "cap_in_hand", "Hammer", "Propeller", "blue_shell", "GoldBlock" };

        [MenuItem("NSMB DS/Build Mario Prefab")]
        public static void Build() {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            GameObject root = new("NsmbMario");
            GameObject parent = Object.Instantiate(source.transform.Find(ModelParent).gameObject, root.transform);
            parent.name = ModelParent;

            KeepOnly(parent.transform, Models);
            Transform models = parent.transform.Find(Models);
            for (int i = models.childCount - 1; i >= 0; i--) {
                Transform child = models.GetChild(i);
                if (child.name != SmallModel && child.name != LargeModel) {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
            models.Find(SmallModel).gameObject.SetActive(true);
            models.Find(LargeModel).gameObject.SetActive(false);
            StripBehaviours(parent);
            HideParts(root);
            CreateFolder(MaterialFolder);
            ReplaceMaterials(root);

            Animator animator = root.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(SmallModelPath).OfAvatar();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(SmallControllerPath);
            animator.applyRootMotion = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
                Debug.Log($"[NSMBDS] Mario renderer {renderer.name} active {renderer.gameObject.activeInHierarchy}, materials {renderer.sharedMaterials.Length}");
            }

            CreateFolder(TargetFolder);
            PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
            Object.DestroyImmediate(root);
            BuildItemAssets();
            Debug.Log($"[NSMBDS] Wrote {TargetPath}");
        }

        private static void BuildItemAssets() {
            NsmbItemAssets items = AssetDatabase.LoadAssetAtPath<NsmbItemAssets>(ItemAssetsPath);
            if (items == null) {
                items = ScriptableObject.CreateInstance<NsmbItemAssets>();
                AssetDatabase.CreateAsset(items, ItemAssetsPath);
            }

            items.mushroom = ReserveSprite("Mushroom");
            items.oneUp = items.mushroom;
            items.fireFlower = ReserveSprite("FireFlower");
            items.star = ReserveSprite("Starman");
            items.mega = ReserveSprite("MegaMushroom");
            items.mini = ReserveSprite("MiniMushroom");
            items.shell = ReserveSprite("BlueShell");
            items.goomba = SpritesOf(GoombaSheet);
            items.smallAvatar = AssetDatabase.LoadAllAssetsAtPath(SmallModelPath).OfAvatar();
            items.largeAvatar = AssetDatabase.LoadAllAssetsAtPath(LargeModelPath).OfAvatar();
            items.smallController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(SmallControllerPath);
            items.largeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LargeControllerPath);
            EditorUtility.SetDirty(items);
            AssetDatabase.SaveAssets();
        }

        private static Sprite[] SpritesOf(string sheet) {
            List<Sprite> sprites = new();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheet)) {
                if (asset is Sprite sprite) {
                    sprites.Add(sprite);
                }
            }
            sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return sprites.ToArray();
        }

        private static Sprite ReserveSprite(string item) {
            foreach (string guid in AssetDatabase.FindAssets(item + " t:ScriptableObject")) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == item) {
                    SerializedProperty sprite = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path)).FindProperty("ReserveSprite");
                    if (sprite != null) {
                        return sprite.objectReferenceValue as Sprite;
                    }
                }
            }
            Debug.LogWarning($"[NSMBDS] No sprite for {item}");
            return null;
        }

        public static void BuildBatch() {
            Build();
            EditorApplication.Exit(0);
        }

        private static void CreateFolder(string folder) {
            if (!AssetDatabase.IsValidFolder(folder)) {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            }
        }

        private static void HideParts(GameObject root) {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) {
                // The base project's player layer gets a coloured outline from the renderer.
                node.gameObject.layer = 0;
                foreach (string part in HiddenParts) {
                    if (node.name.Contains(part)) {
                        node.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static void ReplaceMaterials(GameObject root) {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Dictionary<Material, Material> replaced = new();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) {
                    if (!replaced.TryGetValue(materials[i], out Material copy)) {
                        copy = new Material(shader);
                        copy.SetTexture(MainTexture, materials[i].GetTexture(MainTexture));
                        copy.SetFloat(IsEyeTexture, materials[i].HasProperty(IsEyeTexture) ? materials[i].GetFloat(IsEyeTexture) : 0f);
                        AssetDatabase.CreateAsset(copy, $"{MaterialFolder}/{materials[i].name}.mat");
                        replaced[materials[i]] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Avatar OfAvatar(this Object[] assets) {
            foreach (Object asset in assets) {
                if (asset is Avatar avatar) {
                    return avatar;
                }
            }
            return null;
        }

        private static void KeepOnly(Transform parent, string name) {
            for (int i = parent.childCount - 1; i >= 0; i--) {
                Transform child = parent.GetChild(i);
                if (child.name != name) {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void StripBehaviours(GameObject root) {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
                foreach (MonoBehaviour behaviour in node.GetComponents<MonoBehaviour>()) {
                    Object.DestroyImmediate(behaviour);
                }
            }
            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true)) {
                Object.DestroyImmediate(particles.gameObject);
            }
        }
    }
}
