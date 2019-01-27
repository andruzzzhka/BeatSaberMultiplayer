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
            HashSet<int> rows = new HashSet<int>(tableView.GetPrivateField<HashSet<int>>("_selectedRows"));
            float scrollPosition = tableView.GetPrivateField<ScrollRect>("_scrollRect").verticalNormalizedPosition;

            tableView.ReloadData();

            tableView.GetPrivateField<ScrollRect>("_scrollRect").verticalNormalizedPosition = scrollPosition;
            tableView.SetPrivateField("_targetVerticalNormalizedPosition", scrollPosition);
            if (rows.Count > 0)
                tableView.SelectRow(rows.First(), callbackTable);
        }
    }
}