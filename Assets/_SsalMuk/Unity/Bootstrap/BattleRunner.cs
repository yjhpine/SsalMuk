using System;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class BattleRunner : MonoBehaviour
    {
        private RunModel run;
        private MovementSystem movement;
        private WorldPresenter presenter;
        private Action prepareTerrain;
        private double accumulator;
        public void Configure(RunModel run, MovementSystem movement, WorldPresenter presenter, Action prepareTerrain)
        { this.run = run; this.movement = movement; this.presenter = presenter; this.prepareTerrain = prepareTerrain; }
        private void Update()
        {
            if (run == null || run.Phase != RunPhase.Running) return;
            accumulator += Time.deltaTime;
            while (accumulator >= run.Clock.FixedStep)
            {
                movement.Step(run.Clock.FixedStep); run.Clock.Advance(); accumulator -= run.Clock.FixedStep;
            }
        }
        private void LateUpdate()
        {
            if (run == null || run.Phase != RunPhase.Running) return;
            prepareTerrain(); presenter.Refresh(run);
        }
    }
}
