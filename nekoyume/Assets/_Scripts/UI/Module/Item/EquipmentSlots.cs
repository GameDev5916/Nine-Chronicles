using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nekoyume.Battle;
using Nekoyume.Helper;
using Nekoyume.Model;
using Nekoyume.Model.Elemental;
using Nekoyume.Model.Item;
using Nekoyume.State;
using UnityEngine;

namespace Nekoyume.UI.Module
{
    /// <summary>
    /// 이 객체는 외부 UI에 의해서 장비의 장착이나 해제 상태가 변하고 있음.
    /// 외부 UI에서는 항상 인벤토리와 함께 사용하고 있어서 그에 따르는 중복 코드가 생길 여지가 큼.
    /// UI간 결합을 끊고 이벤트 기반으로 동작하게끔 수정하면 좋겠음.
    /// </summary>
    public class EquipmentSlots : MonoBehaviour, IEnumerable<EquipmentSlot>
    {
        private Action<EquipmentSlot> _onSlotClicked;
        private Action<EquipmentSlot> _onSlotDoubleClicked;

        [SerializeField]
        private EquipmentSlot[] slots = null;

        private void Awake()
        {
            if (slots is null)
            {
                throw new SerializeFieldNullException();
            }

            foreach (var slot in slots)
            {
                slot.Set(null, RaiseSlotClicked, RaiseSlotDoubleClicked);
            }
        }

        public bool TryGetSlot(ItemSubType itemSubType, out EquipmentSlot outSlot)
        {
            foreach (var slot in slots)
            {
                if (slot.ItemSubType != itemSubType)
                {
                    continue;
                }

                outSlot = slot;
                return true;
            }

            outSlot = default;
            return false;
        }

        public void SetPlayerCostumes(
            Player player,
            Action<EquipmentSlot> onClick,
            Action<EquipmentSlot> onDoubleClick)
        {
            Clear();

            if (player is null)
            {
                return;
            }

            _onSlotClicked = onClick;
            _onSlotDoubleClicked = onDoubleClick;

            UpdateSlots(player.Level);
            foreach (var costume in player.Costumes)
            {
                TryToEquip(costume);
            }
        }

        public void SetPlayerEquipments(
            Player player,
            Action<EquipmentSlot> onClick,
            Action<EquipmentSlot> onDoubleClick,
            List<ElementalType> elementalTypes = null)
        {
            Clear();

            if (player is null)
            {
                return;
            }

            _onSlotClicked = onClick;
            _onSlotDoubleClicked = onDoubleClick;

            UpdateSlots(player.Level);
            foreach (var equipment in player.Equipments)
            {
                TryToEquip(equipment);
            }
            UpdateDim(elementalTypes);
        }

        public void SetPlayerConsumables(int avatarLevel,
            Action<EquipmentSlot> onClick,
            Action<EquipmentSlot> onDoubleClick)
        {
            Clear();

            foreach (var slot in slots)
            {
                slot.Set(avatarLevel);
            }

            _onSlotClicked = onClick;
            _onSlotDoubleClicked = onDoubleClick;
        }

        public bool TryToEquip(Costume costume)
        {
            if (!TryGetToEquip(costume, out var slot))
            {
                return false;
            }

            slot.Set(costume, RaiseSlotClicked, RaiseSlotDoubleClicked);
            return true;
        }

        public bool TryToEquip(Equipment equipment)
        {
            if (!TryGetToEquip(equipment, out var slot))
            {
                return false;
            }

            slot.Set(equipment, RaiseSlotClicked, RaiseSlotDoubleClicked);
            return true;
        }

        /// <summary>
        /// `costume`을 장착하기 위한 슬롯을 반환한다.
        /// </summary>
        /// <param name="costume"></param>
        /// <param name="slot"></param>
        /// <returns></returns>
        public bool TryGetToEquip(Costume costume, out EquipmentSlot slot)
        {
            if (costume is null)
            {
                slot = null;
                return false;
            }

            var itemSubType = costume.ItemSubType;
            var typeSlots = slots
                .Where(e => !e.IsLock && e.ItemSubType == itemSubType)
                .ToList();
            slot = typeSlots.FirstOrDefault();
            return slot;
        }

