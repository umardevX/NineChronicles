﻿using System.Linq;
using Nekoyume.Game;
using Nekoyume.Model.Item;
using Nekoyume.State;
using Nekoyume.UI.Scroller;
using UnityEngine;

namespace Nekoyume.UI
{
    public class OutfitInfoListPopup : PopupWidget
    {
        [SerializeField]
        private RandomOutfitScroll scroll;

        public void Show(ItemSubType subType, bool ignoreShowAnimation = false)
        {
            var rows = TableSheets.Instance.CustomEquipmentCraftIconSheet.Values.Where(row =>
                row.RequiredRelationship <= ReactiveAvatarState.Relationship &&
                row.ItemSubType == subType).ToList();
            var sumRatio = rows.Sum(row => (float)row.Ratio);
            scroll.UpdateData(rows.Select(
                row => new RandomOutfitCell.Model(row.IconId, $"{row.Ratio / sumRatio * 100f}%")));
            base.Show(ignoreShowAnimation);
        }
    }
}
