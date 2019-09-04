using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (rows.Count > 0)
                tableView.SelectCellWithIdx(rows.First(), callbackTable);
        }
    }
}