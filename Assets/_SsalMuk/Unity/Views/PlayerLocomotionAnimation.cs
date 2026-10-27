using System;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SsalMuk.Unity
{
    // Only the visual child is animated. Manual evaluation follows active game time, including ESC pause.
    public sealed class PlayerLocomotionAnimation : MonoBehaviour
    {
        [SerializeField] private float lean, hop;
        private PlayableGraph graph;
        private AnimatorControllerPlayable controller;
        private Animator animator;
        private RuntimeAnimatorController asset;
        private bool moving;
        private static readonly int Moving = Animator.StringToHash("Moving");
        public WalkPose Step(DVec2 input, double dt, VisualCatalog visuals)
        {
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!graph.IsValid() || asset != visuals.PlayerLocomotion) Initialize(visuals.PlayerLocomotion);
            bool next = input.Length > 1e-6;
            if (next != moving)
            {
                moving = next; controller.SetBool(Moving, moving);
                graph.Evaluate(0);
            }
            controller.SetSpeed(visuals.WalkCycles);
            if (dt > 0) graph.Evaluate((float)dt);
            return new WalkPose(lean * visuals.WalkAngle, hop * visuals.WalkHeight);
        }
        private void Initialize(RuntimeAnimatorController source)
        {
            if (source == null) throw new InvalidOperationException("Player locomotion controller is missing.");
            Release(); asset = source;
            animator = GetComponent<Animator>(); if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.enabled = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Player locomotion"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            controller = AnimatorControllerPlayable.Create(graph, source);
            AnimationPlayableOutput.Create(graph, "Player pose", animator).SetSourcePlayable(controller);
            graph.Play(); controller.Play("Idle", 0, 0); graph.Evaluate(0);
        }
        public void ResetPose()
        {
            moving = false; lean = hop = 0;
            if (graph.IsValid()) { controller.SetBool(Moving, false); controller.Play("Idle", 0, 0); graph.Evaluate(0); }
        }
        public void Release()
        {
            if (graph.IsValid()) graph.Destroy();
            moving = false; lean = hop = 0; asset = null;
            if (animator != null) animator.enabled = false;
        }
        private void OnDestroy() => Release();
    }
}
