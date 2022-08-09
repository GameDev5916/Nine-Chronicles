using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nekoyume.TableData;
using Nekoyume.UI.Model;
using Nekoyume.Model.Item;

namespace Nekoyume.UI.Module
{
    public class RequiredItemRecipeView : MonoBehaviour
    {
        [SerializeField] private RequiredItemView[] requiredItemViews = null;

        [SerializeField] private GameObject plusImage = null;

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetData(
            EquipmentItemSubRecipeSheet.MaterialInfo baseMaterialInfo,
            List<EquipmentItemSubRecipeSheet.MaterialInfo> materials,
            bool checkInventory
            )
        {
            requiredItemViews[0].gameObject.SetActive(true);
            SetView(requiredItemViews[0], baseMaterialInfo.Id, baseMaterialInfo.Count, checkInventory);
            plusImage.SetActive(materials.Any());

            if (materials != null)
            {
                for (int i = 1; i < requiredItemViews.Length; ++i)
                {
                    var itemView = requiredItemViews[i];
                    if (i - 1 >= materials.Count)
                    {
                        itemView.gameObject.SetActive(false);
                    }
                    else
                    {
                        var material = materials[i - 1];
                        SetView(itemView, material.Id, material.Count, checkInventory);
                        itemView.gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                for (int i = 1; i < requiredItemViews.Length; ++i)
                {
                    requiredItemViews[i].gameObject.SetActive(false);
                }
            }

            Show();
        }

        public void SetData(
            List<EquipmentItemSubRecipeSheet.MaterialInfo> materials,
            bool checkInventory
            )
        {
            for (int i = 0; i < requiredItemViews.Length; ++i)
            {
                var itemView = requiredItemViews[i];
                if (i >= materials.Count)
                {
                    itemView.gameObject.SetActive(false);
                }
                else
                {
                    var material = materials[i];
                    SetView(itemView, material.Id, material.Count, checkInventory);
                    itemView.gameObject.SetActive(true);
                }
            }

            Show();
        }

        private void SetView(
            RequiredItemView view,
            int materialId,
            int requiredCount,
            bool checkInventory
            )
        {
            var material = ItemFactory.CreateMaterial(Game.Game.instance.TableSheets.MaterialItemSheet, materialId);
            var itemCount = requiredCount;
            if (checkInventory)
            {
                var inventory = Game.Game.instance.States.CurrentAvatarState.inventory;
                itemCount = inventory.TryGetFungibleItems(material.FungibleId, out var outFungibleItems)
                    ? outFungibleItems.Sum(e => e.count)
                    : 0;
            }
            var countableItem = new CountableItem(material, itemCount);
            view.SetData(countableItem, requiredCount);
            if (!checkInventory)
            {
                view.SetRequiredText();
            }
        }
    }
}
