﻿using Nekoyume.Helper;
using Nekoyume.L10n;
using Nekoyume.TableData;
using UnityEngine;

namespace Nekoyume
{
    public static class BuffSheetRowExtension
    {
        public static string GetLocalizedName(this BuffSheet.Row row)
        {
            return L10nManager.Localize($"BUFF_NAME_{row.Id}");
        }

        public static string GetLocalizedDescription(this BuffSheet.Row row)
        {
            var desc = L10nManager.Localize($"BUFF_DESCRIPTION_{row.Id}");
            return string.Format(desc, row.StatModifier.Value);
        }

        public static Sprite GetIcon(this BuffSheet.Row row)
        {
            return SpriteHelper.GetBuffIcon(row.IconResource);
        }
    }
}
