namespace BlImplementation;
using BlApi;
using BO;
using Helpers;

internal class CourierImplementation : ICourier
{
    public void AddCourier(int requesterId, BO.Courier courier)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (courier == null)
            throw new BlInvalidIdException("Courier cannot be null", null);

        if (courier.CourierID <= 0)
            throw new BlInvalidIdException($"Invalid courier ID: {courier.CourierID}", null);

        if (CourierManager.IsCourierExists(courier.CourierID))
            throw new BlItemAlreadyExistsException($"Courier with ID {courier.CourierID} already exists", null);

        PasswordValidator.ValidateOrThrow(courier.Password);

        var email = string.IsNullOrWhiteSpace(courier.Email)
            ? $"courier.{courier.CourierID}@gmail.com"
            : courier.Email!;

        var doCourier = new DO.Courier(
            CourierID: courier.CourierID,
            FullName: courier.FullName ?? "",
            Phone: courier.Phone ?? "",
            Email: email,
            StartWorkInCompany: courier.StartWorkInCompany ?? AdminManager.Now,
            TransportType: (DO.DeliveryTransport)courier.TransportType,
            DeliveryType: courier.DeliveryType.HasValue ? (DO.DeliveryType?)courier.DeliveryType.Value : null,
            Password: Helpers.Tools.HashPassword(courier.Password!),
            IsActive: courier.IsActive,
            MaxDeliveryDistanceKm: courier.MaxDeliveryDistanceKM,
            OrderInProgress: null
        );

