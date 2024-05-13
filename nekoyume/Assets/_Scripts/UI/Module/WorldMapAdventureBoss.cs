using CommandLine;
using Cysharp.Threading.Tasks;
using Nekoyume.L10n;
using Nekoyume.Model.AdventureBoss;
using Nekoyume.Model.Mail;
using Nekoyume.UI.Scroller;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using System;

namespace Nekoyume.UI.Module
{
    using UniRx;
    public class WorldMapAdventureBoss : MonoBehaviour
    {
        [SerializeField] private GameObject Open;
        [SerializeField] private GameObject WantedOpen;
        [SerializeField] private GameObject WantedClose;
        [SerializeField] private GameObject Close;

        [SerializeField] private TextMeshProUGUI[] RemainingBlockIndexs;
        [SerializeField] private TextMeshProUGUI UsedNCG;
        [SerializeField] private TextMeshProUGUI Floor;

        private readonly List<System.IDisposable> _disposables = new();
        private long _remainingBlockIndex = 0;

        private void OnEnable()
        {
            Game.Game.instance.Agent.BlockIndexSubject
                .Subscribe(UpdateViewAsync)
                .AddTo(_disposables);

            Game.Game.instance.AdventureBossData.SeasonInfo.Subscribe(OnSeasonInfoChanged).AddTo(_disposables);
            Game.Game.instance.AdventureBossData.BountyBoard.Subscribe(OnBountyBoardChanged).AddTo(_disposables);
            Game.Game.instance.AdventureBossData.ExploreInfo.Subscribe(OnExploreInfoChanged).AddTo(_disposables);
        }

        private void OnDisable()
        {
            _disposables.DisposeAllAndClear();
        }

        private void UpdateViewAsync(long blockIndex)
        {
            var seasonInfo = Game.Game.instance.AdventureBossData.SeasonInfo.Value;
            if (seasonInfo == null)
            {
                foreach (var text in RemainingBlockIndexs)
                {
                    text.text = "";
                }
                return;
            }
            _remainingBlockIndex =  seasonInfo.EndBlockIndex - blockIndex;
            var timeText = $"{_remainingBlockIndex:#,0}({_remainingBlockIndex.BlockRangeToTimeSpanString()})";
            foreach (var text in RemainingBlockIndexs)
            {
                text.text = timeText;
            }
        }

        public void OnClickOpenEnterBountyPopup()
        {
            Widget.Find<AdventureBossEnterBountyPopup>().Show();
        }

        public void OnClickOpenAdventureBoss()
        {
            Widget.Find<LoadingScreen>().Show();
            try
            {
                Game.Game.instance.AdventureBossData.RefreshAllByCurrentState().ContinueWith(() =>
                {
                    Widget.Find<LoadingScreen>().Close();
                    Widget.Find<AdventureBoss>().Show();
                });
            }
            catch (System.Exception e)
            {
                NcDebug.LogError(e);
                Widget.Find<LoadingScreen>().Close();
            }
        }

        public void OnClickAdventureSeasonAlert()
        {
            var remaingTimespan = _remainingBlockIndex.BlockToTimeSpan();
            OneLineSystem.Push(MailType.System, L10nManager.Localize("NOTIFICATION_ADVENTURE_BOSS_REMAINIG_TIME", remaingTimespan.Hours, remaingTimespan.Minutes%60), NotificationCell.NotificationType.Notification);
        }

        private void OnSeasonInfoChanged(SeasonInfo info)
        {
            if (info == null)
            {
                Close.SetActive(true);
                Open.SetActive(false);
                return ;
            }

            if (info.EndBlockIndex < Game.Game.instance.Agent.BlockIndex)
            {
                Open.SetActive(false);
                Close.SetActive(true);
                return;
            }
            Open.SetActive(true);
            Close.SetActive(false);

            UsedNCG.text = info.UsedNcg.ToCurrencyNotation();

            return;
        }

        private void OnBountyBoardChanged(BountyBoard bountyBoard)
        {
            if(bountyBoard.Investors != null && bountyBoard.Investors.Count() > 0)
            {
                WantedOpen.SetActive(true);
                WantedClose.SetActive(false);
            }
            else
            {
                WantedOpen.SetActive(false);
                WantedClose.SetActive(true);
            }
        }

        private void OnExploreInfoChanged(ExploreInfo info)
        {
            if (info != null)
            {
                Floor.text = info.Floor.ToString();
            }
        }
    }
}
