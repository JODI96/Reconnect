using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reconnect.Client.Rooms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Realistic avatars: one prefab per MakeHuman figure and one Humanoid animator for all of them (Quaternius clips +
    /// wave / nod / head shake built here as muscle clips). Called by ProjectSetup. Mobile budget: each figure is one
    /// skinned mesh per LOD with two materials (opaque atlas, cut-out atlas for hair), 2-bone skinning, LODGroup.
    /// </summary>
    internal static class AvatarSetup
    {
        private const string PrefabDir = "Assets/_Project/Avatars";
        private const string MaterialDir = PrefabDir + "/Materials";
        private const string GestureDir = "Assets/_Project/Animation/Gestures";
        private const string ClipSource = AvatarImportSettings.Animations + "AvatarAnimations.fbx";

        /// <summary>Layer names are part of the contract with <see cref="AvatarView"/>.</summary>
        public static AvatarCatalog Build(string catalogPath, string controllerPath)
        {
            AssetDatabase.DeleteAsset(MaterialDir);   // materials of older builds (one per part)
            Directory.CreateDirectory(MaterialDir);
            AssetDatabase.Refresh();
            var figures = Directory.GetDirectories(AvatarImportSettings.Figures.TrimEnd('/'))
                .Select(dir => dir.Replace('\\', '/'))
                .Where(dir => File.Exists($"{dir}/{Path.GetFileName(dir)}.fbx"))
                .OrderBy(dir => dir)
                .Select(BuildPrefab)
                .ToArray();

            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            catalog.characters = figures;
            catalog.animator = BuildController(controllerPath);
            catalog.scale = 1f;   // real metres
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>Screen height (fraction) below which the next LOD is used; below the last the figure is culled.</summary>
        private static readonly float[] LodHeights = { 0.25f, 0.08f, 0.01f };

        private static GameObject BuildPrefab(string dir)
        {
            var id = Path.GetFileName(dir);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{id}.fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var materials = new Dictionary<string, Material>
            {
                ["opaque"] = Material(dir, id, "opaque"),
                ["cutout"] = Material(dir, id, "cutout"),
            };

            var lods = new List<LOD>();
            for (var level = 0; level < LodHeights.Length; level++)
            {
                var renderer = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == $"{id}_LOD{level}");
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(m => m != null && materials.TryGetValue(m.name, out var ours) ? ours : materials["opaque"])
                    .ToArray();
                renderer.quality = SkinQuality.Bone2;          // phones: 2 bones per vertex
                renderer.updateWhenOffscreen = false;
                renderer.skinnedMotionVectors = false;
                renderer.shadowCastingMode = level < 2 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                lods.Add(new LOD(LodHeights[level], new Renderer[] { renderer }));
            }
            var group = instance.GetComponent<LODGroup>() ?? instance.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath($"{dir}/{id}.fbx").OfType<Avatar>().First();
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // off screen: no bone updates
            if (instance.GetComponent<UprightPosture>() == null)
            {
                instance.AddComponent<UprightPosture>();
            }
            var path = $"{PrefabDir}/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>URP Lit material for one atlas: "opaque" (skin, eyes, clothes) or "cutout" (hair, beard, brows, lashes).</summary>
        private static Material Material(string dir, string id, string atlas)
        {
            var path = $"{MaterialDir}/{id}_{atlas}.mat";
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{atlas}.png"));
            material.SetColor("_BaseColor", Color.white);
            var cutout = atlas == "cutout";
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.4f);
            SetKeyword(material, "_ALPHATEST_ON", cutout);
            material.SetFloat("_Cull", cutout ? 0f : 2f);   // hair cards are seen from both sides
            material.renderQueue = cutout ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            material.SetFloat("_Smoothness", cutout ? 0.25f : 0.3f);
            material.SetFloat("_EnvironmentReflections", 0f);   // cheaper on phones, fabric and skin barely reflect
            SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", true);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static AnimatorController BuildController(string path)
        {
            AssetDatabase.DeleteAsset(path);   // rebuilt from scratch every run
            var clips = AssetDatabase.LoadAllAssetsAtPath(ClipSource).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Swimming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("jump", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;

            var idle = machine.AddState("Idle");
            idle.motion = clips["Idle_Loop"];
            machine.defaultState = idle;
            var talk = machine.AddState("Talk");
            talk.motion = clips["Idle_Talking_Loop"];
            var walk = machine.AddState("Walk");
            walk.motion = clips["Walk_Loop"];
            walk.speed = 1.5f;   // matches AvatarView's brisk walking pace (no sliding feet)
            Connect(idle, walk, AnimatorConditionMode.If, "Walking", 0.2f);
            Connect(talk, walk, AnimatorConditionMode.If, "Walking", 0.2f);
            Connect(walk, idle, AnimatorConditionMode.IfNot, "Walking", 0.25f);
            Connect(idle, talk, AnimatorConditionMode.If, "Talking", 0.3f);
            Connect(talk, idle, AnimatorConditionMode.IfNot, "Talking", 0.4f);

            // Sitting: sit down, stay, stand up.
            var sitDown = machine.AddState("SitDown");
            sitDown.motion = clips["Sitting_Enter"];
            var sitting = machine.AddState("Sitting");
            sitting.motion = clips["Sitting_Idle_Loop"];
            var standUp = machine.AddState("StandUp");
            standUp.motion = clips["Sitting_Exit"];
            Connect(idle, sitDown, AnimatorConditionMode.If, "Sitting", 0.2f);
            Connect(talk, sitDown, AnimatorConditionMode.If, "Sitting", 0.2f);
            ThenTo(sitDown, sitting, 0.15f);
            Connect(sitting, standUp, AnimatorConditionMode.IfNot, "Sitting", 0.15f);
            ThenTo(standUp, idle, 0.2f);

            // Jump on the spot.
            var jumpStart = machine.AddState("JumpStart");
            jumpStart.motion = clips["Jump_Start"];
            var jumpLand = machine.AddState("JumpLand");
            jumpLand.motion = clips["Jump_Land"];
            foreach (var from in new[] { idle, talk })
            {
                var t = from.AddTransition(jumpStart);
                t.AddCondition(AnimatorConditionMode.If, 0, "jump");
                t.hasExitTime = false;
                t.duration = 0.1f;
            }
            ThenTo(jumpStart, jumpLand, 0.08f);
            ThenTo(jumpLand, idle, 0.2f);

            // Relaxed, slightly open hands on top of every clip (the library's hands are half-closed fists).
            AddPoseLayer(controller, "Hands", new[] { AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }, RelaxedHands());

            // Swimming: treading water, or swimming forward while moving. Entered from anywhere, left back to land.
            var swimIdle = machine.AddState("SwimIdle");
            swimIdle.motion = clips["Swim_Idle_Loop"];
            var swim = machine.AddState("Swim");
            swim.motion = clips["Swim_Fwd_Loop"];
            foreach (var (state, walking) in new[] { (swimIdle, false), (swim, true) })
            {
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0, "Swimming");
                enter.AddCondition(walking ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Walking");
                enter.canTransitionToSelf = false;
                enter.duration = 0.3f;
            }
            Connect(swimIdle, idle, AnimatorConditionMode.IfNot, "Swimming", 0.3f);
            Connect(swim, walk, AnimatorConditionMode.IfNot, "Swimming", 0.3f);

            // Gestures on masked layers (AvatarView fades the layer weight in while one plays).
            AddGestureLayer(controller, "Arm Gestures", AvatarMaskBodyPart.RightArm, new[] { ("wave", Wave()) });
            AddGestureLayer(controller, "Head Gestures", AvatarMaskBodyPart.Head, new[] { ("yes", Nod()), ("no", HeadShake()) });
            return controller;
        }

        private static AvatarMask Mask(AnimatorController controller, string name, params AvatarMaskBodyPart[] parts)
        {
            var mask = new AvatarMask { name = name + " Mask" };
            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, parts.Contains((AvatarMaskBodyPart)i));
            }
            AssetDatabase.AddObjectToAsset(mask, controller);
            return mask;
        }

        /// <summary>A layer that always holds one looping pose on the masked body parts.</summary>
        private static void AddPoseLayer(AnimatorController controller, string name, AvatarMaskBodyPart[] parts, AnimationClip clip)
        {
            var machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            machine.defaultState = machine.AddState(name);
            machine.defaultState.motion = clip;
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = name,
                stateMachine = machine,
                avatarMask = Mask(controller, name, parts),
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });
        }

        private static void AddGestureLayer(AnimatorController controller, string name, AvatarMaskBodyPart part,
            IEnumerable<(string Trigger, AnimationClip Clip)> gestures)
        {
            var mask = Mask(controller, name, part);
            var machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var rest = machine.AddState("None");
            machine.defaultState = rest;
            foreach (var (trigger, clip) in gestures)
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = machine.AddState(trigger);
                state.motion = clip;
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                enter.duration = 0.05f;
                enter.canTransitionToSelf = false;
                ThenTo(state, rest, 0.1f);
            }
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = name,
                stateMachine = machine,
                avatarMask = mask,
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });
        }

        // ---------- Gestures as humanoid muscle clips (work on every figure) ----------

        private static AnimationClip Wave() => GestureClip("Wave", 1.8f, new Dictionary<string, AnimationCurve>
        {
            // Hand up beside the head, forearm upright, swinging from the elbow.
            ["Right Shoulder Down-Up"] = Hold(1.8f, 0.5f),
            ["Right Arm Down-Up"] = Hold(1.8f, 0.95f),
            ["Right Arm Front-Back"] = Hold(1.8f, 0.15f),
            ["Right Forearm Stretch"] = Hold(1.8f, -0.25f),
            ["Right Arm Twist In-Out"] = Oscillate(1.8f, 0.1f, 0.45f, 3),
            ["Right Hand In-Out"] = Oscillate(1.8f, 0f, 0.3f, 3),
        });

        private static AnimationClip RelaxedHands()
        {
            var muscles = new Dictionary<string, AnimationCurve>();
            foreach (var side in new[] { "Left", "Right" })
            {
                foreach (var finger in new[] { "Index", "Middle", "Ring", "Little" })
                {
                    // The little finger curls a bit more than the index, like a hand at rest.
                    var curl = finger switch { "Index" => 0f, "Middle" => 0.05f, "Ring" => 0.1f, _ => 0.15f };
                    muscles[$"{side}Hand.{finger}.1 Stretched"] = Constant(1f, 0.3f - curl);
                    muscles[$"{side}Hand.{finger}.2 Stretched"] = Constant(1f, 0.15f - curl);
                    muscles[$"{side}Hand.{finger}.3 Stretched"] = Constant(1f, 0.3f - curl);
                    muscles[$"{side}Hand.{finger}.Spread"] = Constant(1f, -0.3f);
                }
                muscles[$"{side}Hand.Thumb.1 Stretched"] = Constant(1f, 0.2f);
                muscles[$"{side}Hand.Thumb.2 Stretched"] = Constant(1f, 0.4f);
                muscles[$"{side}Hand.Thumb.3 Stretched"] = Constant(1f, 0.4f);
                muscles[$"{side}Hand.Thumb.Spread"] = Constant(1f, 0.2f);
            }
            var clip = GestureClip("RelaxedHands", 1f, muscles);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static AnimationCurve Constant(float length, float value) => AnimationCurve.Constant(0f, length, value);

        private static AnimationClip Nod() => GestureClip("Nod", 1.3f, new Dictionary<string, AnimationCurve>
        {
            ["Head Nod Down-Up"] = Oscillate(1.3f, 0f, 0.55f, 2, downFirst: true),
            ["Neck Nod Down-Up"] = Oscillate(1.3f, 0f, 0.25f, 2, downFirst: true),
            ["Head Turn Left-Right"] = Hold(1.3f, 0f),
            ["Neck Turn Left-Right"] = Hold(1.3f, 0f),
        });

        private static AnimationClip HeadShake() => GestureClip("HeadShake", 1.3f, new Dictionary<string, AnimationCurve>
        {
            ["Head Turn Left-Right"] = Oscillate(1.3f, 0f, 0.6f, 3),
            ["Neck Turn Left-Right"] = Oscillate(1.3f, 0f, 0.25f, 3),
            ["Head Nod Down-Up"] = Hold(1.3f, 0.05f),
            ["Neck Nod Down-Up"] = Hold(1.3f, 0f),
        });

        private static AnimationClip GestureClip(string name, float length, Dictionary<string, AnimationCurve> muscles)
        {
            Directory.CreateDirectory(GestureDir);
            var path = $"{GestureDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            foreach (var (muscle, curve) in muscles)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>Eases into <paramref name="value"/>, holds, eases back to rest (0).</summary>
        private static AnimationCurve Hold(float length, float value) => new(
            new Keyframe(0f, 0f), new Keyframe(0.3f, value), new Keyframe(length - 0.3f, value), new Keyframe(length, 0f));

        /// <summary>Swings around <paramref name="centre"/> <paramref name="swings"/> times between the ease in/out.</summary>
        private static AnimationCurve Oscillate(float length, float centre, float amplitude, int swings, bool downFirst = false)
        {
            var keys = new List<Keyframe> { new(0f, 0f), new(0.25f, centre) };
            var span = (length - 0.5f) / (swings * 2);
            var sign = downFirst ? -1f : 1f;
            for (var i = 0; i < swings * 2; i++)
            {
                keys.Add(new Keyframe(0.25f + span * (i + 0.5f), centre + sign * amplitude));
                sign = -sign;
            }
            keys.Add(new Keyframe(length - 0.25f, centre));
            keys.Add(new Keyframe(length, 0f));
            var curve = new AnimationCurve(keys.ToArray());
            for (var i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        private static void Connect(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter, float blend)
        {
            var transition = from.AddTransition(to);
            transition.AddCondition(mode, 0, parameter);
            transition.hasExitTime = false;
            transition.duration = blend;
        }

        private static void ThenTo(AnimatorState from, AnimatorState to, float blend)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.duration = blend;
        }
    }
}