        /// <summary>
        /// `equipment`를 장착하기 위한 슬롯을 반환한다.
        /// 반지의 경우 이미 장착되어 있는 슬롯이 있다면 이를 반환한다.
        /// </summary>
        /// <param name="equipment"></param>
        /// <param name="slot"></param>
        /// <param name="elementalTypeToIgnore"></param>
        /// <returns></returns>
        public bool TryGetToEquip(Equipment equipment, out EquipmentSlot slot, ElementalType? elementalTypeToIgnore = null)
        {
            if (equipment is null)
            {
                slot = null;
                return false;
            }

            var itemSubType = equipment.ItemSubType;
            var typeSlots = slots
                .Where(e => !e.IsLock && e.ItemSubType == itemSubType)
                .ToList();
            if (!typeSlots.Any())
            {
                slot = null;
                return false;
            }

            if (itemSubType == ItemSubType.Ring)
            {
                // Find the first slot which contains the same `non-fungible item`
                slot = typeSlots.FirstOrDefault(e =>
                            !e.IsEmpty &&
                            e.Item is INonFungibleItem nonFungibleItem &&
                            nonFungibleItem.NonFungibleId.Equals(equipment.NonFungibleId))
                        // Find the first empty slot.
                        ?? typeSlots.FirstOrDefault(e => e.IsEmpty)
                        // Find the first slot of `ElementalType` that is different from `elementalTypeToIgnore`.
                        ?? (elementalTypeToIgnore != null
                            ? typeSlots.FirstOrDefault(e =>
                                !e.Item.ElementalType.Equals(elementalTypeToIgnore))
                            : null)
                        // Find the first slot with the lowest 'CP'.
                        ?? typeSlots.OrderBy(e => CPHelper.GetCP((ItemUsable) e.Item))
                            .First();
            }
            else
            {
                slot = typeSlots.First();
            }

            return true;
        }

        /// <summary>
        /// `equipment`가 이미 장착되어 있는 슬롯을 반환한다.
        /// </summary>
        /// <param name="itemBase"></param>
        /// <param name="slot"></param>
        /// <returns></returns>
        public bool TryGetAlreadyEquip(ItemBase itemBase, out EquipmentSlot slot)
        {
            if (itemBase is null)
            {
                slot = null;
                return false;
            }

            slot = slots.FirstOrDefault(e =>
                !e.IsLock &&
                !e.IsEmpty &&
                ((INonFungibleItem) e.Item).NonFungibleId
                .Equals(((INonFungibleItem) itemBase).NonFungibleId) &&
                e.Item.Equals(itemBase));
            return slot;
        }

        /// <summary>
        /// 모든 슬롯을 해제한다.
        /// </summary>
        public void Clear()
        {
            foreach (var slot in slots)
            {
                slot.Clear();
            }
        }

        #region IEnumerable<EquipmentSlot>

        public IEnumerator<EquipmentSlot> GetEnumerator()
        {
            return slots.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        private void UpdateSlots(int avatarLevel)
        {
            foreach (var slot in slots)
            {
                slot.Set(avatarLevel);
            }
        }

        private void UpdateDim(List<ElementalType> elementalTypes)
        {
            if (elementalTypes is null)
            {
                return;
            }

            foreach (var slot in slots)
            {
                if (slot.Item == null)
                {
                    slot.SetDim(false);
                    continue;
                }

                slot.SetDim(!elementalTypes.Exists(x => x.Equals(slot.Item.ElementalType)));
            }
        }

        private void RaiseSlotClicked(EquipmentSlot slot)
        {
            _onSlotClicked?.Invoke(slot);
        }

        private void RaiseSlotDoubleClicked(EquipmentSlot slot)
        {
            _onSlotDoubleClicked?.Invoke(slot);
        }
    }
}
