using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

namespace GameFrameworks.Editor
{
    public static class PlayableSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PeasantPrefabPath = "Assets/Stylized NPC - Peasant Nolant/Prefabs/Peasant Nolant Blue(Free Version).prefab";
        private const string PeasantControllerPath = "Assets/Animations/PeasantNolant_Controller.controller";
        private const string CargoControllerPath = "Assets/Animations/CargoPlatform_Controller.controller";
        private const string GateControllerPath = "Assets/Animations/SecurityGate_Controller.controller";
        private const string StreetLampControllerPath = "Assets/Animations/StreetLamp_Controller.controller";
        private const string PlayerMaterialPath = "Assets/Materials/PlayerMaterial.mat";
        private const string PlayerAccentPath = "Assets/Materials/PlayerAccent.mat";
        private const string WoodBrightMatPath = "Assets/Lowpoly_Crates/Materials/Wood_bright.mat";
        private const string WoodDarkMatPath = "Assets/Lowpoly_Crates/Materials/Wood_dark.mat";

        private static readonly string[] CropPrefabPaths = new[]
        {
            "Assets/FastMesh/Prefabs/Vegetable/Vege-Carrot.prefab",
            "Assets/FastMesh/Prefabs/Vegetable/Vege-Corn.prefab",
            "Assets/FastMesh/Prefabs/Vegetable/Vege-Potato.prefab",
            "Assets/FastMesh/Prefabs/Vegetable/Vege-Tomato.prefab",
            "Assets/FastMesh/Prefabs/Vegetable/Fruit-Apple A.prefab",
            "Assets/FastMesh/Prefabs/Vegetable/Fruit-Apple B.prefab"
        };

