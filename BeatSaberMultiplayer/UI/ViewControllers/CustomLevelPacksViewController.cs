using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers
{
    class CustomLevelPacksViewController : VRUIViewController
    {
        public event Action<CustomLevelPacksViewController, IBeatmapLevelPack> didSelectPackEvent;
        public int _selectedPackNum;
        protected IBeatmapLevelPackCollection _levelPackCollection;
        protected AdditionalContentModelSO _additionalContentModel;
        protected LevelPacksTableView _levelPacksTableView;
        public TableView tableView;
        private Button PageLeftButton;
        private Button PageRightButton;

        protected override void DidActivate(bool firstActivation, VRUIViewController.ActivationType activationType)
        {
            if (firstActivation && activationType == VRUIViewController.ActivationType.AddedToHierarchy)
            {
                _additionalContentModel = Resources.FindObjectsOfTypeAll<AdditionalContentModelSO>().FirstOrDefault();
                RectTransform container = new GameObject("HorizontalListContainer", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.sizeDelta = Vector2.zero;
                container.anchorMin = new Vector2(0.1f, 0); //Squish the list container a little bit
                container.anchorMax = new Vector2(0.9f, 1); //To make room for the forward/backward buttons

                var tableGameObject = new GameObject("CustomTableView");
                tableGameObject.SetActive(false);
                tableView = tableGameObject.AddComponent<TableView>();
                tableView.gameObject.AddComponent<RectMask2D>();
                tableView.transform.SetParent(container, false);
                tableView.SetPrivateField("_isInitialized", false);
                tableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                tableView.SetPrivateField("_tableType", TableView.TableType.Horizontal);

                (tableView.transform as RectTransform).anchorMin = Vector2.zero;
                (tableView.transform as RectTransform).anchorMax = Vector2.one;
                (tableView.transform as RectTransform).sizeDelta = Vector2.zero;
                (tableView.transform as RectTransform).anchoredPosition = Vector2.zero;

                // Thanks alot Caeden117 for helping out. Used his Counters+ Horizontal Settings view controller to get all rects set correctly.

                PageLeftButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageLeftButton")), transform);
                RectTransform buttonTransform = PageLeftButton.transform.Find("BG") as RectTransform;
                RectTransform glow = Instantiate(Resources.FindObjectsOfTypeAll<GameObject>().Last(x => (x.name == "GlowContainer")), PageLeftButton.transform).transform as RectTransform;
                glow.localPosition = buttonTransform.localPosition;
                glow.anchoredPosition = buttonTransform.anchoredPosition;
                glow.anchorMin = buttonTransform.anchorMin;
                glow.anchorMax = buttonTransform.anchorMax;
                glow.sizeDelta = buttonTransform.sizeDelta;
                PageLeftButton.transform.localPosition = new Vector3(-80, 2.5f, -5);
                PageLeftButton.interactable = true;
                PageRightButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageRightButton")), transform);
                buttonTransform = PageRightButton.transform.Find("BG") as RectTransform;
                glow = Instantiate(Resources.FindObjectsOfTypeAll<GameObject>().Last(x => (x.name == "GlowContainer")), PageRightButton.transform).transform as RectTransform;
                glow.localPosition = buttonTransform.localPosition;
                glow.anchoredPosition = buttonTransform.anchoredPosition;
                glow.anchorMin = buttonTransform.anchorMin;
                glow.anchorMax = buttonTransform.anchorMax;
                glow.sizeDelta = buttonTransform.sizeDelta;
                PageRightButton.transform.localPosition = new Vector3(80, 2.5f, -5);
                PageRightButton.interactable = true;

                RectTransform viewport = new GameObject("Viewport").AddComponent<RectTransform>(); //Make a Viewport RectTransform
                viewport.SetParent(tableView.transform as RectTransform, false); //It expects one from a ScrollRect, so we have to make one ourselves.
                viewport.sizeDelta = Vector2.zero; //Important to set this to zero so the TableView can scroll through all available cells.
                tableView.SetPrivateField("_pageUpButton", PageLeftButton); //Set Up button to Left
                tableView.SetPrivateField("_pageDownButton", PageRightButton); //Set down button to Right

                tableView.Init();
                tableView.SetPrivateField("_scrollRectTransform", viewport); //Set it with our hot-out-of-the-oven Viewport.
                tableGameObject.SetActive(true);
                _levelPacksTableView = new LevelPacksTableView();
                _levelPacksTableView.SetPrivateField("_tableView", tableView);
                string[] packs = new string[1];
                packs[0] = "";
                _levelPacksTableView.SetPrivateField("_promoPackIDStrings", packs);
                LevelPackTableCell prefab = Resources.FindObjectsOfTypeAll<LevelPackTableCell>().First(x => (x.name == "LevelPackTableCell"));
                _levelPacksTableView.SetPrivateField("_cellPrefab", prefab);
                _levelPacksTableView.SetPrivateField("_cellWidth", 30f);
                _levelPacksTableView.SetPrivateField("_additionalContentModel", _additionalContentModel);

                _levelPacksTableView.didSelectPackEvent += HandleLevelPacksTableViewDidSelectPack;
                _levelPacksTableView.Init();
                for (int i = 0; i < tableView.transform.childCount; i++)
                {
                    (tableView.transform.GetChild(i).transform as RectTransform).anchoredPosition = Vector3.zero;
                    (tableView.transform.GetChild(i).transform as RectTransform).anchorMin = Vector3.zero;
                    (tableView.transform.GetChild(i).transform as RectTransform).anchorMax = Vector3.one;
                }
                tableView.ReloadData();
                tableView.SelectCellWithIdx(0, false);
                _levelPacksTableView.SelectCellWithIdx(_selectedPackNum);
                _levelPacksTableView.RefreshPacksAvailability();
            }
            _additionalContentModel.didInvalidateDataEvent += HandleAdditionalContentModelDidInvalidateData;
        }

        protected override void DidDeactivate(VRUIViewController.DeactivationType deactivationType)
        {
            if (deactivationType == VRUIViewController.DeactivationType.RemovedFromHierarchy)
            {
                _levelPacksTableView.didSelectPackEvent -= HandleLevelPacksTableViewDidSelectPack;
            }
            _levelPacksTableView.CancelAsyncOperations();
            _additionalContentModel.didInvalidateDataEvent -= HandleAdditionalContentModelDidInvalidateData;
        }

        public virtual void SetData(IBeatmapLevelPackCollection levelPackCollection, int selectedPackNumber)
        {
            _levelPackCollection = levelPackCollection;
            _levelPacksTableView.SetData(_levelPackCollection);
            _selectedPackNum = selectedPackNumber;
            if (base.isInViewControllerHierarchy)
            {
                _levelPacksTableView.SelectCellWithIdx(selectedPackNumber);
            }
        }

        public virtual void HandleAdditionalContentModelDidInvalidateData()
        {
            _levelPacksTableView.RefreshPacksAvailability();
        }

        public virtual void HandleLevelPacksTableViewDidSelectPack(LevelPacksTableView levelPacksTableView, IBeatmapLevelPack pack)
        {
            for (int i = 0; i < _levelPackCollection.beatmapLevelPacks.Length; i++)
            {
                if (pack == _levelPackCollection.beatmapLevelPacks[i])
                {
                    _selectedPackNum = i;
                    break;
                }
            }
            Action<CustomLevelPacksViewController, IBeatmapLevelPack> action = didSelectPackEvent;
            if (action == null)
            {
                return;
            }
            action(this, pack);
        }
    }
}
