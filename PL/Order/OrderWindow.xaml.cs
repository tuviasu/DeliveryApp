using BO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using PL.Helpers;

namespace PL.Order
{
    public partial class OrderWindow : Window, INotifyPropertyChanged
    {
        static readonly BlApi.IBl s_bl = BlApi.Factory.Get();

        private readonly ObserverMutex _orderMutex = new(); //stage 7

        private readonly string _requesterId;
        public int OrderId { get; }

        private readonly BO.Config _cfg;
        private readonly DateTime _now;

        private bool _updatingOrder;

        public OrderWindow(string requesterId = "0", int orderId = 0)
        {
            _requesterId = requesterId;
            OrderId = orderId;
            ButtonText = (OrderId == 0) ? "Add" : "Update";

            InitializeComponent();

            int requesterIdInt = int.TryParse(_requesterId, out var rid) ? rid : 0;
            _now = s_bl.Admin.GetClock(requesterIdInt);
            _cfg = s_bl.Admin.GetConfig(requesterIdInt);

            if (OrderId == 0)
            {
                int nextId = s_bl.Order.GetNextOrderId(_requesterId);
                CurrentOrder = new BO.Order
                {
                    OrderID = nextId,
                    OrderOpenTime = _now,
                    MaxDeliveryTime = _now.Add(_cfg.MaxDeliveryTimeRange),
                    OrderType = BO.OrderType.Individual,
                    PizzaSize = BO.DeviceType.Desktop,
                    FullAddress = "",
                    CustomerFullName = "",
                    CustomerPhone = "",
                    Description = "",
                    DeliveriesForOrder = new()
                };

                // defaults for scheduled UI
                ScheduledDate = _now.Date;
                ScheduledTimeText = _now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
            else
            {
                QueryOrder();

                // defaults for scheduled UI when editing existing
                if (CurrentOrder != null)
                {
                    ScheduledDate = CurrentOrder.OrderOpenTime.Date;
                    ScheduledTimeText = CurrentOrder.OrderOpenTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                }
            }

            RefreshCanCancel();
        }

        public BO.Order? CurrentOrder
        {
            get { return (BO.Order)GetValue(CurrentOrderProperty); }
            set { SetValue(CurrentOrderProperty, value); }
        }

        public static readonly DependencyProperty CurrentOrderProperty =
            DependencyProperty.Register(
                "CurrentOrder",
                typeof(BO.Order),
                typeof(OrderWindow),
                new PropertyMetadata(null, (d, e) => ((OrderWindow)d).OnCurrentOrderChanged()));

        public BO.OrderType SelectedOrderType
        {
            get => (BO.OrderType)GetValue(SelectedOrderTypeProperty);
            set => SetValue(SelectedOrderTypeProperty, value);
        }

        public static readonly DependencyProperty SelectedOrderTypeProperty =
            DependencyProperty.Register(
                nameof(SelectedOrderType),
                typeof(BO.OrderType),
                typeof(OrderWindow),
                new PropertyMetadata(BO.OrderType.Individual, (d, e) => ((OrderWindow)d).OnSelectedOrderTypeChanged()));

        public bool IsScheduled => SelectedOrderType == BO.OrderType.Corporate;

        public DateTime ScheduledDate
        {
            get { return (DateTime)GetValue(ScheduledDateProperty); }
            set { SetValue(ScheduledDateProperty, value); }
        }

        public static readonly DependencyProperty ScheduledDateProperty =
            DependencyProperty.Register(
                "ScheduledDate",
                typeof(DateTime),
                typeof(OrderWindow),
                new PropertyMetadata(DateTime.Today, (d, e) => ((OrderWindow)d).ApplyScheduledDateTimeToOrder()));

        public string ScheduledTimeText
        {
            get { return (string)GetValue(ScheduledTimeTextProperty); }
            set { SetValue(ScheduledTimeTextProperty, value); }
        }

        public static readonly DependencyProperty ScheduledTimeTextProperty =
            DependencyProperty.Register(
                "ScheduledTimeText",
                typeof(string),
                typeof(OrderWindow),
                new PropertyMetadata("", (d, e) => ((OrderWindow)d).ApplyScheduledDateTimeToOrder()));

        private void OnCurrentOrderChanged()
        {
            if (_updatingOrder)
                return;

            // Keep SelectedOrderType synced from model
            if (CurrentOrder != null)
            {
                SelectedOrderType = CurrentOrder.OrderType;
                ScheduledDate = CurrentOrder.OrderOpenTime.Date;
                ScheduledTimeText = CurrentOrder.OrderOpenTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }

            OnPropertyChanged(nameof(IsScheduled));
            ApplyScheduledDateTimeToOrder();
        }

        private void OnSelectedOrderTypeChanged()
        {
            if (CurrentOrder == null)
                return;

            if (_updatingOrder)
                return;

            // Keep CurrentOrder.OrderType in sync with SelectedOrderType
            CurrentOrder.OrderType = SelectedOrderType;

            if (SelectedOrderType == BO.OrderType.Corporate)
            {
                // Initialize defaults (today + now) if user just switched
                if (ScheduledTimeText == "" || ScheduledDate == default)
                {
                    ScheduledDate = _now.Date;
                    ScheduledTimeText = _now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                }
            }

            OnPropertyChanged(nameof(IsScheduled));
            ApplyScheduledDateTimeToOrder();
        }

        private void ApplyScheduledDateTimeToOrder()
        {
            if (CurrentOrder == null)
                return;

            if (_updatingOrder)
                return;

            try
            {
                _updatingOrder = true;

                // For non-scheduled we keep "now" (system clock)
                if (SelectedOrderType != BO.OrderType.Corporate)
                {
                    if (ButtonText == "Add")
                    {
                        var newOrder = new BO.Order
                        {
                            OrderID = CurrentOrder.OrderID,
                            OrderType = SelectedOrderType,
                            PizzaSize = CurrentOrder.PizzaSize,
                            FullAddress = CurrentOrder.FullAddress,
                            CustomerFullName = CurrentOrder.CustomerFullName,
                            CustomerPhone = CurrentOrder.CustomerPhone,
                            Description = CurrentOrder.Description,
                            DeliveriesForOrder = CurrentOrder.DeliveriesForOrder,

                            OrderOpenTime = _now,
                            MaxDeliveryTime = _now.Add(_cfg.MaxDeliveryTimeRange)
                        };

                        CurrentOrder = newOrder;
                    }

                    OnPropertyChanged(nameof(IsScheduled));
                    return;
                }

                if (!TryParseTime(ScheduledTimeText, out var timeOfDay))
                    return;

                var start = ScheduledDate.Date.Add(timeOfDay);
                var end = start.Add(_cfg.MaxDeliveryTimeRange);

                CurrentOrder = new BO.Order
                {
                    OrderID = CurrentOrder.OrderID,
                    OrderType = SelectedOrderType,
                    PizzaSize = CurrentOrder.PizzaSize,
                    FullAddress = CurrentOrder.FullAddress,
                    CustomerFullName = CurrentOrder.CustomerFullName,
                    CustomerPhone = CurrentOrder.CustomerPhone,
                    Description = CurrentOrder.Description,
                    DeliveriesForOrder = CurrentOrder.DeliveriesForOrder,

                    OrderOpenTime = start,
                    MaxDeliveryTime = end
                };

                OnPropertyChanged(nameof(IsScheduled));
            }
            finally
            {
                _updatingOrder = false;
            }
        }

        private static bool TryParseTime(string? text, out TimeSpan time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Accept HH:mm:ss or HH:mm
            return TimeSpan.TryParseExact(text.Trim(), "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out time)
                || TimeSpan.TryParseExact(text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out time)
                || TimeSpan.TryParse(text.Trim(), CultureInfo.CurrentCulture, out time);
        }

        public string ButtonText
        {
            get { return (string)GetValue(ButtonTextProperty); }
            set { SetValue(ButtonTextProperty, value); }
        }

        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register("ButtonText", typeof(string), typeof(OrderWindow), new PropertyMetadata(string.Empty));

        public bool CanCancel
        {
            get { return (bool)GetValue(CanCancelProperty); }
            set { SetValue(CanCancelProperty, value); }
        }

        public static readonly DependencyProperty CanCancelProperty =
            DependencyProperty.Register("CanCancel", typeof(bool), typeof(OrderWindow), new PropertyMetadata(false));

        private void RefreshCanCancel()
        {
            if (CurrentOrder == null)
            {
                CanCancel = false;
                return;
            }

            // For new order (not saved yet) - no cancel.
            if (ButtonText == "Add")
            {
                CanCancel = false;
                return;
            }

            CanCancel = CurrentOrder.OrderStatus != BO.OrderStatus.Delivered;
        }

        private void btnAddOrUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentOrder == null)
                return;

            // Ensure times are synced before saving
            ApplyScheduledDateTimeToOrder();

            // Strict validation: reject invalid input before it reaches the BL.
            var errors = ValidateOrder();
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join("\n", errors);
                HasError = true;
                MessageBox.Show(this, ErrorMessage, "Invalid input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ErrorMessage = string.Empty;
            HasError = false;

            if (ButtonText == "Add")
            {
                try
                {
                    s_bl.Order.AddOrder(_requesterId, CurrentOrder);
                    MessageBox.Show("Order added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                try
                {
                    s_bl.Order.UpdateOrder(_requesterId, CurrentOrder);
                    MessageBox.Show("Order updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentOrder == null || ButtonText == "Add")
                return;

            if (!CanCancel)
                return;

            var result = MessageBox.Show($"Cancel order {CurrentOrder.OrderID}?", "Cancel Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                s_bl.Order.CancelOrder(_requesterId, CurrentOrder.OrderID);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to cancel order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QueryOrder()
        {
            CurrentOrder = s_bl.Order.GetOrderDetails(_requesterId, OrderId);
            RefreshCanCancel();
        }

        /// <summary>
        /// Validates the current order's user-editable fields. Returns the list of
        /// human-readable problems (empty = valid).
        /// </summary>
        private List<string> ValidateOrder()
        {
            var errors = new List<string>();
            if (CurrentOrder == null)
            {
                errors.Add("No order is loaded.");
                return errors;
            }

            // Order ID is now auto-assigned; no manual validation needed.

            if (string.IsNullOrWhiteSpace(CurrentOrder.CustomerFullName))
                errors.Add("Customer name is required.");

            var phone = CurrentOrder.CustomerPhone?.Trim() ?? "";
            if (!Regex.IsMatch(phone, @"^05\d{8}$"))
                errors.Add("Phone must be 10 digits starting with 05 (e.g. 0501234567).");

            if (string.IsNullOrWhiteSpace(CurrentOrder.FullAddress))
                errors.Add("Address is required.");

            // Scheduled (Corporate) orders need a valid time-of-day.
            if (SelectedOrderType == BO.OrderType.Corporate &&
                !TryParseTime(ScheduledTimeText, out _))
                errors.Add("Scheduled time must be HH:mm or HH:mm:ss.");

            return errors;
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
        private string _errorMessage = string.Empty;

        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                OnPropertyChanged(nameof(HasError));
            }
        }
        private bool _hasError;

        private void OrderObserver()
        {
            #region Stage 7 (for multithreading)
            if (_orderMutex.CheckAndSetLoadInProgressOrRestartRequired())
                return;

            _ = Dispatcher.BeginInvoke(async () =>
            {
                QueryOrder();
                if (await _orderMutex.UnsetLoadInProgressAndCheckRestartRequested())
                    OrderObserver();
            });
            #endregion Stage 7 (for multithreading)
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (OrderId != 0)
                s_bl.Order.AddObserver(OrderId, OrderObserver);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (OrderId != 0)
                s_bl.Order.RemoveObserver(OrderId, OrderObserver);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
