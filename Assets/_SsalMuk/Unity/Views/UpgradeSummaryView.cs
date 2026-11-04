using System;
using System.Numerics;
using System.Text;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class UpgradeSummaryView
    {
        private readonly UnityEngine.UI.Text text;
        private readonly UnityEngine.UI.ScrollRect scroll;
        private readonly BigInteger[] shown = new BigInteger[20];
        private Guid runId;
        private long ownership = -1;
        private static readonly WeaponKind[] weapons = { WeaponKind.Sword, WeaponKind.Spear, WeaponKind.Axe, WeaponKind.Fireball };
        private static readonly UpgradeKind[] kinds = { UpgradeKind.Damage, UpgradeKind.Copies, UpgradeKind.Speed, UpgradeKind.Range };
        private static readonly string[] weaponNames = { "철검", "철창", "철도끼", "파이어볼" }, sharedNames = { "이동속도", "초당 회복", "흡수 범위", "경험치 획득" };
        public UpgradeSummaryView(Transform parent, Font font)
        {
            var panel = ItemUiStyle.Panel(parent, "UpgradeSummary", new UnityEngine.Vector2(212,360), new UnityEngine.Vector2(0,1), new UnityEngine.Vector2(118,-330));
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            UiFactory.Label(panel, "Heading", font, "강화", 21, UnityEngine.Color.white, new UnityEngine.Vector2(188,35), new UnityEngine.Vector2(.5f,1), new UnityEngine.Vector2(0,-23));
            var viewport = UiFactory.Rect(panel, "UpgradeScroll", new UnityEngine.Vector2(186,304), new UnityEngine.Vector2(.5f,1), new UnityEngine.Vector2(0,-206));
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false;
            scroll.viewport = viewport; scroll.scrollSensitivity = 30;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            text = UiFactory.Label(viewport, "Levels", font, "", 14, new Color32(219,231,235,255), new UnityEngine.Vector2(186,304), new UnityEngine.Vector2(.5f,1), UnityEngine.Vector2.zero, TextAnchor.UpperLeft);
            text.rectTransform.pivot = new UnityEngine.Vector2(.5f,1); scroll.content = text.rectTransform;
        }
        public void Refresh(RunModel run)
        {
            bool newRun = runId != run.Id;
            bool dirty = newRun || ownership != run.Player.Weapons.OwnershipVersion;
            runId = run.Id; ownership = run.Player.Weapons.OwnershipVersion;
            for (int w = 0; w < 4; w++) for (int k = 0; k < 4; k++)
            {
                BigInteger level = run.Player.Weapons.Owns(weapons[w]) ? run.Player.Weapons.Get(weapons[w]).GetLevel(kinds[k]) : -1;
                int index = w * 4 + k; if (shown[index] != level) { dirty = true; shown[index] = level; }
            }
            for (int i = 0; i < 4; i++) { var value = run.Player.Upgrades.GetLevel((SharedUpgradeKind)i); if (shown[16+i] != value) { dirty = true; shown[16+i] = value; } }
            if (!dirty) return;
            var s = new StringBuilder(); string N(int i) => NumberFormatter.Format(shown[i]);
            for (int w = 0; w < 4; w++) if (shown[w*4] >= 0)
                s.Append("<b>").Append(weaponNames[w]).Append("</b>\n공격력 ").Append(N(w*4)).Append("  ·  개수 ").Append(N(w*4+1))
                    .Append("\n공속 ").Append(N(w*4+2)).Append("  ·  범위 ").Append(N(w*4+3)).Append("\n");
            s.Append("\n<b>공용 강화</b>");
            for (int i = 0; i < 4; i++) s.Append('\n').Append(sharedNames[i]).Append("  ").Append(N(16+i));
            text.text = s.ToString();
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(304, text.preferredHeight));
            if (newRun) scroll.verticalNormalizedPosition = 1;
        }
    }
}
