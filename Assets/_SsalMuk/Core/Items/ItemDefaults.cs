using System.Collections.Generic;
namespace SsalMuk.Core
{
    public static class ItemDefaults
    {
        public static ItemCatalog Create()
        {
            var items = new List<ItemDefinition>(); var recipes = new List<ItemRecipe>();
            string[] families = { "blade", "feather", "boots", "herb", "magnet", "book" };
            string[] common = { "무딘 칼날", "작은 깃털", "낡은 신발", "약초 잎", "자석 조각", "지식 파편" };
            string[] uncommon = { "강철 문장", "매의 깃털", "바람 신발", "생명 약초", "자석 핵", "지식의 서" };
            string[] symbols = { "검", "깃", "발", "+", "자", "책" };
            for (int tier = 0; tier < 2; tier++) for (int f = 0; f < 6; f++)
            {
                var bonus = f == 0 ? new ItemBonuses(damage: tier == 0 ? .05 : .18) : f == 1 ? new ItemBonuses(attackSpeed: tier == 0 ? .04 : .15) :
                    f == 2 ? new ItemBonuses(moveSpeed: tier == 0 ? .03 : .10) : f == 3 ? new ItemBonuses(regeneration: tier == 0 ? .1 : .35) :
                    f == 4 ? new ItemBonuses(pickup: tier == 0 ? .10 : .35) : new ItemBonuses(experiencePercent: tier == 0 ? 5 : 18);
                items.Add(new ItemDefinition((tier == 0 ? "c_" : "u_") + families[f], tier == 0 ? common[f] : uncommon[f], symbols[f], (ItemRarity)tier, bonus));
            }
            string[] prefixes = { "r_", "l_", "x_" };
            string[,] names = { { "흡혈 송곳니", "번개 핵", "불씨 심장", "수호 부적" }, { "혈월 이빨", "폭풍 핵", "화염 심장", "성채 문장" }, { "불멸의 심장", "뇌신의 왕관", "태양의 핵", "영원의 방패" } };
            double[] damage = { .18, .40, .90 }, life = { .10, .15, .20 }, lifeCap = { 2, 5, 10 }, speed = { .15, .30, .60 }, chain = { .2, .4, .7 },
                range = { 3, 4, 5 }, explosion = { .6, 1, 1.6 }, radius = { 1.8, 2.4, 3 }, regen = { .35, .8, 2 }, shield = { 20, 40, 80 }, interval = { 8, 7, 6 };
            int[] count = { 2, 3, 5 };
            for (int t = 0; t < 3; t++)
            {
                var rarity = (ItemRarity)(t + 2); string p = prefixes[t];
                items.Add(new ItemDefinition(p + "fang", names[t, 0], "혈", rarity, new ItemBonuses(damage: damage[t], lifeRatio: life[t], lifeCap: lifeCap[t])));
                items.Add(new ItemDefinition(p + "lightning", names[t, 1], "뢰", rarity, new ItemBonuses(attackSpeed: speed[t], chainDamage: chain[t], chainCount: count[t], chainRange: range[t])));
                items.Add(new ItemDefinition(p + "fire", names[t, 2], "화", rarity, new ItemBonuses(damage: damage[t], explosionDamage: explosion[t], explosionRadius: radius[t])));
                items.Add(new ItemDefinition(p + "shield", names[t, 3], "방", rarity, new ItemBonuses(regeneration: regen[t], shieldCapacity: shield[t], shieldInterval: interval[t])));
            }
            string[] third = { "feather", "boots", "book", "blade", "boots", "herb" };
            for (int f = 0; f < 6; f++) recipes.Add(new ItemRecipe("make_u_" + families[f], new[] { "c_" + families[f], "c_" + families[f], "c_" + third[f] }, "u_" + families[f]));
            recipes.Add(new ItemRecipe("make_r_fang", new[] { "u_blade", "u_herb", "u_boots" }, "r_fang"));
            recipes.Add(new ItemRecipe("make_r_lightning", new[] { "u_feather", "u_magnet", "u_book" }, "r_lightning"));
            recipes.Add(new ItemRecipe("make_r_fire", new[] { "u_blade", "u_feather", "u_magnet" }, "r_fire"));
            recipes.Add(new ItemRecipe("make_r_shield", new[] { "u_herb", "u_boots", "u_book" }, "r_shield"));
            string[] unique = { "fang", "lightning", "fire", "shield" }, partner = { "fire", "fang", "lightning", "fang" };
            for (int t = 0; t < 2; t++) for (int f = 0; f < 4; f++) recipes.Add(new ItemRecipe("make_" + prefixes[t + 1] + unique[f],
                new[] { prefixes[t] + unique[f], prefixes[t] + unique[f], prefixes[t] + partner[f] }, prefixes[t + 1] + unique[f]));
            return new ItemCatalog(items, recipes);
        }
    }
}
