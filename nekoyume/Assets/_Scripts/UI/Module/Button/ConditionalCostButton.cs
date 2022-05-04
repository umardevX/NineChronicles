using Nekoyume.EnumType;
using Nekoyume.Game.Controller;
using Nekoyume.Helper;
using Nekoyume.L10n;
using Nekoyume.Model.Mail;
using Nekoyume.State;
using Nekoyume.UI.Scroller;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Nekoyume.UI.Module
{
    public class ConditionalCostButton : ConditionalButton
    {
        public struct CostParam
        {
            public CostType type;
            public int cost;
            public CostParam(CostType type, int cost)
            {
                this.type = type;
                this.cost = cost;
            }
        }

        [Serializable]
        private struct CostObject
        {
            public CostType type;
            public List<CostText> costTexts;
        }

        [Serializable]
        private struct CostText
        {
            public GameObject parent;
            public TMP_Text text;
        }

        public enum CostType
        {
            None,
            NCG,
            ActionPoint,
            Hourglass,
            Crystal
        }

        [SerializeField]
        private bool showCostAlert = true;

        [SerializeField]
        private List<CostObject> costObjects = null;

        [SerializeField]
        private List<GameObject> costParents = null;
        
        private readonly Dictionary<CostType, int> _costMap = new Dictionary<CostType, int>();

        public void SetCost(params CostParam[] costs)
        {
            _costMap.Clear();
            foreach (var cost in costs)
            {
                if (cost.cost > 0)
                {
                    _costMap[cost.type] = cost.cost;
                }
            }
            UpdateObjects();
        }

        public void SetCost(CostType type, int cost)
        {
            _costMap.Clear();
            if (cost > 0)
            {
                _costMap[type] = cost;
            }
            UpdateObjects();
        }

        public override void UpdateObjects()
        {
            base.UpdateObjects();

            var showCost = _costMap.Count > 0;
            foreach (var parent in costParents)
            {
                parent.SetActive(showCost);
            }

            foreach (var costObject in costObjects)
            {
                var exist = _costMap.ContainsKey(costObject.type);
                foreach (var costText in costObject.costTexts)
                {
                    costText.parent.SetActive(exist);
                }
                if (exist)
                {
                    foreach (var costText in costObject.costTexts)
                    {
                        var cost = _costMap[costObject.type];
                        costText.text.text = cost.ToString();
                        costText.text.color = CheckCostOfType(costObject.type, cost) ?
                            Palette.GetColor(ColorType.ButtonEnabled) :
                            Palette.GetColor(ColorType.ButtonDisabled);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if costs are enough to pay for current avatar.
        /// </summary>
        /// <returns>
        /// Type of cost that is not enough to pay. If <see cref="CostType.None"/> is returned, costs are enough to pay.
        /// </returns>
        protected CostType CheckCost()
        {
            foreach (var pair in _costMap)
            {
                var type = pair.Key;
                var cost = pair.Value;

                switch (type)
                {
                    case CostType.NCG:
                        if (States.Instance.GoldBalanceState.Gold.MajorUnit < cost)
                        {
                            return CostType.NCG;
                        }
                        break;
                    case CostType.Crystal:
                        if (ReactiveCrystalState.CrystalBalance.MajorUnit < cost)
                        {
                            return CostType.Crystal;
                        }
                        break;
                    case CostType.ActionPoint:
                        if (States.Instance.CurrentAvatarState.actionPoint < cost)
                        {
                            return CostType.ActionPoint;
                        }
                        break;
                    case CostType.Hourglass:
                        var inventory = States.Instance.CurrentAvatarState.inventory;
                        var count = Util.GetHourglassCount(inventory, Game.Game.instance.Agent.BlockIndex);
                        if (count < cost)
                        {
                            return CostType.Hourglass;
                        }
                        break;
                    default:
                        break;
                }
            }

            return CostType.None;
        }

        protected bool CheckCostOfType(CostType type, int cost)
        {
            switch (type)
            {
                case CostType.NCG:
                    return States.Instance.GoldBalanceState.Gold.MajorUnit >= cost;
                case CostType.Crystal:
                    return ReactiveCrystalState.CrystalBalance.MajorUnit >= cost;
                case CostType.ActionPoint:
                    return States.Instance.CurrentAvatarState.actionPoint >= cost;
                case CostType.Hourglass:
                    var inventory = States.Instance.CurrentAvatarState.inventory;
                    var count = Util.GetHourglassCount(inventory, Game.Game.instance.Agent.BlockIndex);
                    return count >= cost;
                default:
                    return true;
            }
        }

        protected override bool CheckCondition()
        {
            return CheckCost() == CostType.None
                && base.CheckCondition();
        }

        protected override void OnClickButton()
        {
            base.OnClickButton();

            if (!showCostAlert && CurrentState.Value != State.Conditional)
            {
                return;
            }

            switch (CheckCost())
            {
                case CostType.None:
                    break;
                case CostType.NCG:
                    OneLineSystem.Push(
                        MailType.System,
                        L10nManager.Localize("UI_NOT_ENOUGH_NCG"),
                        NotificationCell.NotificationType.Alert);
                    return;
                case CostType.Crystal:
                    OneLineSystem.Push(
                        MailType.System,
                        L10nManager.Localize("UI_NOT_ENOUGH_CRYSTAL"),
                        NotificationCell.NotificationType.Alert);
                    return;
                case CostType.ActionPoint:
                    OneLineSystem.Push(
                        MailType.System,
                        L10nManager.Localize("ERROR_ACTION_POINT"),
                        NotificationCell.NotificationType.Alert);
                    return;
                case CostType.Hourglass:
                    OneLineSystem.Push(
                        MailType.System,
                        L10nManager.Localize("UI_NOT_ENOUGH_HOURGLASS"),
                        NotificationCell.NotificationType.Alert);
                    return;
                default:
                    break;
            }
        }
    }
}
