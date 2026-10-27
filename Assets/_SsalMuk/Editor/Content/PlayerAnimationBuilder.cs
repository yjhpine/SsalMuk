using SsalMuk.Unity;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SsalMuk.Editor
{
    public static class PlayerAnimationBuilder
    {
        public const string Folder = "Assets/_SsalMuk/Content/Animations";
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_SsalMuk/Content", "Animations");
            var idle = Clip("PlayerIdle", new[] { 0f, 0f }, new[] { 0f, 0f });
            var walk = Clip("PlayerWalk", new[] { 0f, 1f, 0f, -1f, 0f }, new[] { 0f, 1f, 0f, 1f, 0f });
            string path = Folder + "/PlayerLocomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
                var machine = controller.layers[0].stateMachine;
                var stopped = machine.AddState("Idle"); stopped.motion = idle; machine.defaultState = stopped;
                var moving = machine.AddState("Walk"); moving.motion = walk;
                Transition(stopped, moving, AnimatorConditionMode.If);
                Transition(moving, stopped, AnimatorConditionMode.IfNot);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>("Assets/_SsalMuk/Content/Definitions/VisualCatalog.asset");
            catalog.ConfigurePlayerLocomotion(controller); EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(controller); AssetDatabase.SaveAssetIfDirty(catalog);
        }
        private static AnimationClip Clip(string name, float[] lean, float[] hop)
        {
            string path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = name, frameRate = 60 }; AssetDatabase.CreateAsset(clip, path); }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(PlayerLocomotionAnimation), "lean"), Curve(lean));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(PlayerLocomotionAnimation), "hop"), Curve(hop));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }
        private static AnimationCurve Curve(float[] values)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < values.Length; i++) curve.AddKey(new Keyframe((float)i / (values.Length - 1), values[i], 0, 0));
            return curve;
        }
        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode condition)
        {
            var transition = from.AddTransition(to); transition.hasExitTime = false;
            transition.hasFixedDuration = true; transition.duration = .06f;
            transition.AddCondition(condition, 0, "Moving");
        }
    }
}
