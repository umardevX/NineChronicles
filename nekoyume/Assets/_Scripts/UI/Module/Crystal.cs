using System;
using Libplanet.Assets;
using Nekoyume.State;
using Nekoyume.UI.Module.Common;
using TMPro;
using UnityEngine;

namespace Nekoyume.UI.Module
{
    using UniRx;

    public class Crystal : AlphaAnimateModule
    {
        [SerializeField]
        private TextMeshProUGUI text = null;

        private IDisposable _disposable;

        [SerializeField]
        private GameObject loadingObject;

        public bool NowCharging => loadingObject.activeSelf;

        protected override void OnEnable()
        {
            base.OnEnable();
            _disposable = ReactiveAvatarState.Crystal.Subscribe(SetCrystal);
            UpdateCrystalAsync();
        }

        protected override void OnDisable()
        {
            _disposable.Dispose();
            base.OnDisable();
        }

        public void SetProgressCircle(bool isVisible)
        {
            loadingObject.SetActive(isVisible);
            text.enabled = !isVisible;
        }

        private async void UpdateCrystalAsync()
        {
            var currentAvatarState = States.Instance.CurrentAvatarState;
            if (currentAvatarState is null ||
                ReactiveAvatarState.Crystal is null)
            {
                return;
            }

            SetProgressCircle(true);
            var address = currentAvatarState.address;
            var currency = new Currency("CRYSTAL", 18, minters: null);
            var task = Game.Game.instance.Agent.GetBalanceAsync(address, currency);
            await task;

            SetCrystal(task.Result);
            SetProgressCircle(false);
        }

        private void SetCrystal(FungibleAssetValue crystal)
        {
            text.text = crystal.GetQuantityString();
        }
    }
}
