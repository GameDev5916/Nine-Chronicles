﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lib9c.Model.Order;
using Nekoyume.BlockChain;
using Nekoyume.EnumType;
using Nekoyume.State;
using UnityEditor;
using UnityEngine;

namespace Planetarium.Nekoyume.Editor
{
    public static class DataExporter
    {
        [MenuItem("Tools/Export Data/Shop", true)]
        public static bool ExportShopDataValidation() => Application.isPlaying;

        [MenuItem("Tools/Export Data/Shop")]
        public static void ExportShopData()
        {
            var defaultFolderPath = Path.Combine(StorePath.GetPrefixPath(), "exported-data");
            if (!Directory.Exists(defaultFolderPath))
            {
                Directory.CreateDirectory(defaultFolderPath);
            }

            var path = EditorUtility.SaveFilePanel(
                "Select File",
                defaultFolderPath,
                $"shop-{DateTime.Now:yyyy-MM-dd-hh-mm-ss}",
                "csv");
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Error", "Invalid file path.", "Thanks");
                return;
            }

            var strings = new List<string>
            {
                "item_id,price",
                "_my_items,"
            };
            strings.AddRange(GetItemBaseArray(ReactiveShopState.SellDigest.Value));
            strings.AddRange(new[]
            {
                "_other_items,"
            });
            strings.AddRange(GetItemBaseArray(ReactiveShopState.BuyDigest.Value));
            File.WriteAllLines(path, strings, Encoding.UTF8);
            Debug.Log("Export store data finished");
        }

        private static IEnumerable<string> GetItemBaseArray(List<OrderDigest> source) => source?
                .Select(orderDigest => ReactiveShopState.TryGetShopItem(orderDigest, out var itemBase)
                    ? $"{itemBase.Id},{orderDigest.Price.GetQuantityString()}"
                    : string.Empty)
                .Where(stringData => !string.IsNullOrEmpty(stringData))
            ?? Array.Empty<string>();
    }
}
