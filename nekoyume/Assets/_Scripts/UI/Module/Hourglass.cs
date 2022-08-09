﻿using System;
using System.Collections.Generic;
using Nekoyume.Helper;
using Nekoyume.State;
using Nekoyume.State.Subjects;
using Nekoyume.UI.Module.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nekoyume.UI.Module
{
    using UniRx;

    public class Hourglass : AlphaAnimateModule
    {
        [SerializeField]
        private TextMeshProUGUI countText = null;

        [SerializeField]
        private RectTransform tooltipArea = null;

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        protected override void OnEnable()
        {
            base.OnEnable();
            ReactiveAvatarState.Inventory?.Subscribe(UpdateHourglass).AddTo(_disposables);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _disposables.DisposeAllAndClear();
        }

        private void UpdateHourglass(Nekoyume.Model.Item.Inventory inventory)
        {
            var count = Util.GetHourglassCount(inventory, Game.Game.instance.Agent.BlockIndex);
            countText.text = count.ToString();
        }

        public void ShowTooltip()
        {
            Widget.Find<VanilaTooltip>()
                .Show("ITEM_NAME_400000", "UI_HOURGLASS_DESCRIPTION", tooltipArea.position);
        }

        public void HideTooltip()
        {
            Widget.Find<VanilaTooltip>().Close();
        }
    }
}
