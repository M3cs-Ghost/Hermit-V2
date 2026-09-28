using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Economy;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C9.1: the Hermit Coin reward breakdown shown on Clásico's Results
    /// screen, beside the normal results card — never during rounds. It
    /// only DISPLAYS a <see cref="HermitRewardOutcome"/>; it never calculates
    /// or credits anything. Same restrained Gold "plaque" language as the
    /// Game Show prize/explanation cards (dark fill, gold edge, Marcellus),
    /// a single calm count-up on the total — no coin showers, no casino
    /// sounds. Every reduction is visible: the diminishing multiplier gets
    /// its own "Práctica adicional ×0.60" line.
    /// </summary>
    internal sealed class HermitRewardSummaryPanel
    {
        private const int MaxLines = 8;
        private const float LineStartY = -96f;
        private const float LineSpacing = 29f;
        private const float CountUpSeconds = 0.6f;

        private static readonly Color PlaqueFill = new Color(0.07f, 0.05f, 0.10f, 0.95f);
        private static readonly Color PlaqueBorder = new Color(0.78f, 0.60f, 0.30f, 1f);
        private static readonly Color Gold = new Color(0.97f, 0.80f, 0.42f, 1f);
        private static readonly Color Muted = new Color(0.80f, 0.76f, 0.70f, 1f);

        private readonly MonoBehaviour _host;
        private RectTransform _panel;
        private TMP_Text _subtitle;
        private TMP_Text _note;
        private readonly List<(RectTransform row, TMP_Text label, TMP_Text value)> _lines = new List<(RectTransform, TMP_Text, TMP_Text)>();
        private RectTransform _divider;
        private RectTransform _totalRow;
        private TMP_Text _totalValue;
        private TMP_Text _balanceValue;
        private Coroutine _countUp;

        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public HermitRewardSummaryPanel(MonoBehaviour host)
        {
            _host = host;
        }

        public RectTransform Root => _panel;

        public void Build(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            var (panel, content) = RuntimeUIFactory.CreatePremiumPanel(parent, "HermitRewardPanel", PlaqueFill, PlaqueBorder, 3f, 16);
            _panel = panel;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = anchoredPosition;
            _panel.sizeDelta = size;
            _panel.GetComponent<Image>().raycastTarget = false;

            var width = size.x - 40f;
            RuntimeUIFactory.CreatePremiumText(
                content, "RewardTitle", "HERMIT COINS", 22f, Gold, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(width, 30f),
                fontStyle: FontStyles.Bold, characterSpacing: 8f);

            _subtitle = RuntimeUIFactory.CreatePremiumText(
                content, "RewardSubtitle", string.Empty, 14f, Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(width, 22f));

            for (var i = 0; i < MaxLines; i++)
            {
                var rowGo = new GameObject($"RewardLine{i}", typeof(RectTransform));
                var row = (RectTransform)rowGo.transform;
                row.SetParent(content, false);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, LineStartY - i * LineSpacing);
                row.sizeDelta = new Vector2(width, 26f);

                var label = RuntimeUIFactory.CreatePremiumText(
                    row, "Label", string.Empty, 17f, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft,
                    Vector2.zero, new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
                var value = RuntimeUIFactory.CreatePremiumText(
                    row, "Value", string.Empty, 17f, Gold, TextAlignmentOptions.MidlineRight,
                    new Vector2(0.72f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
                _lines.Add((row, label, value));
            }

            _note = RuntimeUIFactory.CreatePremiumText(
                content, "RewardNote", string.Empty, 16f, Theme.TextPrimary, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(width, 96f),
                lineSpacing: 2f);

            _divider = RuntimeUIFactory.CreatePanel(content, "RewardDivider", new Color(PlaqueBorder.r, PlaqueBorder.g, PlaqueBorder.b, 0.55f));
            _divider.anchorMin = _divider.anchorMax = new Vector2(0.5f, 0f);
            _divider.anchoredPosition = new Vector2(0f, 118f);
            _divider.sizeDelta = new Vector2(width, 1.5f);
            _divider.GetComponent<Image>().raycastTarget = false;

            _totalRow = BuildTotalRow(content, "RewardTotal", "GANASTE", 20f, 30f, new Vector2(0f, 84f), width, out _totalValue);
            BuildTotalRow(content, "RewardBalance", "SALDO", 15f, 19f, new Vector2(0f, 40f), width, out _balanceValue);

            _panel.gameObject.SetActive(false);
        }

        private static RectTransform BuildTotalRow(Transform parent, string name, string caption, float captionSize, float valueSize, Vector2 position, float width, out TMP_Text value)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(parent, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.anchoredPosition = position;
            row.sizeDelta = new Vector2(width, 40f);

            var label = RuntimeUIFactory.CreatePremiumText(
                row, "Label", caption, captionSize, Muted, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, characterSpacing: 6f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            value = RuntimeUIFactory.CreatePremiumText(
                row, $"{name}Value", string.Empty, valueSize, Gold, TextAlignmentOptions.MidlineRight,
                new Vector2(0.5f, 0f), Vector2.one, Vector2.zero, Vector2.zero, fontStyle: FontStyles.Bold);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
            return row;
        }

        public void Hide()
        {
            StopCountUp();
            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
        }

        public void Show(HermitRewardOutcome outcome)
        {
            StopCountUp();
            ClearLines();
            _note.text = string.Empty;
            _panel.gameObject.SetActive(true);

            var reward = outcome?.Reward;
            var balance = outcome?.BalanceAfter ?? 0;

            if (reward == null || !reward.WasValidSession)
            {
                _subtitle.text = outcome != null && outcome.AlreadyCredited ? "Recompensa ya acreditada" : "Sin Hermit Coins esta vez";
                _note.text = reward == null ? string.Empty : ExplainInvalid(reward);
                SetTotalsVisible(false);
                _balanceValue.text = FormatHc(balance);
                return;
            }

            _subtitle.text = outcome.AlreadyCredited
                ? "Recompensa ya acreditada"
                : $"Sesión {reward.DailySessionNumber} de hoy";

            var line = 0;
            SetLine(line++, "Participación", Signed(reward.BaseReward));
            SetLine(line++, $"Precisión  {reward.CorrectAnswers}/{reward.TotalRounds}", Signed(reward.AccuracyBonus));
            SetLine(line++, "Velocidad", Signed(reward.SpeedBonus));
            if (reward.DiminishingMultiplier < 0.999f)
            {
                SetLine(line++, "Práctica adicional", "×" + reward.DiminishingMultiplier.ToString("0.00", CultureInfo.InvariantCulture));
            }

            if (reward.FirstSessionBonus > 0)
            {
                SetLine(line++, "Primera sesión del día", Signed(reward.FirstSessionBonus));
            }

            if (reward.VarietyBonus > 0)
            {
                SetLine(line++, "Variedad del día", Signed(reward.VarietyBonus));
            }

            if (reward.WeeklyConsistencyBonus > 0)
            {
                SetLine(line, "Constancia semanal", Signed(reward.WeeklyConsistencyBonus));
            }

            SetTotalsVisible(true);
            var total = reward.TotalReward;
            if (outcome.Credited && total > 0 && _host != null && _host.isActiveAndEnabled)
            {
                _countUp = _host.StartCoroutine(CountUpRoutine(total, balance - total, balance));
            }
            else
            {
                _totalValue.text = FormatHc(total);
                _balanceValue.text = FormatHc(balance);
            }
        }

        private IEnumerator CountUpRoutine(int total, int balanceFrom, int balanceTo)
        {
            var elapsed = 0f;
            while (elapsed < CountUpSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / CountUpSeconds);
                var eased = 1f - (1f - t) * (1f - t);
                _totalValue.text = FormatHc(Mathf.RoundToInt(total * eased));
                _balanceValue.text = FormatHc(Mathf.RoundToInt(Mathf.Lerp(balanceFrom, balanceTo, eased)));
                yield return null;
            }

            _totalValue.text = FormatHc(total);
            _balanceValue.text = FormatHc(balanceTo);
            _countUp = null;
        }

        private void StopCountUp()
        {
            if (_countUp != null && _host != null)
            {
                _host.StopCoroutine(_countUp);
            }

            _countUp = null;
        }

        private void SetTotalsVisible(bool visible)
        {
            _divider.gameObject.SetActive(visible);
            _totalRow.gameObject.SetActive(visible);
        }

        private void ClearLines()
        {
            foreach (var (row, label, value) in _lines)
            {
                label.text = string.Empty;
                value.text = string.Empty;
                row.gameObject.SetActive(false);
            }
        }

        private void SetLine(int index, string label, string value)
        {
            if (index < 0 || index >= _lines.Count)
            {
                return;
            }

            var line = _lines[index];
            line.label.text = label;
            line.value.text = value;
            line.row.gameObject.SetActive(true);
        }

        private static string ExplainInvalid(HermitRewardResult reward)
        {
            switch (reward.InvalidReason)
            {
                case HermitInvalidSessionReason.Aborted:
                    return "Sesión abandonada.\nCompleta la sesión para ganar Hermit Coins.";
                case HermitInvalidSessionReason.IncompleteRounds:
                    return "Sesión incompleta.\nCompleta todas las rondas para ganar Hermit Coins.";
                case HermitInvalidSessionReason.InsufficientInteraction:
                    var needed = (reward.TotalRounds * 2 + 2) / 3;
                    return $"Respondiste {reward.InteractedRounds} de {reward.TotalRounds} rondas.\nResponde al menos {needed} para ganar Hermit Coins.";
                default:
                    return string.Empty;
            }
        }

        private static string Signed(int amount) => $"+{amount} HC";

        private static string FormatHc(int amount) => amount.ToString("#,0", CultureInfo.InvariantCulture) + " HC";
    }
}
