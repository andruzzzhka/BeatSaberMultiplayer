using BS_Utils.Utilities;
using HMUI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.Misc
{
    public static class TableViewHelper
    {
        public static void RefreshTable(this TableView tableView, bool callbackTable = true)
        {
            HashSet<int> rows = new HashSet<int>(tableView.GetPrivateField<HashSet<int>>("_selectedCellIdxs"));
            float scrollPosition = tableView.GetComponent<ScrollRect>().verticalNormalizedPosition;

            tableView.ReloadData();

            tableView.GetComponent<ScrollRect>().verticalNormalizedPosition = scrollPosition;
            tableView.GetPrivateField<TableViewScroller>("_scroller").SetPrivateField("_targetPosition", scrollPosition);
            if (rows.Count > 0 && tableView.selectionType != TableViewSelectionType.None)
                tableView.SelectCellWithIdx(rows.First(), callbackTable);
        }

        public static void RemoveReusableCells(this TableView tableView, string id)
        {
            var reusableCells = tableView.GetPrivateField<Dictionary<string, List<TableCell>>>("_reusableCells");

            if (reusableCells.TryGetValue(id, out var list))
            {
                foreach (var cell in list)
                {
                    if (cell != null)
                    {
                        GameObject.Destroy(cell);
                    }
                }
                list.Clear();
            }
        }
    }
}