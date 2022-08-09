using System;
using System.Collections.Generic;
using System.Globalization;
using Nekoyume.EnumType;
using Nekoyume.Game.Controller;
using Nekoyume.L10n;
using Nekoyume.Model.Mail;
using Nekoyume.UI.Module;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nekoyume.UI
{
    using Nekoyume.UI.Scroller;
    using UniRx;

    public class ItemCountableAndPricePopup : ItemCountPopup<Model.ItemCountableAndPricePopup>
    {
        [SerializeField] private Button overrideCancelButton;

        [SerializeField] private TMP_InputField priceInputField = null;
        [SerializeField] private TMP_InputField countInputField = null;

        [SerializeField] private Button addCountButton = null;
        [SerializeField] private Button addMaximumCountButton = null;
        [SerializeField] private Button removeCountButton = null;
        [SerializeField] private Button resetPriceButton = null;
        [SerializeField] private Button notificationButton = null;
        [SerializeField] private ConditionalButton reregisterButton = null;
        [SerializeField] private List<Button> addPriceButton = null;

        [SerializeField] private TextMeshProUGUI unitPrice;

        [SerializeField] private GameObject positiveMessage;
        [SerializeField] private GameObject warningMessage;

        private readonly List<IDisposable> _disposablesForAwake = new List<IDisposable>();
        private readonly List<IDisposable> _disposablesForSetData = new List<IDisposable>();

        private const int DefaultPrice = 10;
        public override CloseKeyType CloseKeyType => CloseKeyType.Escape;

        #region Mono

        protected override void Awake()
        {
            base.Awake();

            if (overrideCancelButton != null)
            {
                overrideCancelButton.onClick.AddListener(() =>
                {
                    _data?.OnClickCancel.OnNext(_data);
                });
                CloseWidget = overrideCancelButton.onClick.Invoke;
            }

            countInputField.onEndEdit.AsObservable().Subscribe(_ =>
            {
                if (countInputField.text.Equals(string.Empty) ||
                    countInputField.text.Equals("0"))
                {
                    countInputField.text = string.Empty;
                    _data.OnChangeCount.OnNext(0);
                }
                else
                {
                    var maxCount = 1;
                    if (_data.Item is { Value: { } })
                    {
                        maxCount = _data.Item.Value.MaxCount.Value;
                    }
                    var count = InputFieldValueToValue<int>(countInputField);
                    var result = Mathf.Clamp(count, 1, maxCount);
                    countInputField.text = result.ToString();
                    _data.OnChangeCount.OnNext(result);
                }

            }).AddTo(_disposablesForAwake);

            addCountButton.OnClickAsObservable().Subscribe(_ =>
            {
                var maxCount = _data.Item.Value.MaxCount.Value;
                var count = InputFieldValueToValue<int>(countInputField) + 1;
                _data.OnChangeCount.OnNext(Math.Min(count, maxCount));
            }).AddTo(_disposablesForAwake);

            addMaximumCountButton.OnClickAsObservable().Subscribe(_ =>
            {
                var maxCount = _data.Item.Value.MaxCount.Value;
                _data.OnChangeCount.OnNext(maxCount);
            }).AddTo(_disposablesForAwake);

            removeCountButton.OnClickAsObservable().Subscribe(_ =>
            {
                var count = InputFieldValueToValue<int>(countInputField) - 1;
                _data.OnChangeCount.OnNext(Math.Max(count, 1));
            }).AddTo(_disposablesForAwake);

            // price
            priceInputField.onEndEdit.AsObservable().Subscribe(_ =>
            {
                if (priceInputField.text.Length > 0 &&
                    priceInputField.text.Length == priceInputField.caretPosition &&
                    priceInputField.text[priceInputField.caretPosition - 1].Equals('.'))
                {
                    return;
                }

                if (priceInputField.text.Equals(string.Empty) ||
                    priceInputField.text.Equals("0"))
                {
                    priceInputField.text = string.Empty;
                    _data.OnChangePrice.OnNext(0);
                }
                else
                {
                    var price = InputFieldValueToValue<decimal>(priceInputField);
                    _data.OnChangePrice.OnNext(price);
                }

            }).AddTo(_disposablesForAwake);

            resetPriceButton.OnClickAsObservable()
                .Subscribe(_ => _data.OnChangePrice.OnNext(Model.Shop.MinimumPrice));

            for (int i = 0; i < addPriceButton.Count; i++)
            {
                int digit = i;
                addPriceButton[i].OnClickAsObservable().Subscribe(_ =>
                {
                    var price = InputFieldValueToValue<decimal>(priceInputField) +
                                (int) Mathf.Pow(DefaultPrice, digit);
                    _data.OnChangePrice.OnNext(price);
                }).AddTo(_disposablesForAwake);
            }

            reregisterButton.Text = L10nManager.Localize("UI_REREGISTER");
            reregisterButton.OnSubmitSubject
                .Subscribe(_ =>
                {
                    _data?.OnClickReregister.OnNext(_data);
                })
                .AddTo(_disposablesForAwake);

            notificationButton.OnClickAsObservable().Subscribe(_ =>
            {
                OneLineSystem.Push(
                    MailType.System,
                    L10nManager.Localize("NOTIFICATION_QUANTITY_CANNOT_CHANGED"),
                    NotificationCell.NotificationType.Information);
            }).AddTo(_disposablesForAwake);

            CloseWidget = () =>
            {
                if (countInputField.isFocused || priceInputField.isFocused)
                {
                    return;
                }

                _data?.OnClickCancel.OnNext(_data);
            };

            L10nManager.OnLanguageChange.Subscribe(_ =>
            {
                reregisterButton.Text = L10nManager.Localize("UI_REREGISTER");
            }).AddTo(_disposablesForAwake);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _disposablesForAwake.DisposeAllAndClear();
            Clear();
        }

        #endregion

        protected override void SetData(Model.ItemCountableAndPricePopup data)
        {
            if (ReferenceEquals(data, null))
            {
                Clear();
                return;
            }

            _disposablesForSetData.DisposeAllAndClear();
            base.SetData(data);

            priceInputField.text = data.Count.Value.ToString();
            countInputField.text = data.Count.Value.ToString();
            _data.Price.Value = data.Price.Value;
            _data.UnitPrice.Value = data.UnitPrice.Value;

            _data.Count.Subscribe(value => countInputField.text = value.ToString())
                .AddTo(_disposablesForSetData);

            _data.Price.Subscribe(value =>
                {
                    if (value.MinorUnit == 0 && priceInputField.text.Contains(".0"))
                    {
                        priceInputField.text = $"{value.MajorUnit}.{value.MinorUnit}";
                    }
                    else if (value.MajorUnit == 0 && value.MinorUnit == 0)
                    {
                        priceInputField.text = string.Empty;
                    }
                    else
                    {
                        priceInputField.text = value.GetQuantityString();
                    }

                    var isValid = IsValid();
                    submitButton.Interactable = isValid;
                    reregisterButton.Interactable = isValid;
                    positiveMessage.SetActive(isValid);
                    warningMessage.SetActive(!isValid);
                })
                .AddTo(_disposablesForSetData);

            _data.UnitPrice.Subscribe(value =>
                {
                    unitPrice.text = $"/{value.GetQuantityString()}";
                })
                .AddTo(_disposablesForSetData);

            priceInputField.Select();
            countInputField.Select();
        }

        private bool IsValid()
        {
            if (decimal.TryParse(_data.Price.Value.GetQuantityString(),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
            {
                if (price - (int) price > 0)
                {
                    return false;
                }

                var count = _data.Count.Value;
                return !(price < Model.Shop.MinimumPrice || count < 0);
            }

            return false;
        }

        protected override void Clear()
        {
            _disposablesForSetData.DisposeAllAndClear();
            base.Clear();
        }

        private T InputFieldValueToValue<T>(TMP_InputField inputField)
        {
            var result = 0;

            if (typeof(T) == typeof(decimal))
            {
                if (!decimal.TryParse(inputField.text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out var price))
                {
                    price = 0;
                }
                return (T)Convert.ChangeType(price, typeof(T));
            }

            if (typeof(T) == typeof(int))
            {
                if (!int.TryParse(inputField.text, NumberStyles.Number, CultureInfo.InvariantCulture,
                    out var price))
                {
                    price = 0;
                }
                return (T)Convert.ChangeType(price, typeof(T));
            }

            return (T)Convert.ChangeType(result, typeof(T));
        }

        public void Show(Model.ItemCountableAndPricePopup data, bool isSell)
        {
            if (isSell)
            {
                SubmitWidget = () => submitButton.OnSubmitSubject.OnNext(default);
            }
            else
            {
                SubmitWidget = () => reregisterButton.OnSubmitSubject.OnNext(default);
            }

            countInputField.enabled = isSell;
            addCountButton.gameObject.SetActive(isSell);
            addMaximumCountButton.gameObject.SetActive(isSell);
            removeCountButton.gameObject.SetActive(isSell);
            submitButton.gameObject.SetActive(isSell);
            reregisterButton.gameObject.SetActive(!isSell);
            notificationButton.gameObject.SetActive(!isSell);
            Pop(data);
        }
    }
}
