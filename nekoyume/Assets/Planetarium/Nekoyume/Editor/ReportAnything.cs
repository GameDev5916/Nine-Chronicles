﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lib9c.Model.Order;
using Nekoyume.Extensions;
using Nekoyume.Game;
using Nekoyume.Model.Item;
using Nekoyume.State;
using UnityEditor;
using UnityEngine;

namespace Planetarium.Nekoyume.Editor
{
    public static class ReportAnything
    {
        [MenuItem("Tools/Report Anything/Check the shop items is made with mimisbrunnr recipe", true)]
        public static bool ShopItemCheckMimisbrunnrReportValidation() =>
            Application.isPlaying &&
            Game.instance is { };

        [MenuItem("Tools/Report Anything/Check the shop items is made with mimisbrunnr recipe")]
        public static void ShopItemCheckMimisbrunnrReport()
        {
            Debug.Log("[Report Anything] Start report: \"Check the shop items is made with mimisbrunnr recipe\"");
            var tableSheets = TableSheets.Instance;
            var recipeSheet = tableSheets.EquipmentItemRecipeSheet;
            var subRecipeSheet = tableSheets.EquipmentItemSubRecipeSheetV2;
            var optionSheet = tableSheets.EquipmentItemOptionSheet;
            var sellEquipments = GetItemBaseArray(ReactiveShopState.SellDigest.Value).ToArray();
            var mimisCount = 0;
            foreach (var equipment in sellEquipments)
            {
                if (equipment.IsMadeWithMimisbrunnrRecipe(recipeSheet, subRecipeSheet, optionSheet))
                {
                    mimisCount++;
                }
            }

            Debug.Log($"[Report Anything] Made with mimisbrunnr recipe items in sell: {mimisCount}/{sellEquipments.Length}");

            var buyEquipments = GetItemBaseArray(ReactiveShopState.BuyDigest.Value).ToArray();
            mimisCount = 0;
            foreach (var equipment in buyEquipments)
            {
                if (equipment.IsMadeWithMimisbrunnrRecipe(recipeSheet, subRecipeSheet, optionSheet))
                {
                    mimisCount++;
                }
            }

            Debug.Log($"[Report Anything] Made with mimisbrunnr recipe items in buy: {mimisCount}/{buyEquipments.Length}");
            Debug.Log("[Report Anything] End report");
        }

        public static IEnumerable<Equipment> GetItemBaseArray(IEnumerable<OrderDigest> source) => source?
                .Select(orderDigest =>
                    ReactiveShopState.TryGetShopItem(orderDigest, out var itemBase) &&
                    itemBase is Equipment equipment
                        ? equipment
                        : null)
                .Where(equipment => equipment is { })
            ?? Array.Empty<Equipment>();
    }
}
