namespace BlImplementation;
using BlApi;
using BO;
using Helpers;

internal class OrderImplementation : IOrder
{
    public int GetNextOrderId(string requesterId)
    {
        var allOrders = OrderManager.GetAllOrders().ToList();
        return allOrders.Any() ? allOrders.Max(o => o.OrderID) + 1 : 1000;
    }

    public void AddOrder(string requesterId, Order order)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (order == null)
            throw new BlInvalidIdException("Order cannot be null", null);

        var allOrders = OrderManager.GetAllOrders().ToList();
        int nextId = allOrders.Any() ? allOrders.Max(o => o.OrderID) + 1 : 1000;

        var doOrder = new DO.Order(
            OrderID: nextId,
            Description: order.Description,
            FullAddress: order.FullAddress,
            Latitude: order.Latitude,
            Longitude: order.Longitude,
            CustomerName: order.CustomerFullName,
            CustomerPhone: order.CustomerPhone,
            OrderType: (DO.OrderType)order.OrderType,
            PizzaSize: (DO.DeviceType)order.PizzaSize,
            OrderOpeningTime: AdminManager.Now
        );

        OrderManager.AddOrder(doOrder);
        OrderManager.Observers.NotifyListUpdated();
    }

    public void CancelOrder(string requesterId, int orderId)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (!OrderManager.IsOrderExists(orderId))
            throw new BlItemNotFoundException($"Order with ID {orderId} not found", null);

        var lastDelivery = DeliveryManager.GetLastDeliveryForOrder(orderId);

        // Case 1: order is currently being delivered -> close the current delivery
        if (lastDelivery != null && lastDelivery.DeliveryDoneTime == null)
        {
            var updated = lastDelivery with
            {
                DeliveryDoneType = DO.ProcessResult.Cancelled,
                DeliveryDoneTime = AdminManager.Now
            };

            DeliveryManager.UpdateDelivery(updated);
            OrderManager.Observers.NotifyItemUpdated(orderId);
            OrderManager.Observers.NotifyListUpdated();
            return;
        }

        // Case 2: no active delivery -> create a dummy delivery that immediately closes the order
        // NOTE: In DalList, IDs are generated from an in-memory counter that may be behind after DB initialization.
        // To avoid DalIdAlreadyExist, generate a safe new ID based on the current max delivery ID.
        var nextDeliveryId = DeliveryManager.GetAllDeliveries()
            .Select(d => d.DeliveryID)
            .DefaultIfEmpty(99999)
            .Max() + 1;

        var dummy = new DO.Delivery(
            DeliveryID: nextDeliveryId,
            OrderID: orderId,
            CourierID: 0,
            DeliveryType: null,
            DeliveryStartTime: AdminManager.Now,
            DeliveryDistance: 0,
            DeliveryDoneType: DO.ProcessResult.Cancelled,
            DeliveryDoneTime: AdminManager.Now
        );

        DeliveryManager.AddDelivery(dummy);
        OrderManager.Observers.NotifyItemUpdated(orderId);
        OrderManager.Observers.NotifyListUpdated();
    }

    public void CompleteOrderHandling(string requesterId, string courierId, int deliveryId)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (!int.TryParse(courierId, out int courierIdInt))
            throw new BlInvalidIdException("Invalid courier ID format", null);

        var delivery = DeliveryManager.GetDeliveryById(deliveryId);
        if (delivery == null)
            throw new BlItemNotFoundException($"Delivery with ID {deliveryId} not found", null);

        if (delivery.CourierID != courierIdInt)
            throw new BlInvalidIdException($"Delivery {deliveryId} is not assigned to courier {courierId}", null);

        if (delivery.DeliveryDoneTime != null)
            throw new BlInvalidIdException($"Delivery {deliveryId} is already completed", null);

        // doneType is always Completed when triggered by a courier completing their run.
        // The requesterId is a user/manager identifier, not a delivery result string.
        DO.ProcessResult doneType = DO.ProcessResult.Completed;

        var updatedDelivery = delivery with
        {
            DeliveryDoneTime = AdminManager.Now,
            DeliveryDoneType = doneType
        };

        DeliveryManager.UpdateDelivery(updatedDelivery);

        // Spec-specific behavior:
        // - CustomerNotFound: close current delivery but order should become available again.
        // - Failed: close current delivery but order stays open.
        // In this codebase, "open" means: last delivery is completed and NOT Cancelled.
        // Therefore both cases work automatically for selection logic.

        OrderManager.Observers.NotifyItemUpdated(delivery.OrderID);
        OrderManager.Observers.NotifyListUpdated();
    }

    public void DeleteOrder(string requesterId, int orderId)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7

        if (!OrderManager.IsOrderExists(orderId))
            throw new BlItemNotFoundException($"Order with ID {orderId} not found", null);

        // Don't allow deleting an order that currently has an active delivery.
        var lastDelivery = DeliveryManager.GetLastDeliveryForOrder(orderId);
        if (lastDelivery != null && lastDelivery.DeliveryDoneTime == null)
            throw new BlInvalidIdException($"Cannot delete order {orderId} - delivery is in progress", null);

        // Remove related deliveries first (if any) to keep data consistent.
        var deliveries = DeliveryManager.GetDeliveriesByOrder(orderId).ToList();
        foreach (var d in deliveries)
        {
            DeliveryManager.DeleteDelivery(d.DeliveryID);
        }

        OrderManager.DeleteOrder(orderId);

        OrderManager.Observers.NotifyItemUpdated(orderId);
        OrderManager.Observers.NotifyListUpdated();
    }

    public IEnumerable<ClosedDeliveryInList> GetClosedOrdersByCourier(string requesterId, string courierId, OrderType? orderTypeFilter, ClosedDeliveryListSortProperty? sortProperty)
    {
        if (!int.TryParse(courierId, out int courierIdInt))
            throw new BlInvalidIdException("Invalid courier ID format", null);

        if (!CourierManager.IsCourierExists(courierIdInt))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        var deliveries = DeliveryManager.GetCompletedDeliveries()
            .Where(d => d.CourierID == courierIdInt);

        var result = deliveries.Select(d =>
        {
            var order = OrderManager.GetOrderById(d.OrderID);
            if (order == null) return null;

            if (orderTypeFilter.HasValue && (DO.OrderType)orderTypeFilter.Value != order.OrderType)
                return null;

            return new ClosedDeliveryInList
            {
                DeliveryID = d.DeliveryID,
                OrderID = d.OrderID,
                orderType = (BO.OrderType)order.OrderType,
                FullAddress = order.FullAddress ?? "",
                deliveryType = d.DeliveryType.HasValue ? (BO.DeliveryType)d.DeliveryType.Value : BO.DeliveryType.Regular,
                RealDistance = d.DeliveryDistance,
                TotalHandlingTime = d.DeliveryDoneTime.HasValue
                    ? d.DeliveryDoneTime.Value - d.DeliveryStartTime
                    : TimeSpan.Zero,
                deliveryDoneType = d.DeliveryDoneType.HasValue ? (BO.DeliveryDoneType?)d.DeliveryDoneType.Value : null
            };
        }).Where(x => x != null).Cast<ClosedDeliveryInList>();

        if (sortProperty.HasValue)
        {
            result = sortProperty.Value switch
            {
                ClosedDeliveryListSortProperty.DeliveryId => result.OrderBy(x => x.DeliveryID),
                ClosedDeliveryListSortProperty.DeliveryType => result.OrderBy(x => x.deliveryType),
                _ => result
            };
        }

        return result;
    }

    public async Task<IEnumerable<OpenOrderInList>> GetOpenOrdersForCourierAsync(
        string requesterId,
        string courierId,
        OrderType? orderTypeFilter,
        OpenOrderListSortProperty? sortProperty,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(courierId, out int courierIdInt))
            throw new BlInvalidIdException("Invalid courier ID format", null);

        if (!CourierManager.IsCourierExists(courierIdInt))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        // Only orders that are currently available to be taken:
        // - No deliveries yet
        // - OR last delivery is completed AND was not a cancellation
        var openOrders = OrderManager.GetAllOrders()
            .Where(o =>
            {
                var lastDelivery = DeliveryManager.GetLastDeliveryForOrder(o.OrderID);
                if (lastDelivery == null)
                    return true;

                if (lastDelivery.DeliveryDoneTime == null)
                    return false; // already in progress

                if (lastDelivery.DeliveryDoneType == DO.ProcessResult.Cancelled)
                    return false;

                return true;
            });

        if (orderTypeFilter.HasValue)
            openOrders = openOrders.Where(o => (BO.OrderType)o.OrderType == orderTypeFilter.Value);

        var config = AdminManager.GetConfig();
        var courier = CourierManager.GetCourierById(courierIdInt);

        var companyLat = config.Latitude;
        var companyLon = config.Longitude;

        double maxKm = courier!.MaxDeliveryDistanceKm
            ?? config.MaxDeliveryDistance
            ?? double.MaxValue;

        // Materialize now to avoid deferred-execution while doing awaits.
        var ordersList = openOrders.ToList();

        // Create tasks that compute the items including the network distance.
        var tasks = ordersList.Select(async o =>
        {
            var maxDeliveryTime = o.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
            var timeRemaining = maxDeliveryTime - AdminManager.Now;

            var scheduleStatus = timeRemaining < TimeSpan.Zero
                ? ScheduleStatus.Late
                : (timeRemaining <= config.RiskRange ? ScheduleStatus.InRisk : ScheduleStatus.OnTime);

            double airDistance = (companyLat is null || companyLon is null)
                ? 0
                : OrderManager.CalculateDistance(companyLat.Value, companyLon.Value, o.Latitude, o.Longitude);

            // Stage 7: network distance is calculated asynchronously (OSRM)
            double? realDistance = await Tools.CalcNetworkDistanceKmAsync(
                companyLat,
                companyLon,
                o.Latitude,
                o.Longitude,
                Tools.TravelMode.Driving,
                cancellationToken);

            return new OpenOrderInList
            {
                CourierID = courierIdInt,
                OrderID = o.OrderID,
                orderType = (BO.OrderType)o.OrderType,
                pizzaSize = (BO.DeviceType)o.PizzaSize,
                FullAddress = o.FullAddress ?? string.Empty,
                AirDistance = airDistance,
                RealDistance = realDistance,
                EstimatedTimeInReality = null,
                scheduleStatus = scheduleStatus,
                TimeRemainingToComplete = timeRemaining,
                MaxDeliveryTime = maxDeliveryTime
            };
        });

        var items = await Task.WhenAll(tasks);

        var result = items
            .Where(x => x.AirDistance <= maxKm);

        if (sortProperty.HasValue)
        {
            result = sortProperty.Value switch
            {
                OpenOrderListSortProperty.OrderId => result.OrderBy(x => x.OrderID),
                OpenOrderListSortProperty.OrderDate => result.OrderBy(x => x.TimeRemainingToComplete),
                _ => result
            };
        }

        return result.ToList();
    }

    public async IAsyncEnumerable<OpenOrderInList> StreamOpenOrdersForCourierAsync(
        string requesterId,
        string courierId,
        OrderType? orderTypeFilter,
        OpenOrderListSortProperty? sortProperty,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(courierId, out int courierIdInt))
            throw new BlInvalidIdException("Invalid courier ID format", null);

        if (!CourierManager.IsCourierExists(courierIdInt))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        var openOrders = OrderManager.GetAllOrders()
            .Where(o =>
            {
                var lastDelivery = DeliveryManager.GetLastDeliveryForOrder(o.OrderID);
                if (lastDelivery == null)
                    return true;

                if (lastDelivery.DeliveryDoneTime == null)
                    return false;

                if (lastDelivery.DeliveryDoneType == DO.ProcessResult.Cancelled)
                    return false;

                return true;
            });

        if (orderTypeFilter.HasValue)
            openOrders = openOrders.Where(o => (BO.OrderType)o.OrderType == orderTypeFilter.Value);

        var config = AdminManager.GetConfig();
        var courier = CourierManager.GetCourierById(courierIdInt);

        var companyLat = config.Latitude;
        var companyLon = config.Longitude;

        double maxKm = courier!.MaxDeliveryDistanceKm
            ?? config.MaxDeliveryDistance
            ?? double.MaxValue;

        // Materialize early to avoid deferred execution during async iteration.
        IEnumerable<DO.Order> ordersSeq = openOrders.ToList();

        // Optional: deterministic ordering for UI streaming.
        ordersSeq = sortProperty switch
        {
            OpenOrderListSortProperty.OrderId => ordersSeq.OrderBy(o => o.OrderID),
            OpenOrderListSortProperty.OrderDate => ordersSeq.OrderBy(o => o.OrderOpeningTime),
            _ => ordersSeq
        };

        foreach (var o in ordersSeq)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var maxDeliveryTime = o.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
            var timeRemaining = maxDeliveryTime - AdminManager.Now;

            var scheduleStatus = timeRemaining < TimeSpan.Zero
                ? ScheduleStatus.Late
                : (timeRemaining <= config.RiskRange ? ScheduleStatus.InRisk : ScheduleStatus.OnTime);

            double airDistance = (companyLat is null || companyLon is null)
                ? 0
                : OrderManager.CalculateDistance(companyLat.Value, companyLon.Value, o.Latitude, o.Longitude);

            if (airDistance > maxKm)
                continue;

            double? realDistance = await Tools.CalcNetworkDistanceKmAsync(
                companyLat,
                companyLon,
                o.Latitude,
                o.Longitude,
                Tools.TravelMode.Driving,
                cancellationToken);

            yield return new OpenOrderInList
            {
                CourierID = courierIdInt,
                OrderID = o.OrderID,
                orderType = (BO.OrderType)o.OrderType,
                pizzaSize = (BO.DeviceType)o.PizzaSize,
                FullAddress = o.FullAddress ?? string.Empty,
                AirDistance = airDistance,
                RealDistance = realDistance,
                EstimatedTimeInReality = null,
                scheduleStatus = scheduleStatus,
                TimeRemainingToComplete = timeRemaining,
                MaxDeliveryTime = maxDeliveryTime
            };
        }
    }

    public IEnumerable<OpenOrderInList> GetOpenOrdersForCourier(string requesterId, string courierId, OrderType? orderTypeFilter, OpenOrderListSortProperty? sortProperty)
    {
        if (!int.TryParse(courierId, out int courierIdInt))
            throw new BlInvalidIdException("Invalid courier ID format", null);

        if (!CourierManager.IsCourierExists(courierIdInt))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        // Fetch all required data under a single lock, then work on in-memory snapshots
        // to avoid O(N×M) repeated lock acquisitions inside LINQ predicates.
        var allOrders = OrderManager.GetAllOrders().ToList();
        var allDeliveries = DeliveryManager.GetAllDeliveries().ToList();

        // Build a lookup: orderId -> last delivery (by start time)
        var lastDeliveryByOrder = allDeliveries
            .GroupBy(d => d.OrderID)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.DeliveryStartTime).First());

        var openOrders = allOrders.Where(o =>
        {
            if (!lastDeliveryByOrder.TryGetValue(o.OrderID, out var lastDelivery))
                return true; // no deliveries yet

            if (lastDelivery.DeliveryDoneTime == null)
                return false; // already in progress

            if (lastDelivery.DeliveryDoneType == DO.ProcessResult.Cancelled)
                return false;

            return true;
        });

        if (orderTypeFilter.HasValue)
            openOrders = openOrders.Where(o => (BO.OrderType)o.OrderType == orderTypeFilter.Value);

        var config = AdminManager.GetConfig();
        var courier = CourierManager.GetCourierById(courierIdInt);

        var companyLat = config.Latitude ?? 0;
        var companyLon = config.Longitude ?? 0;

        double maxKm = courier!.MaxDeliveryDistanceKm
            ?? config.MaxDeliveryDistance
            ?? double.MaxValue;

        var result = openOrders
            .Select(o =>
            {
                double airDistance = (companyLat == 0 && companyLon == 0)
                    ? 0
                    : OrderManager.CalculateDistance(companyLat, companyLon, o.Latitude, o.Longitude);

                var maxDeliveryTime = o.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
                var timeRemaining = maxDeliveryTime - AdminManager.Now;

                var scheduleStatus = timeRemaining < TimeSpan.Zero
                    ? ScheduleStatus.Late
                    : (timeRemaining <= config.RiskRange ? ScheduleStatus.InRisk : ScheduleStatus.OnTime);

                return new OpenOrderInList
                {
                    CourierID = courierIdInt,
                    OrderID = o.OrderID,
                    orderType = (BO.OrderType)o.OrderType,
                    pizzaSize = (BO.DeviceType)o.PizzaSize,
                    FullAddress = o.FullAddress ?? "",
                    AirDistance = airDistance,
                    RealDistance = null,
                    EstimatedTimeInReality = null,
                    scheduleStatus = scheduleStatus,
                    TimeRemainingToComplete = timeRemaining,
                    MaxDeliveryTime = maxDeliveryTime
                };
            })
            .Where(x => x.AirDistance <= maxKm);

        if (sortProperty.HasValue)
        {
            result = sortProperty.Value switch
            {
                OpenOrderListSortProperty.OrderId => result.OrderBy(x => x.OrderID),
                OpenOrderListSortProperty.OrderDate => result.OrderBy(x => x.TimeRemainingToComplete),
                _ => result
            };
        }

        return result;
    }

    public Order GetOrderDetails(string requesterId, int orderId)
    {
        var doOrder = OrderManager.GetOrderById(orderId);
        if (doOrder == null)
            throw new BlItemNotFoundException($"Order with ID {orderId} not found", null);

        var deliveries = DeliveryManager.GetDeliveriesByOrder(orderId);
        var config = AdminManager.GetConfig();

        var maxDeliveryTime = doOrder.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
        var timeRemaining = maxDeliveryTime - AdminManager.Now;

        var lastDelivery = deliveries.OrderByDescending(d => d.DeliveryStartTime).FirstOrDefault();

        ScheduleStatus scheduleStatus = timeRemaining < TimeSpan.Zero
            ? ScheduleStatus.Late
            : (timeRemaining <= config.RiskRange ? ScheduleStatus.InRisk : ScheduleStatus.OnTime);

        OrderStatus orderStatus = OrderStatus.NotDelivered;

        if (lastDelivery != null && lastDelivery.DeliveryDoneTime.HasValue && lastDelivery.DeliveryDoneType.HasValue)
        {
            orderStatus = lastDelivery.DeliveryDoneType.Value switch
            {
                DO.ProcessResult.Completed => OrderStatus.Delivered,
                DO.ProcessResult.CustomerRefused => OrderStatus.CustomerRefused,
                DO.ProcessResult.Cancelled => OrderStatus.Cancelled,
                _ => OrderStatus.NotDelivered
            };

            // For closed orders: base schedule status on done time vs max
            scheduleStatus = lastDelivery.DeliveryDoneTime.Value <= maxDeliveryTime
                ? ScheduleStatus.OnTime
                : ScheduleStatus.Late;
        }

        var deliveriesList = deliveries.Select(d =>
        {
            var courier = CourierManager.GetCourierById(d.CourierID);

            return new DeliveryPerOrderInList
            {
                DeliveryID = d.DeliveryID,
                CourierID = d.CourierID,
                CourierName = courier?.FullName ?? "Unknown",
                DeliveryType = d.DeliveryType.HasValue ? (BO.DeliveryType)d.DeliveryType.Value : BO.DeliveryType.Regular,
                DeliveryStartTime = d.DeliveryStartTime,
                deliveryDoneType = d.DeliveryDoneType.HasValue ? (BO.DeliveryDoneType?)d.DeliveryDoneType.Value : null,
                DeliveryDoneTime = d.DeliveryDoneTime
            };
        }).ToList();

        return new Order
        {
            OrderID = doOrder.OrderID,
            OrderType = (BO.OrderType)doOrder.OrderType,
            Description = doOrder.Description,
            FullAddress = doOrder.FullAddress ?? "",
            Latitude = doOrder.Latitude,
            Longitude = doOrder.Longitude,
            AirDistance = 0,
            CustomerFullName = doOrder.CustomerName ?? "",
            CustomerPhone = doOrder.CustomerPhone ?? "",
            PizzaSize = (BO.DeviceType)doOrder.PizzaSize,
            OrderOpenTime = doOrder.OrderOpeningTime,
            EstimatedDeliveryTime = null,
            MaxDeliveryTime = maxDeliveryTime,
            OrderStatus = orderStatus,
            ScheduleStatus = scheduleStatus,
            TimeRemainingToComplete = timeRemaining,
            DeliveriesForOrder = deliveriesList
        };
    }

    public IEnumerable<OrderInList> GetOrdersList(string requesterId, OrderListFilterProperty? filterProperty, object? filterValue, OrderListSortProperty? sortProperty)
    {
        var orders = OrderManager.GetAllOrders();
        var config = AdminManager.GetConfig();

        var companyLat = config.Latitude ?? 0;
        var companyLon = config.Longitude ?? 0;

        var result = orders.Select(o =>
        {
            var deliveries = DeliveryManager.GetDeliveriesByOrder(o.OrderID);
            var lastDelivery = deliveries.OrderByDescending(d => d.DeliveryStartTime).FirstOrDefault();

            var maxDeliveryTime = o.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
            var timeRemaining = maxDeliveryTime - AdminManager.Now;

            var scheduleStatus = timeRemaining < TimeSpan.Zero
                ? ScheduleStatus.Late
                : (timeRemaining <= config.RiskRange ? ScheduleStatus.InRisk : ScheduleStatus.OnTime);

            OrderStatus orderStatus = OrderStatus.NotDelivered;

            if (lastDelivery != null && lastDelivery.DeliveryDoneTime.HasValue && lastDelivery.DeliveryDoneType.HasValue)
            {
                orderStatus = lastDelivery.DeliveryDoneType.Value switch
                {
                    DO.ProcessResult.Completed => OrderStatus.Delivered,
                    DO.ProcessResult.CustomerRefused => OrderStatus.CustomerRefused,
                    DO.ProcessResult.Cancelled => OrderStatus.Cancelled,
                    _ => OrderStatus.NotDelivered
                };

                scheduleStatus = lastDelivery.DeliveryDoneTime.Value <= maxDeliveryTime
                    ? ScheduleStatus.OnTime
                    : ScheduleStatus.Late;

                // Closed orders have no remaining time: show 00:00:00 rather than a
                // negative "Overdue" dump (only open-but-late orders show as Overdue).
                timeRemaining = TimeSpan.Zero;
            }

            var totalHandlingTime = lastDelivery != null && lastDelivery.DeliveryDoneTime.HasValue
                ? lastDelivery.DeliveryDoneTime.Value - o.OrderOpeningTime
                : AdminManager.Now - o.OrderOpeningTime;

            var airDistance = (companyLat == 0 && companyLon == 0)
                ? 0
                : OrderManager.CalculateDistance(companyLat, companyLon, o.Latitude, o.Longitude);

            return new OrderInList
            {
                DeliveryID = lastDelivery?.DeliveryID,
                OrderID = o.OrderID,
                orderType = (BO.OrderType)o.OrderType,
                AirDistance = Math.Round(airDistance, 2),
                orderStatus = orderStatus,
                scheduleStatus = scheduleStatus,
                TimeRemainingToComplete = timeRemaining,
                TotalHandlingTime = totalHandlingTime,
                TotalDeliveries = deliveries.Count()
            };
        });

        if (filterProperty.HasValue && filterValue != null)
        {
            result = filterProperty.Value switch
            {
                OrderListFilterProperty.Status => result.Where(x => x.orderStatus.ToString() == filterValue.ToString()),
                OrderListFilterProperty.ScheduleStatus => result.Where(x => x.scheduleStatus.ToString() == filterValue.ToString()),
                OrderListFilterProperty.OrderType => result.Where(x => x.orderType.ToString() == filterValue.ToString()),
                OrderListFilterProperty.DeliveryType => result.Where(x =>
                {
                    var last = DeliveryManager.GetLastDeliveryForOrder(x.OrderID);
                    return last?.DeliveryType != null
                        && last.DeliveryType.Value.ToString() == filterValue.ToString();
                }),
                _ => result
            };
        }

        if (sortProperty.HasValue)
        {
            result = sortProperty.Value switch
            {
                OrderListSortProperty.OrderId => result.OrderBy(x => x.OrderID),
                OrderListSortProperty.LastDeliveryId => result.OrderBy(x => x.DeliveryID ?? int.MaxValue),
                OrderListSortProperty.OrderType => result.OrderBy(x => x.orderType),
                OrderListSortProperty.Status => result.OrderBy(x => x.orderStatus),
                OrderListSortProperty.ScheduleStatus => result.OrderBy(x => x.scheduleStatus),
                OrderListSortProperty.TotalDeliveries => result.OrderBy(x => x.TotalDeliveries),
                OrderListSortProperty.TimeRemaining => result.OrderBy(x => x.TimeRemainingToComplete),
                OrderListSortProperty.TotalHandlingTime => result.OrderBy(x => x.TotalHandlingTime),
                _ => result
            };
        }

        return result;
    }

    public int[] GetOrdersSummary(string requesterId)
    {
        var orders = OrderManager.GetAllOrders();
        var summary = new int[Enum.GetValues(typeof(OrderStatus)).Length];

        foreach (var order in orders)
        {
            var lastDelivery = DeliveryManager.GetLastDeliveryForOrder(order.OrderID);

            if (lastDelivery != null && lastDelivery.DeliveryDoneTime.HasValue && lastDelivery.DeliveryDoneType.HasValue)
            {
                var status = lastDelivery.DeliveryDoneType.Value switch
                {
                    DO.ProcessResult.Completed => OrderStatus.Delivered,
                    DO.ProcessResult.CustomerRefused => OrderStatus.CustomerRefused,
                    DO.ProcessResult.Cancelled => OrderStatus.Cancelled,
                    _ => OrderStatus.NotDelivered
                };

                summary[(int)status]++;
            }
            else
            {
                summary[(int)OrderStatus.NotDelivered]++;
            }
        }

        return summary;
    }

    public void SelectOrderForHandling(string requesterId, string courierId, int orderId)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (!int.TryParse(courierId, out int courierIdInt))
            throw new BlInvalidIdException("Invalid courier ID format", null);

        if (!CourierManager.IsCourierExists(courierIdInt))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        if (!OrderManager.IsOrderExists(orderId))
            throw new BlItemNotFoundException($"Order with ID {orderId} not found", null);

        if (DeliveryManager.IsCourierCurrentlyDelivering(courierIdInt))
            throw new BlInvalidIdException($"Courier {courierId} is already handling another order", null);

        var lastDelivery = DeliveryManager.GetLastDeliveryForOrder(orderId);
        if (lastDelivery != null && lastDelivery.DeliveryDoneTime == null)
            throw new BlInvalidIdException($"Order {orderId} is already being delivered", null);

        var newDelivery = new DO.Delivery(
            DeliveryID: 0,
            OrderID: orderId,
            CourierID: courierIdInt,
            DeliveryType: DO.DeliveryType.Regular,
            DeliveryStartTime: AdminManager.Now,
            DeliveryDistance: null,
            DeliveryDoneType: null,
            DeliveryDoneTime: null
        );

        DeliveryManager.AddDelivery(newDelivery);
        OrderManager.Observers.NotifyItemUpdated(orderId);
        OrderManager.Observers.NotifyListUpdated();
    }

    public void UpdateOrder(string requesterId, Order order)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (order == null)
            throw new BlInvalidIdException("Order cannot be null", null);

        var existingOrder = OrderManager.GetOrderById(order.OrderID);
        if (existingOrder == null)
            throw new BlItemNotFoundException($"Order with ID {order.OrderID} not found", null);

        var updatedOrder = existingOrder with
        {
            Description = order.Description,
            FullAddress = order.FullAddress,
            CustomerName = order.CustomerFullName,
            CustomerPhone = order.CustomerPhone,
            OrderType = (DO.OrderType)order.OrderType,
            PizzaSize = (DO.DeviceType)order.PizzaSize
        };

        OrderManager.UpdateOrder(updatedOrder);
        OrderManager.Observers.NotifyItemUpdated(order.OrderID);
        OrderManager.Observers.NotifyListUpdated();
    }
    #region Stage 5
    public void AddObserver(Action listObserver) =>
        OrderManager.Observers.AddListObserver(listObserver); //stage 5
    public void AddObserver(int id, Action observer) =>
        OrderManager.Observers.AddObserver(id, observer); //stage 5
    public void RemoveObserver(Action listObserver) =>
        OrderManager.Observers.RemoveListObserver(listObserver); //stage 5
    public void RemoveObserver(int id, Action observer) =>
        OrderManager.Observers.RemoveObserver(id, observer); //stage 5
    #endregion Stage 5
}
