using System;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class BattleRunner : MonoBehaviour
    {
        private RunModel run;
        private RunSimulation simulation;
        public RunSimulation Simulation => simulation;
        private WorldPresenter presenter;
        private Action prepareTerrain;
        private double accumulator;
        private Camera battleCamera;
        public void Configure(RunModel run, RunSimulation simulation, WorldPresenter presenter, Action prepareTerrain)
        { this.run = run; this.simulation = simulation; this.presenter = presenter; this.prepareTerrain = prepareTerrain; }
        private void FixedUpdate()
        {
            if (run == null || run.Phase != RunPhase.Running) return;
            if (battleCamera == null) battleCamera = Camera.main;
            if (battleCamera != null)
            {
                var center = run.ViewOrigin.Offset(new DVec2(battleCamera.transform.position.x, battleCamera.transform.position.y));
                simulation.SetViewBounds(new WorldRect(center, battleCamera.orthographicSize * battleCamera.aspect, battleCamera.orthographicSize));
            }
            accumulator += Time.fixedDeltaTime;
            while (accumulator + 1e-8 >= run.Clock.FixedStep && run.Phase == RunPhase.Running)
            {
                simulation.Step(run.Clock.FixedStep); accumulator -= run.Clock.FixedStep;
            }
        }
        private void LateUpdate()
        {
            if (run == null || run.Phase != RunPhase.Running) return;
            prepareTerrain();
            double alpha = Math.Max(0, Math.Min(1, (Time.timeAsDouble - Time.fixedTimeAsDouble) / run.Clock.FixedStep));
            double radius = battleCamera != null ? battleCamera.orthographicSize * Math.Sqrt(1 + battleCamera.aspect * battleCamera.aspect) + 1.5 : 22;
            presenter.Refresh(run, alpha, radius);
        }
    }
}
