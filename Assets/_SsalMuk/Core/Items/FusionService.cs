using System;
using System.Collections.Generic;
using System.Linq;
namespace SsalMuk.Core
{
    public enum FusionMode { Random, Fixed }
    public sealed class FusionCommand
    {
        public Guid RunId { get; }
        public long Version { get; }
        public IReadOnlyList<long> Ids { get; }
        public FusionMode Mode { get; }
        public string RecipeId { get; }
        public FusionCommand(Guid runId, long version, IReadOnlyList<long> ids, FusionMode mode, string recipe = null)
        { RunId = runId; Version = version; Ids = Array.AsReadOnly(ids?.ToArray() ?? Array.Empty<long>()); Mode = mode; RecipeId = recipe; }
    }
    public sealed class FusionPreview
    {
        public string Reason { get; }
        public bool IsValid => Reason.Length == 0;
        public ItemDefinition FixedResult { get; }
        public string RecipeId { get; }
        public IReadOnlyList<ItemDefinition> RandomResults { get; }
        internal FusionPreview(string reason, IReadOnlyList<ItemDefinition> results = null, ItemDefinition fixedResult = null, string recipe = null)
        { Reason = reason; RandomResults = results ?? Array.Empty<ItemDefinition>(); FixedResult = fixedResult; RecipeId = recipe; }
    }
    public sealed class FusionService
    {
        private readonly RunModel run;
        private readonly IRandomSource random;
        private FusionCommand queued;
        public bool HasQueuedCommand => queued != null;
        public FusionService(RunModel run)
        { this.run = run; random = new SeededRandom(unchecked((int)StableHash.Combine(run.Seed, 0, 0, 1, 1103))); }
        public FusionPreview Preview(IReadOnlyList<long> ids)
        {
            if (ids == null || ids.Count != 3) return new FusionPreview("재료 3개를 선택하세요");
            if (new HashSet<long>(ids).Count != 3) return new FusionPreview("같은 칸을 두 번 사용할 수 없습니다");
            var defs = new List<ItemDefinition>(3);
            foreach (long id in ids)
            { if (!run.Inventory.TryGet(id, out var item)) return new FusionPreview("보유하지 않은 재료입니다"); defs.Add(item.Definition); }
            var rarity = defs[0].Rarity;
            if (defs.Any(d => d.Rarity != rarity)) return new FusionPreview("같은 등급의 재료만 선택하세요");
            if (rarity == ItemRarity.Ultimate) return new FusionPreview("얼티밋은 최고 등급입니다");
            var catalog = run.Inventory.Catalog; var pool = catalog.Pool(rarity + 1);
            foreach (var output in pool) if (!run.Inventory.CanReplace(ids, output, out _)) return new FusionPreview("현재 수치 범위에서 합성할 수 없습니다");
            var recipe = catalog.FindRecipe(defs.Select(d => d.Id));
            return new FusionPreview("", pool, recipe == null ? null : catalog.Get(recipe.ResultId), recipe?.Id);
        }
        private bool Valid(FusionCommand command, out FusionPreview preview)
        {
            preview = null;
            if (command == null || run.Phase != RunPhase.Running || run.Player == null || !run.Player.IsAlive ||
                command.RunId != run.Id || command.Version != run.Inventory.Version || !Enum.IsDefined(typeof(FusionMode), command.Mode)) return false;
            preview = Preview(command.Ids);
            return preview.IsValid && (command.Mode == FusionMode.Random || preview.FixedResult != null && preview.RecipeId == command.RecipeId);
        }
        public bool TryQueue(FusionCommand command)
        { if (run.IsPaused || queued != null || !Valid(command, out _)) return false; queued = command; return true; }
        public void Step()
        {
            if (run.IsPaused) return;
            var command = queued; queued = null;
            if (!Valid(command, out var preview)) return;
            // Preflight all random outcomes before consuming RNG, so failure is side-effect free.
            var output = command.Mode == FusionMode.Fixed ? preview.FixedResult : preview.RandomResults[(int)(random.NextUnit() * preview.RandomResults.Count)];
            run.Inventory.CanReplace(command.Ids, output, out var bonuses);
            run.Inventory.Replace(command.Ids, output, bonuses);
        }
        public void Clear() => queued = null;
    }
}
