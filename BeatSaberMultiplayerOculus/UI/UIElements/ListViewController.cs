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
    public class ListViewController : MonoBehaviour
    {
        public event Action<int> ValueChanged;

        private Button _decButton;
        private Button _incButton;

        private TextMeshProUGUI _valueText;

        public string[] textForValues;

        public int _value;

        public int Value { get { return _value; } set { _value = value; _valueText.text = _value.ToString(); UpdateButtons(); } }

        public int minValue = 0;
        public int maxValue = 999;

        public void OnEnable()
        {
            textForValues = new string[0];

            _incButton = GetComponentsInChildren<Button>().First(x => x.name == "UpButton");
            _incButton.onClick.RemoveAllListeners();
            _incButton.onClick.AddListener(delegate()
            {
                _value += 1;
                ValueChanged?.Invoke(_value);

                UpdateButtons();
                _valueText.text = (textForValues.Length > _value) ? textForValues[_value] : _value.ToString();
            });

            _decButton = GetComponentsInChildren<Button>().First(x => x.name == "DownButton");
            _decButton.onClick.RemoveAllListeners();
            _decButton.onClick.AddListener(delegate ()
            {
                _value -= 1;
                ValueChanged?.Invoke(_value);

                UpdateButtons();
                _valueText.text = (textForValues.Length > _value) ? textForValues[_value] : _value.ToString();
            });

            _valueText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "ValueText");
            _valueText.text = (textForValues.Length > _value) ? textForValues[_value] : _value.ToString();
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            if (_value <= minValue)
            {
                _decButton.interactable = false;
            }
            else
            {
                _decButton.interactable = true;
            }

            if (_value >= maxValue)
            {
                _incButton.interactable = false;
            }
            else
            {
                _incButton.interactable = true;
            }
        }

        public void UpdateText()
        {
            _valueText.text = (textForValues.Length > _value) ? textForValues[_value] : _value.ToString();
        }

    }
}
