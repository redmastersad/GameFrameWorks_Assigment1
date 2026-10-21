using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameFrameworks.Editor
{
    public static class AnimatorStateMachineBuilder
    {
        private const string ControllerFolder = "Assets/Animations";
        private const string ControllerPath = "Assets/Animations/Player_AnimatorController.controller";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Assignment 2/Build Animator State Machine", false, 10)]
        public static void BuildStateMachineMenu()
        {
            ConfigureKevinIglesiasAssets();
            BuildAnimatorController();
            AssignControllerToPlayerInScene();
        }

        public static void ConfigureKevinIglesiasAssets()
        {
            if (!Directory.Exists("Assets/Kevin Iglesias")) return;

            string[] fbxFiles = Directory.GetFiles("Assets/Kevin Iglesias", "*.fbx", SearchOption.AllDirectories);
            foreach (string file in fbxFiles)
            {
                string assetPath = file.Replace("\\", "/");
                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    bool modified = false;
                    if (importer.animationType != ModelImporterAnimationType.Human)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        modified = true;
                    }

                    ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                    if (clips != null && clips.Length > 0)
                    {
                        foreach (var clip in clips)
                        {
                            if (assetPath.Contains("Idle") || assetPath.Contains("Walk") || assetPath.Contains("Run"))
                            {
                                if (!clip.loopTime)
                                {
                                    clip.loopTime = true;
                                    clip.loopPose = true;
                                    modified = true;
                                }
                            }
                        }
                        importer.clipAnimations = clips;
                        modified = true;
                    }

                    if (modified)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        public static AnimatorController BuildAnimatorController()
        {
            if (!AssetDatabase.IsValidFolder(ControllerFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }

            // Load or create the Animator Controller
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // 1. Setup Parameters
            EnsureParameter(controller, "Walking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Running", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Land", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "MotionScale", AnimatorControllerParameterType.Float);

            // 2. Fetch Base Layer and StateMachine
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            // Clear previous states to build a clean, deterministic state machine
            var childStates = stateMachine.states;
            foreach (var childState in childStates)
            {
                stateMachine.RemoveState(childState.state);
            }

            // 3. Find Animation Clips (Priority: Human Basic Motions / Kevin Iglesias, fallback: Cameron/Peasant)
            AnimationClip idleClip = FindClip(new[] { "HumanM@Idle01", "HumanF@Idle01", "Idle01", "Idle", "metarig|Idle" });
            AnimationClip walkClip = FindClip(new[] { "HumanM@Walk01_Forward", "HumanF@Walk01_Forward", "Walk01_Forward", "Walk", "metarig|Walk" });
            AnimationClip runClip = FindClip(new[] { "HumanM@Run01_Forward", "HumanF@Run01_Forward", "Run01_Forward", "Run" });
            AnimationClip jumpClip = FindClip(new[] { "HumanM@Jump01", "HumanF@Jump01", "Jump01", "Jump", "JumpUp" });
            AnimationClip attackClip = FindClip(new[] { "HumanM@Attack1H01_R", "Attack1H01_R", "Attack1H01", "Attack", "Punch", "Strike" });

            if (attackClip == null)
            {
                attackClip = CreateOrGetPlaceholderAttackClip();
            }

            // 4. Create States
            // Idle (Default State)
            AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250, 120, 0));
            idleState.motion = idleClip;
            stateMachine.defaultState = idleState;

            // Walk
            AnimatorState walkState = stateMachine.AddState("Walk", new Vector3(250, 220, 0));
            walkState.motion = walkClip;
            walkState.speed = 1.0f;

            // Run (Extra Points)
            AnimatorState runState = stateMachine.AddState("Run", new Vector3(250, 320, 0));
            runState.motion = runClip;
            runState.speed = 1.15f;

            // Jump
            AnimatorState jumpState = stateMachine.AddState("Jump", new Vector3(550, 160, 0));
            jumpState.motion = jumpClip;
            jumpState.speed = 1.0f;

            // Attack (Extra Points)
            AnimatorState attackState = stateMachine.AddState("Attack", new Vector3(550, 280, 0));
            attackState.motion = attackClip;
            attackState.speed = 1.0f;

            // 5. Create Transitions & Conditions

            // --- Idle <-> Walk ---
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");

            // --- Walk <-> Run ---
            var walkToRun = walkState.AddTransition(runState);
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0.15f;
            walkToRun.AddCondition(AnimatorConditionMode.If, 0, "Running");

            var runToWalk = runState.AddTransition(walkState);
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0.15f;
            runToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");

            var runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.15f;
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");

            // --- Jump Transitions (From Idle, Walk, Run) ---
            var idleToJump = idleState.AddTransition(jumpState);
            idleToJump.hasExitTime = false;
            idleToJump.duration = 0.1f;
            idleToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var walkToJump = walkState.AddTransition(jumpState);
            walkToJump.hasExitTime = false;
            walkToJump.duration = 0.1f;
            walkToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var runToJump = runState.AddTransition(jumpState);
            runToJump.hasExitTime = false;
            runToJump.duration = 0.1f;
            runToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            // --- Return from Jump to appropriate state ---
            // 1. To Idle on Land
            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = false;
            jumpToIdle.duration = 0.15f;
            jumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "Land");
            jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");

            // 2. To Walk on Land
            var jumpToWalk = jumpState.AddTransition(walkState);
            jumpToWalk.hasExitTime = false;
            jumpToWalk.duration = 0.15f;
            jumpToWalk.AddCondition(AnimatorConditionMode.If, 0, "Land");
            jumpToWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");
            jumpToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");

            // 3. To Run on Land
            var jumpToRun = jumpState.AddTransition(runState);
            jumpToRun.hasExitTime = false;
            jumpToRun.duration = 0.15f;
            jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "Land");
            jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "Walking");
            jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "Running");

            // 4. Fallback exit time from Jump to Idle
            var jumpFallback = jumpState.AddTransition(idleState);
            jumpFallback.hasExitTime = true;
            jumpFallback.exitTime = 0.9f;
            jumpFallback.duration = 0.2f;

            // --- Attack Transitions (From Idle, Walk, Run) ---
            var idleToAttack = idleState.AddTransition(attackState);
            idleToAttack.hasExitTime = false;
            idleToAttack.duration = 0.1f;
            idleToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var walkToAttack = walkState.AddTransition(attackState);
            walkToAttack.hasExitTime = false;
            walkToAttack.duration = 0.1f;
            walkToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var runToAttack = runState.AddTransition(attackState);
            runToAttack.hasExitTime = false;
            runToAttack.duration = 0.1f;
            runToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            // --- Return from Attack to appropriate state ---
            // 1. To Idle after attack finishes
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.85f;
            attackToIdle.duration = 0.15f;
            attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");

            // 2. To Walk after attack finishes
            var attackToWalk = attackState.AddTransition(walkState);
            attackToWalk.hasExitTime = true;
            attackToWalk.exitTime = 0.85f;
            attackToWalk.duration = 0.15f;
            attackToWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");
            attackToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");

            // 3. To Run after attack finishes
            var attackToRun = attackState.AddTransition(runState);
            attackToRun.hasExitTime = true;
            attackToRun.exitTime = 0.85f;
            attackToRun.duration = 0.15f;
            attackToRun.AddCondition(AnimatorConditionMode.If, 0, "Walking");
            attackToRun.AddCondition(AnimatorConditionMode.If, 0, "Running");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AnimatorStateMachineBuilder] Successfully generated State Machine at: {ControllerPath}");
            Debug.Log($"[AnimatorStateMachineBuilder] Clips used: Idle='{(idleClip != null ? idleClip.name : "none")}', Walk='{(walkClip != null ? walkClip.name : "none")}', Run='{(runClip != null ? runClip.name : "none")}', Jump='{(jumpClip != null ? jumpClip.name : "none")}', Attack='{(attackClip != null ? attackClip.name : "none")}'");

            return controller;
        }

        public static void AssignControllerToPlayerInScene()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            RuntimeAnimatorController runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (runtimeController == null)
            {
                runtimeController = BuildAnimatorController();
            }

            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Animator animator = player.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = runtimeController;
                    EditorUtility.SetDirty(animator);
                }

                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null && animator != null)
                {
                    pc.CharacterAnimator = animator;
                    EditorUtility.SetDirty(pc);
                }

                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                Debug.Log("[AnimatorStateMachineBuilder] Successfully assigned Animator Controller to Player in SampleScene!");
            }
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }
            controller.AddParameter(paramName, type);
        }

        private static AnimationClip FindClip(string[] candidates)
        {
            // 1. Check all clips in Assets
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            List<AnimationClip> allClips = new List<AnimationClip>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) allClips.Add(clip);
            }

            // Also check sub-assets inside FBX models
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model");
            foreach (string guid in fbxGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                foreach (var sub in subAssets)
                {
                    if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        allClips.Add(clip);
                    }
                }
            }

            // Search by candidate preference
            foreach (string candidate in candidates)
            {
                // Prefer Kevin Iglesias / Human Basic Motions
                foreach (var clip in allClips)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (path.Contains("Kevin") || path.Contains("Basic") || path.Contains("Human"))
                    {
                        if (clip.name.Equals(candidate, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.Contains(candidate) ||
                            path.Contains(candidate))
                        {
                            return clip;
                        }
                    }
                }

                // Fallback to any match
                foreach (var clip in allClips)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (clip.name.Equals(candidate, System.StringComparison.OrdinalIgnoreCase) ||
                        clip.name.Contains(candidate) ||
                        path.Contains(candidate))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static AnimationClip CreateOrGetPlaceholderAttackClip()
        {
            const string attackClipPath = "Assets/Animations/Player_Attack_Placeholder.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(attackClipPath);
            if (clip != null) return clip;

            clip = new AnimationClip { name = "Player_Attack" };
            // Simple 0.6 second dummy clip curve to ensure valid duration
            AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 0.6f, 1f);
            clip.SetCurve("", typeof(Transform), "localPosition.z", curve);
            AssetDatabase.CreateAsset(clip, attackClipPath);
            AssetDatabase.SaveAssets();
            return clip;
        }
    }
}
