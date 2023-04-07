using System;
using System.Globalization;
using Nekoyume.Model.Stat;
using TMPro;
using UnityEngine;

namespace Nekoyume.UI.Module
{
    public class ComparisonStatView : StatView
    {
        public TextMeshProUGUI afterValueText;

        public void Show(StatType statType, decimal statValue, decimal afterStatValue)
        {
            afterValueText.text
                = statType.ValueToString(afterStatValue);
            Show(statType, statValue);
        }

        public void Show(string keyText, decimal statValue, decimal afterStatValue)
        {
            if (!Enum.TryParse<StatType>(keyText, out var statType))
            {
                Debug.LogError("Failed to parse StatType.");
                return;
            }

            Show(statType, statValue, afterStatValue);
        }
    }
}
