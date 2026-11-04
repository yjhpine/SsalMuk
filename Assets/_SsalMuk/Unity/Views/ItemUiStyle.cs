using System.Globalization;
using System.Linq;
using System.Text;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public static class ItemUiStyle
    {
        private static readonly Color32[] colors = { new Color32(184,190,199,255), new Color32(112,214,107,255), new Color32(98,176,255,255), new Color32(255,215,90,255), new Color32(255,101,113,255) };
        private static readonly string[] names = { "커먼", "언커먼", "레어", "레전더리", "얼티밋" };
        public static Color Color(ItemRarity rarity) => colors[(int)rarity];
        public static string RarityName(ItemRarity rarity) => names[(int)rarity];
        private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        public static string Description(ItemDefinition item)
        {
            var b = item.Bonuses; var s = new StringBuilder();
            void Line(string value) { if (s.Length > 0) s.Append('\n'); s.Append(value); }
            if (b.Damage > 0) Line("전체 무기 공격력 +" + N(b.Damage * 100) + "%");
            if (b.AttackSpeed > 0) Line("전체 무기 공속 +" + N(b.AttackSpeed * 100) + "%");
            if (b.MoveSpeed > 0) Line("이동속도 +" + N(b.MoveSpeed * 100) + "%");
            if (b.Pickup > 0) Line("경험치 흡수 반경 +" + N(b.Pickup * 100) + "%");
            if (b.Damage > 0 || b.AttackSpeed > 0 || b.MoveSpeed > 0 || b.Pickup > 0) Line("현재 성장에 추가 배율 · 같은 항목 합산");
            if (b.Regeneration > 0) Line("초당 체력 회복 +" + N(b.Regeneration));
            if (b.ExperiencePercent > 0) Line("경험치 획득 +" + b.ExperiencePercent + "% · 공용 강화와 합산 · 소수 유지");
            if (b.LifeRatio > 0) Line("흡혈: 직접 피해의 " + N(b.LifeRatio * 100) + "% 회복\n1회 최대 " + N(b.LifeCap) + " · 계열 간격 0.5초");
            if (b.ChainDamage > 0) Line("연쇄 번개: 다른 적 " + b.ChainCount + "명 · 연결 거리 " + N(b.ChainRange) + "\n원래 공격력의 " + N(b.ChainDamage * 100) + "% 피해 · 계열 간격 1초");
            if (b.ExplosionDamage > 0) Line("직접 처치 시 반경 " + N(b.ExplosionRadius) + " 폭발\n원래 공격력의 " + N(b.ExplosionDamage * 100) + "% 피해 · 계열 간격 0.5초");
            if (b.ShieldCapacity > 0) Line("보호막 +" + N(b.ShieldCapacity) + " · " + N(b.ShieldInterval) + "초마다 재충전\n추가 획득·합성 시 잔량 유지");
            if (b.LifeRatio > 0 || b.ChainDamage > 0 || b.ExplosionDamage > 0) Line("중복 보유: 위력 합산 · 계열당 한 번 발동\n아이템 피해로 고유 효과가 다시 발동하지 않음");
            return s.ToString();
        }
        public static string Recipes(ItemDefinition item, ItemCatalog catalog) => string.Join("\n", catalog.Recipes
            .Where(r => r.ResultId == item.Id || r.Materials.Contains(item.Id)).Select(r =>
                string.Join(" + ", r.Materials.GroupBy(id => id).Select(g => catalog.Get(g.Key).Name + (g.Count() > 1 ? " ×" + g.Count() : ""))) + " → " + catalog.Get(r.ResultId).Name));
        internal static RectTransform Panel(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 offset)
        {
            var rect = UiFactory.Rect(parent, name, size, anchor, offset);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(12,24,31,238); return rect;
        }
        internal static UnityEngine.UI.Button Button(Transform parent, Font font, string name, string caption, Vector2 size, Vector2 anchor, Vector2 offset)
        {
            var button = UiFactory.Button(parent, name, font, caption, offset); var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size;
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(); label.fontSize = 17;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4,2); label.rectTransform.offsetMax = new Vector2(-4,-2); return button;
        }
    }
}