        [MenuItem("Assignment 2/Setup Playable Scene", false, 1)]
        public static void BuildPlayableScene()
        {
            AnimatorStateMachineBuilder.ConfigureKevinIglesiasAssets();
            AnimatorStateMachineBuilder.ConfigureMartialArtAnimationAssets();
            AnimatorStateMachineBuilder.BuildAnimatorController();

            ConfigureCropPrefabs();

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            SetupDirectionalLight();
            GameObject player = SetupPlayerCharacter();
            SetupCameras(player);
            SetupCargoPlatform();
            SetupSecurityGate();
            SetupAnimatedStreetLamp();
            SetupItemBox();
            SetupEnvironmentCrops();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayableSceneBuilder] Scene, Animator, Crop Prefabs, ItemBox, and Environment Crops configured and saved!");
        }

        public static void ConfigureCropPrefabs()
        {
            foreach (string prefabPath in CropPrefabPaths)
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null) continue;

                ConfigureCropObjectPhysicsAndScale(root);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureCropObjectPhysicsAndScale(GameObject cropObj)
        {
            if (cropObj == null) return;

            MeshFilter[] meshFilters = cropObj.GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false;
            Bounds combinedMeshBounds = default;
            if (meshFilters != null)
            {
                foreach (var mf in meshFilters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (!hasBounds)
                    {
                        combinedMeshBounds = mf.sharedMesh.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedMeshBounds.Encapsulate(mf.sharedMesh.bounds);
                    }
                }
            }

            if (hasBounds && meshFilters != null)
            {
                foreach (var mf in meshFilters)
                {
                    if (mf != null && mf.transform != cropObj.transform)
                    {
                        mf.transform.localPosition = -combinedMeshBounds.center;
                    }
                }
            }

            float maxMeshDim = hasBounds
                ? Mathf.Max(combinedMeshBounds.size.x, combinedMeshBounds.size.y, combinedMeshBounds.size.z)
                : 0.4f;
            if (maxMeshDim < 0.0001f) maxMeshDim = 0.4f;

            float targetWorldSize = 0.36f;
            float uniformScale = targetWorldSize / maxMeshDim;
            cropObj.transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);

            Rigidbody rb = cropObj.GetComponent<Rigidbody>();
            if (rb == null) rb = cropObj.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.25f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            SphereCollider sphereCol = cropObj.GetComponent<SphereCollider>();
            if (sphereCol == null) sphereCol = cropObj.AddComponent<SphereCollider>();
            sphereCol.isTrigger = false;
            sphereCol.center = Vector3.zero;
            sphereCol.radius = maxMeshDim * 0.45f;

            CropItem cropItem = cropObj.GetComponent<CropItem>();
            if (cropItem == null) cropItem = cropObj.AddComponent<CropItem>();
            cropItem.CropType = cropObj.name.Replace("(Clone)", "").Trim();
        }

        private static void SetupItemBox()
        {
            GameObject existingBox = GameObject.Find("ItemBox");
            if (existingBox != null)
            {
                Object.DestroyImmediate(existingBox);
            }

            GameObject itemBox = new GameObject("ItemBox");
            itemBox.transform.position = new Vector3(1.5f, 0.0f, -3.2f);
            itemBox.transform.rotation = Quaternion.Euler(0f, -20f, 0f);

            Material woodBright = AssetDatabase.LoadAssetAtPath<Material>(WoodBrightMatPath);
            Material woodDark = AssetDatabase.LoadAssetAtPath<Material>(WoodDarkMatPath);
            if (woodBright == null) woodBright = AssetDatabase.LoadAssetAtPath<Material>(PlayerAccentPath);
            if (woodDark == null) woodDark = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);

            CreateBoxPart(itemBox.transform, "Box_Bottom", new Vector3(0f, 0.06f, 0f), new Vector3(1.7f, 0.12f, 1.7f), woodDark);
            CreateBoxPart(itemBox.transform, "Wall_North", new Vector3(0f, 0.45f, 0.79f), new Vector3(1.7f, 0.78f, 0.12f), woodBright);
            CreateBoxPart(itemBox.transform, "Wall_South", new Vector3(0f, 0.45f, -0.79f), new Vector3(1.7f, 0.78f, 0.12f), woodBright);
            CreateBoxPart(itemBox.transform, "Wall_East", new Vector3(0.79f, 0.45f, 0f), new Vector3(0.12f, 0.78f, 1.46f), woodBright);
            CreateBoxPart(itemBox.transform, "Wall_West", new Vector3(-0.79f, 0.45f, 0f), new Vector3(0.12f, 0.78f, 1.46f), woodBright);

            CreateBoxPart(itemBox.transform, "Post_NE", new Vector3(0.79f, 0.48f, 0.79f), new Vector3(0.16f, 0.88f, 0.16f), woodDark);
            CreateBoxPart(itemBox.transform, "Post_NW", new Vector3(-0.79f, 0.48f, 0.79f), new Vector3(0.16f, 0.88f, 0.16f), woodDark);
            CreateBoxPart(itemBox.transform, "Post_SE", new Vector3(0.79f, 0.48f, -0.79f), new Vector3(0.16f, 0.88f, 0.16f), woodDark);
            CreateBoxPart(itemBox.transform, "Post_SW", new Vector3(-0.79f, 0.48f, -0.79f), new Vector3(0.16f, 0.88f, 0.16f), woodDark);

            BoxCollider triggerCol = itemBox.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.center = new Vector3(0f, 0.55f, 0f);
            triggerCol.size = new Vector3(1.55f, 0.95f, 1.55f);

            itemBox.AddComponent<CropItemBox>();
        }

        private static void CreateBoxPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (mat != null)
            {
                MeshRenderer mr = part.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
        }

        private static void SetupEnvironmentCrops()
        {
            GameObject existingContainer = GameObject.Find("EnvironmentCrops");
            if (existingContainer != null)
            {
                Object.DestroyImmediate(existingContainer);
            }

            CropItem[] oldCrops = Object.FindObjectsByType<CropItem>(FindObjectsSortMode.None);
            foreach (var old in oldCrops)
            {
                if (old != null && old.gameObject != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            GameObject container = new GameObject("EnvironmentCrops");
            container.transform.position = Vector3.zero;

            Vector3[] spawnPositions = new[]
            {
                new Vector3(-1.4f, 0.22f, -4.4f),
                new Vector3(-0.4f, 0.22f, -4.8f),
                new Vector3(-2.3f, 0.22f, -4.7f),
                new Vector3(0.2f, 0.22f, -5.6f),
                new Vector3(-2.8f, 0.22f, -6.2f),
                new Vector3(-0.8f, 0.22f, -3.3f),

                new Vector3(-4.2f, 0.22f, -2.5f),
                new Vector3(-5.5f, 0.22f, 0.5f),
                new Vector3(-3.0f, 0.22f, 2.2f),
                new Vector3(-1.0f, 0.22f, 1.5f),
                new Vector3(1.8f, 0.22f, 0.8f),
                new Vector3(3.6f, 0.22f, -1.2f),
                new Vector3(4.2f, 0.22f, 2.5f),
                new Vector3(0.5f, 0.22f, 4.2f),
                new Vector3(-3.8f, 0.22f, 5.0f),
                new Vector3(2.4f, 0.22f, 5.5f)
            };

            for (int i = 0; i < spawnPositions.Length; i++)
            {
                string prefabPath = CropPrefabPaths[i % CropPrefabPaths.Length];
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null) continue;

                GameObject cropInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                cropInstance.name = $"{prefabAsset.name}_{i + 1:00}";
                cropInstance.transform.SetParent(container.transform, true);
                cropInstance.transform.position = spawnPositions[i];
                cropInstance.transform.rotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);

                ConfigureCropObjectPhysicsAndScale(cropInstance);
            }
        }

        private static void SetupDirectionalLight()
        {
            Light sun = Object.FindAnyObjectByType<Light>();
            GameObject sunGO = null;

            if (sun != null && sun.type == LightType.Directional)
            {
                sunGO = sun.gameObject;
            }
            else
            {
                sunGO = new GameObject("Directional Light");
                sun = sunGO.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sunGO.transform.position = new Vector3(0f, 15f, 0f);
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            sun.color = new Color(1.0f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }

        private static GameObject SetupPlayerCharacter()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
            }

            player.tag = "Player";
            player.transform.position = new Vector3(-1.5f, 0.05f, -6f);
            player.transform.rotation = Quaternion.identity;

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null) rb = player.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            CapsuleCollider col = player.GetComponent<CapsuleCollider>();
            if (col == null) col = player.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.35f;
            col.height = 1.8f;

            Transform camTarget = player.transform.Find("CameraTarget");
            if (camTarget == null)
            {
                GameObject ctGO = new GameObject("CameraTarget");
                ctGO.transform.SetParent(player.transform, false);
                ctGO.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                camTarget = ctGO.transform;
            }

            Transform holdPoint = player.transform.Find("HoldPoint");
            if (holdPoint == null)
            {
                GameObject holdGO = new GameObject("HoldPoint");
                holdGO.transform.SetParent(player.transform, false);
                holdPoint = holdGO.transform;
            }
            holdPoint.localPosition = new Vector3(0f, 1.0f, 0.5f);
            holdPoint.localRotation = Quaternion.identity;

            // Remove all existing visual/character children to prevent duplicate character meshes
            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = player.transform.GetChild(i);
                if (child == null) continue;
                if (child == camTarget || child == holdPoint) continue;
                Object.DestroyImmediate(child.gameObject);
            }

            GameObject visualGO = null;
            GameObject peasantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PeasantPrefabPath);
            if (peasantAsset != null)
            {
                visualGO = (GameObject)PrefabUtility.InstantiatePrefab(peasantAsset);
                visualGO.name = "Visual";
                visualGO.transform.SetParent(player.transform, false);
                visualGO.transform.localPosition = Vector3.zero;
                visualGO.transform.localRotation = Quaternion.identity;
                visualGO.transform.localScale = Vector3.one;
            }
            else
            {
                visualGO = new GameObject("Visual");
                visualGO.transform.SetParent(player.transform, false);
            }

            Animator animator = visualGO.GetComponent<Animator>();
            if (animator == null) animator = visualGO.AddComponent<Animator>();

            RuntimeAnimatorController runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player_AnimatorController.controller");
            if (runtimeController == null)
            {
                runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PeasantControllerPath);
            }
            if (runtimeController != null)
            {
                animator.runtimeAnimatorController = runtimeController;
                animator.applyRootMotion = false;
            }

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) controller = player.AddComponent<PlayerController>();
            controller.CharacterAnimator = animator;
            controller.HoldPoint = holdPoint;
            controller.PickupRange = 2.6f;
            controller.ThrowForce = 6.5f;
            controller.ThrowUpwardForce = 2.5f;
            controller.CameraRelative = true;
            controller.WalkSpeed = 4.2f;
            controller.SprintSpeed = 8.5f;
            controller.JumpForce = 7.5f;

            return player;
        }

        private static void SetupCameras(GameObject player)
        {
            GameObject targetGO = GameObject.Find("CameraTarget");
            if (targetGO == null)
            {
                Transform existingTarget = player.transform.Find("CameraTarget");
                if (existingTarget != null)
                {
                    targetGO = existingTarget.gameObject;
                }
                else
                {
                    targetGO = new GameObject("CameraTarget");
                }
            }
            targetGO.transform.position = player.transform.position + new Vector3(0f, 1.4f, 0f);
            targetGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            CameraOrbitController orbitController = targetGO.GetComponent<CameraOrbitController>();
            if (orbitController == null) orbitController = targetGO.AddComponent<CameraOrbitController>();
            orbitController.isOrbitPivot = true;
            orbitController.target = player.transform;
            orbitController.targetOffset = new Vector3(0f, 1.4f, 0f);
            orbitController.mouseSensitivity = 2.0f;
            orbitController.requireRightClick = false;
            orbitController.lockCursor = false;
            orbitController.enabled = true;

            GameObject mainCamera = GameObject.FindWithTag("MainCamera");
            if (mainCamera == null)
            {
                mainCamera = GameObject.Find("Main Camera");
            }
            if (mainCamera == null)
            {
                mainCamera = new GameObject("Main Camera");
                mainCamera.tag = "MainCamera";
            }

            mainCamera.transform.position = new Vector3(-1.5f, 2.5f, -11f);
            mainCamera.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            Camera cam = mainCamera.GetComponent<Camera>();
            if (cam == null) cam = mainCamera.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            if (mainCamera.GetComponent<AudioListener>() == null)
            {
                mainCamera.AddComponent<AudioListener>();
            }

            CinemachineBrain brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain == null) brain = mainCamera.GetComponent<CinemachineBrain>();

            var oldOrbit = mainCamera.GetComponent<CameraOrbitController>();
            if (oldOrbit != null) Object.DestroyImmediate(oldOrbit);
            var oldThirdPerson = mainCamera.GetComponent<ThirdPersonCameraFollow>();
            if (oldThirdPerson != null) Object.DestroyImmediate(oldThirdPerson);

            GameObject cmGO = GameObject.Find("CinemachineCamera");
            if (cmGO == null)
            {
                cmGO = new GameObject("CinemachineCamera");
            }
            cmGO.transform.position = mainCamera.transform.position;
            cmGO.transform.rotation = mainCamera.transform.rotation;

            CinemachineCamera cmCam = cmGO.GetComponent<CinemachineCamera>();
            if (cmCam == null) cmCam = cmGO.AddComponent<CinemachineCamera>();

            cmCam.Follow = targetGO.transform;
            cmCam.LookAt = targetGO.transform;

            CinemachineFollow cmFollow = cmGO.GetComponent<CinemachineFollow>();
            if (cmFollow == null) cmFollow = cmGO.AddComponent<CinemachineFollow>();
            cmFollow.TrackerSettings.BindingMode = BindingMode.LockToTargetNoRoll;
            cmFollow.TrackerSettings.PositionDamping = new Vector3(0.08f, 0.08f, 0.08f);
            cmFollow.FollowOffset = new Vector3(0f, 0.4f, -4.5f);

            CinemachineRotationComposer cmAim = cmGO.GetComponent<CinemachineRotationComposer>();
            if (cmAim == null) cmAim = cmGO.AddComponent<CinemachineRotationComposer>();
            cmAim.TargetOffset = Vector3.zero;
            cmAim.Damping = new Vector2(0.05f, 0.05f);
        }

        private static void SetupCargoPlatform()
        {
            GameObject platform = GameObject.Find("CargoPlatform");
            if (platform == null)
            {
                platform = new GameObject("CargoPlatform");
            }

            platform.transform.position = new Vector3(-7.5f, 0.35f, 7.0f);
            platform.transform.rotation = Quaternion.identity;

            BoxCollider solidCollider = platform.GetComponent<BoxCollider>();
            if (solidCollider == null) solidCollider = platform.AddComponent<BoxCollider>();
            solidCollider.size = new Vector3(3.2f, 0.4f, 3.2f);
            solidCollider.center = Vector3.zero;
            solidCollider.isTrigger = false;

            BoxCollider[] colliders = platform.GetComponents<BoxCollider>();
            BoxCollider triggerCollider = null;
            if (colliders.Length > 1)
            {
                triggerCollider = colliders[1];
            }
            else
            {
                triggerCollider = platform.AddComponent<BoxCollider>();
            }
            triggerCollider.size = new Vector3(3.0f, 0.6f, 3.0f);
            triggerCollider.center = new Vector3(0f, 0.4f, 0f);
            triggerCollider.isTrigger = true;

            Rigidbody rb = platform.GetComponent<Rigidbody>();
            if (rb == null) rb = platform.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            if (platform.GetComponent<MovingPlatform>() == null)
            {
                platform.AddComponent<MovingPlatform>();
            }

            Transform visual = platform.transform.Find("Platform_Mesh");
            if (visual == null)
            {
                GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                meshObj.name = "Platform_Mesh";
                meshObj.transform.SetParent(platform.transform, false);
                meshObj.transform.localPosition = Vector3.zero;
                meshObj.transform.localScale = new Vector3(3.2f, 0.35f, 3.2f);
                Object.DestroyImmediate(meshObj.GetComponent<Collider>());

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(PlayerAccentPath);
                if (mat != null)
                {
                    meshObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }

            Animator anim = platform.GetComponent<Animator>();
            if (anim == null) anim = platform.AddComponent<Animator>();
            RuntimeAnimatorController cargoController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CargoControllerPath);
            if (cargoController != null)
            {
                anim.runtimeAnimatorController = cargoController;
            }
        }

        private static void SetupSecurityGate()
        {
            GameObject gate = GameObject.Find("SecurityGate");
            if (gate == null)
            {
                gate = new GameObject("SecurityGate");
            }

            gate.transform.position = new Vector3(4.5f, 0f, -4.0f);
            gate.transform.rotation = Quaternion.identity;

            Transform post = gate.transform.Find("GatePost");
            if (post == null)
            {
                GameObject postObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                postObj.name = "GatePost";
                postObj.transform.SetParent(gate.transform, false);
                postObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                postObj.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
            }

            Transform arm = gate.transform.Find("GateArm");
            if (arm == null)
            {
                GameObject armObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                armObj.name = "GateArm";
                armObj.transform.SetParent(gate.transform, false);
                armObj.transform.localPosition = new Vector3(1.6f, 1.0f, 0f);
                armObj.transform.localScale = new Vector3(3.2f, 1.6f, 0.15f);

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
                if (mat != null)
                {
                    armObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }

            Animator anim = gate.GetComponent<Animator>();
            if (anim == null) anim = gate.AddComponent<Animator>();
            RuntimeAnimatorController gateController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GateControllerPath);
            if (gateController != null)
            {
                anim.runtimeAnimatorController = gateController;
            }
        }

        private static void SetupAnimatedStreetLamp()
        {
            StreetLampEffect existingEffect = Object.FindAnyObjectByType<StreetLampEffect>();
            GameObject lampGO = null;

            if (existingEffect != null)
            {
                lampGO = existingEffect.gameObject;
            }
            else
            {
                GameObject found = GameObject.Find("StreetLamp");
                if (found != null) lampGO = found;
            }

            if (lampGO != null)
            {
                Animator anim = lampGO.GetComponent<Animator>();
                if (anim == null) anim = lampGO.AddComponent<Animator>();

                RuntimeAnimatorController lampController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(StreetLampControllerPath);
                if (lampController != null)
                {
                    anim.runtimeAnimatorController = lampController;
                }

                StreetLampEffect effect = lampGO.GetComponent<StreetLampEffect>();
                if (effect == null) effect = lampGO.AddComponent<StreetLampEffect>();
            }
        }
    }
}
