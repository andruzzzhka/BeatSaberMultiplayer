using BeatSaberMultiplayer.Data;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer.UI.UIElements
{
    class DownloadStateTableCell : LeaderboardTableCell
    {
        public float progress
        {
            set
            {
                if (value < 1f)
                {
                    _scoreText.text = value.ToString("P");
                }
                else
                {
                    _scoreText.text = "DOWNLOADED";
                }
            }
        }

        public new int rank
        {
            set
            {
                if(value <= 0)
                {
                    _rankText.text = "";
                }
                else
                {
                    _rankText.text = value.ToString();
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
        }

        public void Init()
        {
            LeaderboardTableCell cell = GetComponent<LeaderboardTableCell>();

            foreach (FieldInfo info in cell.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(this, info.GetValue(cell));
            }

            Destroy(cell);

            reuseIdentifier = "DownloadCell";
            
        }

    }
}
