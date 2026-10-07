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

        [MenuItem("Assignment 2/Setup Playable Scene", false, 1)]
        public static void BuildPlayableScene()
        {
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

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
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

            Transform oldVisual = player.transform.Find("Visual");
            if (oldVisual != null)
            {
                Object.DestroyImmediate(oldVisual.gameObject);
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

            RuntimeAnimatorController runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PeasantControllerPath);
            if (runtimeController != null)
            {
                animator.runtimeAnimatorController = runtimeController;
                animator.applyRootMotion = false;
            }

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) controller = player.AddComponent<PlayerController>();
            controller.CharacterAnimator = animator;
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
            if (brain == null) brain = mainCamera.AddComponent<CinemachineBrain>();

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
