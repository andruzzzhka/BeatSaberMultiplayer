using BeatSaberMultiplayer.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.UIElements
{
    public class OnOffViewController : MonoBehaviour
    {
        public event Action<bool> ValueChanged;

        private Button _decButton;
        private Button _incButton;

        private TextMeshProUGUI _valueText;

        public bool _value;

        public bool Value { get {return _value; } set { _value = value; _valueText.text = _value ? "ON" : "OFF"; UpdateButtons();} }

        public void OnEnable()
        {
            _incButton = GetComponentsInChildren<Button>().First(x => x.name == "IncButton");
            _incButton.onClick.RemoveAllListeners();
            _incButton.onClick.AddListener(delegate ()
            {
                _value = true;
                ValueChanged?.Invoke(_value);

                UpdateButtons();
                _valueText.text = _value ? "ON" : "OFF";
            });
            
            _decButton = GetComponentsInChildren<Button>().First(x => x.name == "DecButton");
            _decButton.onClick.RemoveAllListeners();
            _decButton.onClick.AddListener(delegate ()
            {
                _value = false;
                ValueChanged?.Invoke(_value);

                UpdateButtons();
                _valueText.text = _value ? "ON" : "OFF";
            });
            
            _valueText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "ValueText");
            _valueText.text = _value ? "ON" : "OFF";
            
            (transform as RectTransform).sizeDelta = new Vector2(105f, 8f);

            UpdateButtons();
        }

        public void UpdateButtons()
        {
            _decButton.interactable = _value;
            _incButton.interactable = !_value;
        }
    }
}