        CourierManager.AddCourier(doCourier);
    }

    public string AuthenticateCourier(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BlInvalidIdException("Username cannot be empty", null);

        PasswordValidator.ValidateOrThrow(password);

        bool isValid = CourierManager.ValidateCourierCredentials(username, password);

        if (!isValid)
            throw new BlItemNotFoundException("Courier not found or password incorrect", null);

        if (username == "admin" || username == "manager")
            return "Manager";

        return "Courier";
    }

    public void DeleteCourier(int requesterId, int courierId)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (!CourierManager.IsCourierExists(courierId))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        var deliveries = DeliveryManager.GetDeliveriesByCourier(courierId);

        if (deliveries.Any())
        {
            var activeDelivery = deliveries.FirstOrDefault(d => d.DeliveryDoneTime == null);
            if (activeDelivery != null)
                throw new BlInvalidIdException($"Cannot delete courier {courierId} - has active delivery", null);
        }

        CourierManager.DeleteCourier(courierId);
    }

    public BO.Courier GetCourierDetails(int requesterId, int courierId)
    {
        if (!CourierManager.IsCourierExists(courierId))
            throw new BlItemNotFoundException($"Courier with ID {courierId} not found", null);

        var doCourier = CourierManager.GetCourierById(courierId);

        var deliveries = DeliveryManager.GetDeliveriesByCourier(courierId);
        var config = AdminManager.GetConfig();

        int deliveredOnTime = 0;
        int deliveredLate = 0;

        foreach (var delivery in deliveries.Where(d => d.DeliveryDoneTime.HasValue))
        {
            var order = OrderManager.GetOrderById(delivery.OrderID);
            if (order == null) continue;

            var actualTime = delivery.DeliveryDoneTime!.Value - order.OrderOpeningTime;
            if (actualTime <= config.MaxDeliveryTimeRange)
                deliveredOnTime++;
            else
                deliveredLate++;
        }

        var currentDelivery = DeliveryManager.GetCurrentDeliveryForCourier(courierId);
        BO.OrderInProgress? orderInProgress = null;

        if (currentDelivery != null)
        {
            var order = OrderManager.GetOrderById(currentDelivery.OrderID);
            if (order != null)
            {
                double airDistance = 0;

                var maxDeliveryTime = order.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
                var timeRemaining = maxDeliveryTime - AdminManager.Now;
                var scheduleStatus = timeRemaining < config.RiskRange
                    ? ScheduleStatus.Late
                    : ScheduleStatus.OnTime;

                orderInProgress = new BO.OrderInProgress
                {
                    DeliveryID = currentDelivery.DeliveryID,
                    OrderID = order.OrderID,
                    orderType = (BO.OrderType)order.OrderType,
                    Description = order.Description,
                    FullAddress = order.FullAddress ?? "",
                    AirDistanceKM = airDistance,
                    RealDistance = currentDelivery.DeliveryDistance,
                    InviterFullName = order.CustomerName,
                    InviterPhone = order.CustomerPhone ?? "",
                    OrderOpeningTime = order.OrderOpeningTime,
                    DeliveryStartTime = currentDelivery.DeliveryStartTime,
                    EstimatedDeliveryTime = currentDelivery.DeliveryStartTime.Add(config.MaxDeliveryTimeRange),
                    MaxDeliveryTime = maxDeliveryTime,
                    orderStatus = OrderStatus.NotDelivered,
                    scheduleStatus = scheduleStatus,
                    TotalTimeToCompleteAnOrder = AdminManager.Now - order.OrderOpeningTime
                };
            }
        }

        return new BO.Courier
        {
            CourierID = doCourier.CourierID,
            FullName = doCourier.FullName,
            Phone = doCourier.Phone,
            Email = doCourier.Email,
            StartWorkInCompany = doCourier.StartWorkInCompany,
            TransportType = (BO.DeliveryTransport)doCourier.TransportType,
            DeliveryType = doCourier.DeliveryType.HasValue ? (BO.DeliveryType?)doCourier.DeliveryType.Value : null,
            Password = null,
            IsActive = doCourier.IsActive,
            MaxDeliveryDistanceKM = doCourier.MaxDeliveryDistanceKm,
            DeliveredInTime = deliveredOnTime,
            DeliveredNotInTime = deliveredLate,
            orderInProgress = orderInProgress
        };
    }

    public IEnumerable<CourierInList> GetCouriersList(int requesterId, bool? isActive = null, string? sortBy = null)
    {
        var couriers = isActive.HasValue
            ? CourierManager.GetCouriersByFilter(c => c.IsActive == isActive.Value)
            : CourierManager.GetAllCouriers();

        var config = AdminManager.GetConfig();

        var result = couriers.Select(c =>
        {
            // Check for delivery in progress from the system
            var currentDelivery = DeliveryManager.GetCurrentDeliveryForCourier(c.CourierID);
            BO.OrderInProgress? orderInProgress = null;

            // Helper variable for delivery type
            BO.DeliveryType? calculatedDeliveryType = null;

            // If there is an active delivery in the system
            if (currentDelivery != null)
            {
                var order = OrderManager.GetOrderById(currentDelivery.OrderID);
                if (order != null)
                {
                    var maxDeliveryTime = order.OrderOpeningTime.Add(config.MaxDeliveryTimeRange);
                    var timeRemaining = maxDeliveryTime - AdminManager.Now;
                    var scheduleStatus = timeRemaining < config.RiskRange
                        ? ScheduleStatus.Late
                        : ScheduleStatus.OnTime;

                    orderInProgress = new BO.OrderInProgress
                    {
                        DeliveryID = currentDelivery.DeliveryID,
                        OrderID = order.OrderID,
                        orderType = (BO.OrderType)order.OrderType,
                        Description = order.Description,
                        FullAddress = order.FullAddress ?? "",
                        AirDistanceKM = 0,
                        RealDistance = currentDelivery.DeliveryDistance,
                        InviterFullName = order.CustomerName,
                        InviterPhone = order.CustomerPhone ?? "",
                        OrderOpeningTime = order.OrderOpeningTime,
                        DeliveryStartTime = currentDelivery.DeliveryStartTime,
                        EstimatedDeliveryTime = currentDelivery.DeliveryStartTime.Add(config.MaxDeliveryTimeRange),
                        MaxDeliveryTime = maxDeliveryTime,
                        orderStatus = OrderStatus.NotDelivered,
                        scheduleStatus = scheduleStatus,
                        TotalTimeToCompleteAnOrder = AdminManager.Now - order.OrderOpeningTime
                    };
                }

                // Save the delivery type of the current order for later use if needed
                if (currentDelivery.DeliveryType.HasValue)
                    calculatedDeliveryType = (BO.DeliveryType)currentDelivery.DeliveryType.Value;
            }

            // Priority for manual change: if the user entered an ID manually, we'll override what we found (or fill if we didn't find)
            if (!string.IsNullOrEmpty(c.OrderInProgress) && int.TryParse(c.OrderInProgress, out int savedOrderId))
            {
                // Check if the manual ID is different from the real ID (Override)
                if (orderInProgress == null || orderInProgress.OrderID != savedOrderId)
                {
                    orderInProgress = new BO.OrderInProgress
                    {
                        OrderID = savedOrderId,
                        DeliveryID = 0,
                        Description = "Manual Assignment",
                        orderStatus = BO.OrderStatus.Delivered,
                        FullAddress = "Manual Override",
                        InviterPhone = "",
                        OrderOpeningTime = AdminManager.Now,
                        DeliveryStartTime = AdminManager.Now,
                        EstimatedDeliveryTime = AdminManager.Now.AddHours(1),
                        MaxDeliveryTime = AdminManager.Now.AddHours(2)
                    };
                }
            }

            // Final calculation of DeliveryType:
            // 1. First check if the courier itself has a setting (what you edited in the window)
            // 2. If not, check what type of delivery they are currently performing
            var finalDeliveryType = c.DeliveryType.HasValue
                ? (BO.DeliveryType?)c.DeliveryType.Value
                : calculatedDeliveryType;

            return new CourierInList
            {
                CourierID = c.CourierID,
                FullName = c.FullName,
                Phone = c.Phone,
                Email = c.Email,
                StartWorkInCompany = c.StartWorkInCompany,
                TransportType = (BO.DeliveryTransport)c.TransportType,
                Password = null,
                IsActive = c.IsActive,
                MaxDeliveryDistanceKM = c.MaxDeliveryDistanceKm,

                DeliveryType = finalDeliveryType,

                DeliveredInTime = c.DeliveredInTime,
                DeliveredNotInTime = c.DeliveredNotInTime,
                orderInProgress = orderInProgress
            };
        });

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            result = sortBy.ToLower() switch
            {
                "id" => result.OrderBy(c => c.CourierID),
                "name" => result.OrderBy(c => c.FullName),
                "ontime" => result.OrderByDescending(c => c.DeliveredInTime),
                "late" => result.OrderByDescending(c => c.DeliveredNotInTime),
                _ => result.OrderBy(c => c.CourierID)
            };
        }

        return result;
    }

    public void UpdateCourier(int requesterId, BO.Courier courier)
    {
        AdminManager.ThrowOnSimulatorIsRunning(); //stage 7
        if (courier == null)
            throw new BlInvalidIdException("Courier cannot be null", null);

        if (!CourierManager.IsCourierExists(courier.CourierID))
            throw new BlItemNotFoundException($"Courier with ID {courier.CourierID} not found", null);

        var existingCourier = CourierManager.GetCourierById(courier.CourierID);

        // manager (requesterId==0) is allowed to change active flag; courier is not.
        bool requesterIsManager = requesterId == 0;
        var effectiveIsActive = requesterIsManager ? courier.IsActive : existingCourier.IsActive;

        var cfg = AdminManager.GetConfig();
        if (cfg.MaxDeliveryDistance.HasValue && courier.MaxDeliveryDistanceKM.HasValue &&
            courier.MaxDeliveryDistanceKM.Value > cfg.MaxDeliveryDistance.Value)
        {
            throw new BlInvalidIdException(
                $"Max distance must be <= company max distance ({cfg.MaxDeliveryDistance.Value}).", null);
        }

        var currentDelivery = DeliveryManager.GetCurrentDeliveryForCourier(courier.CourierID);
        bool hasOrderInProgress = currentDelivery != null;

        // Couriers cannot change transport/delivery type while an order is in progress.
        // Managers may override (if you want to restrict managers too, change this to always enforce like courier).
        var effectiveTransport = (!requesterIsManager && hasOrderInProgress)
            ? existingCourier.TransportType
            : (DO.DeliveryTransport)courier.TransportType;

        var effectiveDeliveryType = (!requesterIsManager && hasOrderInProgress)
            ? existingCourier.DeliveryType
            : (courier.DeliveryType.HasValue ? (DO.DeliveryType?)courier.DeliveryType.Value : existingCourier.DeliveryType);

        if (!string.IsNullOrWhiteSpace(courier.Password) && courier.Password != existingCourier.Password)
        {
            PasswordValidator.ValidateOrThrow(courier.Password);
        }

        var updatedCourier = existingCourier with
        {
            FullName = courier.FullName ?? existingCourier.FullName,
            Phone = courier.Phone ?? existingCourier.Phone,
            Email = courier.Email ?? existingCourier.Email,
            TransportType = effectiveTransport,
            DeliveryType = effectiveDeliveryType,
            Password = !string.IsNullOrWhiteSpace(courier.Password)
                ? Helpers.Tools.HashPassword(courier.Password!)
                : existingCourier.Password,
            IsActive = effectiveIsActive,
            MaxDeliveryDistanceKm = courier.MaxDeliveryDistanceKM ?? existingCourier.MaxDeliveryDistanceKm,
            OrderInProgress = existingCourier.OrderInProgress,
            DeliveredInTime = existingCourier.DeliveredInTime,
            DeliveredNotInTime = existingCourier.DeliveredNotInTime
        };

        CourierManager.UpdateCourier(updatedCourier);

        CourierManager.Observers.NotifyItemUpdated(updatedCourier.CourierID);
        CourierManager.Observers.NotifyListUpdated();
    }

    #region Stage 5
    public void AddObserver(Action listObserver) =>
        CourierManager.Observers.AddListObserver(listObserver); //stage 5
    public void AddObserver(int id, Action observer) =>
        CourierManager.Observers.AddObserver(id, observer); //stage 5
    public void RemoveObserver(Action listObserver) =>
        CourierManager.Observers.RemoveListObserver(listObserver); //stage 5
    public void RemoveObserver(int id, Action observer) =>
        CourierManager.Observers.RemoveObserver(id, observer); //stage 5
    #endregion Stage 5
}
