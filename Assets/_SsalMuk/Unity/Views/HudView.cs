using UnityEngine;
using UnityEngine.UI;
using SsalMuk.Presentation;

namespace SsalMuk.Unity
{
    public sealed class HudView : MonoBehaviour, IHudView
    {
        private Canvas canvas;
        private RectTransform healthBar, experienceBar;
        private Text experienceText, timeText, killsText;
        public Text HealthText { get; private set; }
        public Text LevelText { get; private set; }
        public void Initialize(Font font)
        {
            canvas = UiFactory.Canvas(transform, "BattleHud", 40);
            var top = UiFactory.Rect(canvas.transform, "Backdrop", new Vector2(0, 96), new Vector2(0, 1), new Vector2(0, -48));
            top.anchorMax = new Vector2(1, 1); top.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.07f, 0.07f, 0.92f);
            UiFactory.Label(canvas.transform, "HealthCaption", font, "체력", 16, UiFactory.Muted, new Vector2(100, 25), new Vector2(0, 1), new Vector2(70, -20), TextAnchor.MiddleLeft);
            HealthText = UiFactory.Label(canvas.transform, "Health", font, "", 23, Color.white, new Vector2(290, 52), new Vector2(0, 1), new Vector2(165, -47), TextAnchor.MiddleLeft);
            healthBar = UiFactory.Bar(canvas.transform, "HealthBar", new Vector2(290, 9), new Vector2(0, 1), new Vector2(165, -70), UiFactory.Mint);
            LevelText = UiFactory.Label(canvas.transform, "Level", font, "", 25, Color.white, new Vector2(320, 56), new Vector2(0.5f, 1), new Vector2(0, -30));
            experienceText = UiFactory.Label(canvas.transform, "Experience", font, "", 16, UiFactory.Muted, new Vector2(450, 25), new Vector2(0.5f, 1), new Vector2(0, -63));
            timeText = UiFactory.Label(canvas.transform, "Time", font, "", 29, Color.white, new Vector2(170, 60), new Vector2(1, 1), new Vector2(-255, -42));
            killsText = UiFactory.Label(canvas.transform, "Kills", font, "", 21, UiFactory.Mint, new Vector2(180, 60), new Vector2(1, 1), new Vector2(-96, -42));
            experienceBar = UiFactory.Bar(canvas.transform, "ExperienceBar", new Vector2(1280, 5), new Vector2(0.5f, 1), new Vector2(0, -94), new Color32(105, 174, 247, 255));
            canvas.gameObject.SetActive(false);
        }
        public void Show(bool visible, string health, double healthFraction, string level, string experience, double experienceFraction, string survival, string kills)
        {
            canvas.gameObject.SetActive(visible); if (!visible) return;
            HealthText.text = health; LevelText.text = level; experienceText.text = experience; timeText.text = survival; killsText.text = kills;
            UiFactory.Fill(healthBar, healthFraction); UiFactory.Fill(experienceBar, experienceFraction);
        }
    }
}
