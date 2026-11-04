namespace Helpers;
using DalApi;

/// <summary>
/// Manages courier-related operations.
/// </summary>

internal static class CourierManager
{
    internal static ObserverManager Observers = new();  //stage 5
    private static readonly IDal s_dal = Factory.Get;

    private static readonly Random s_rand = new();
    private static readonly AsyncMutex s_simulationMutex = new(); //stage 7

    /// <summary>
    /// Gets a courier by their ID.
    /// </summary>
    /// <param name="courierId"></param>
    /// <returns></returns>
    internal static DO.Courier? GetCourierById(int courierId)
    {
        if (courierId <= 0)
            return null;
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Courier.Read(courierId);
    }

    /// <summary>
    /// Gets all couriers.
    /// </summary>
    /// <returns></returns>
    internal static IEnumerable<DO.Courier> GetAllCouriers()
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Courier.ReadAll().ToList();
    }

    /// <summary>
    /// Gets couriers by a specified filter.
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    internal static IEnumerable<DO.Courier> GetCouriersByFilter(Func<DO.Courier, bool> filter)
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Courier.ReadAll(filter).ToList();
    }

    /// <summary>
    /// Adds a new courier.
    /// </summary>
    /// <param name="courier"></param>
    internal static void AddCourier(DO.Courier courier)
    {
        lock (AdminManager.BlMutex) //stage 7
            s_dal.Courier.Create(courier);
        Observers.NotifyListUpdated(); //stage 5
    }

    /// <summary>
    /// Updates an existing courier.
    /// </summary>
    /// <param name="courier"></param>
    internal static void UpdateCourier(DO.Courier courier)
    {
        lock (AdminManager.BlMutex) //stage 7
            s_dal.Courier.Update(courier);
        Observers.NotifyItemUpdated(courier.CourierID);
        Observers.NotifyListUpdated(); //stage 5
    }

    /// <summary>
    /// Deletes a courier by their ID.
    /// </summary>
    /// <param name="courierId"></param>
    internal static void DeleteCourier(int courierId)
    {
        lock (AdminManager.BlMutex) //stage 7
            s_dal.Courier.Delete(courierId);
        Observers.NotifyItemUpdated(courierId);
        Observers.NotifyListUpdated(); //stage 5
    }

    /// <summary>
    /// Checks if a courier exists by their ID.
    /// </summary>
    /// <param name="courierId"></param>
    /// <returns></returns>
    internal static bool IsCourierExists(int courierId)
    {
        try
        {
            lock (AdminManager.BlMutex) //stage 7
                s_dal.Courier.Read(courierId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static async Task SimulateCouriersActivityAsync() //stage 7
    {
        // If the previous simulation is still in progress, exit immediately
        if (s_simulationMutex.CheckAndSetInProgress())
            return;

        try
        {
            List<DO.Courier> activeCouriers;
            lock (AdminManager.BlMutex) //stage 7
                activeCouriers = s_dal.Courier.ReadAll(c => c.IsActive).ToList();

            var cfg = AdminManager.GetConfig();

            var courierIdsToNotify = new HashSet<int>();
            var orderIdsToNotify = new HashSet<int>();
            bool deliveriesListChanged = false;

            // Use BL public API (avoid instantiating implementation classes directly)
            var bl = BlApi.Factory.Get();

            foreach (var courier in activeCouriers)
            {
                var currentDelivery = DeliveryManager.GetCurrentDeliveryForCourier(courier.CourierID);

                if (currentDelivery is null)
                {
                    if (s_rand.NextDouble() > 0.15)
                        continue;

                    var openOrders = (await bl.Order.GetOpenOrdersForCourierAsync(
                        "0",
                        courier.CourierID.ToString(),
                        null,
                        null))
                        .ToList();

                    if (openOrders.Count == 0)
                        continue;

                    if (s_rand.NextDouble() > 0.50)
                        continue;

                    var chosen = openOrders[s_rand.Next(openOrders.Count)];
                    int chosenOrderId = chosen.OrderID;

                    // Re-validate and create atomically via the same rule used by manual selection,
                    // so the simulator can never assign an in-progress, delivered or cancelled order.
                    bool created = DeliveryManager.TryAssignOrderToCourier(chosenOrderId, courier.CourierID, out _);

                    if (created)
                    {
                        deliveriesListChanged = true;
                        courierIdsToNotify.Add(courier.CourierID);
                        orderIdsToNotify.Add(chosenOrderId);
                    }
                }
                else
                {
                    var baseMinutes = cfg.MaxDeliveryTimeRange.TotalMinutes;
                    var targetMinutes = baseMinutes * (0.5 + s_rand.NextDouble() * 0.75);
                    var elapsed = (AdminManager.Now - currentDelivery.DeliveryStartTime).TotalMinutes;

                    if (elapsed >= targetMinutes)
                    {
                        var roll = s_rand.NextDouble();
                        DO.ProcessResult doneType = roll < 0.80
                            ? DO.ProcessResult.Completed
                            : roll < 0.90
                                ? DO.ProcessResult.CustomerNotFound
                                : DO.ProcessResult.CustomerRefused;

                        var updated = currentDelivery with
                        {
                            DeliveryDoneType = doneType,
                            DeliveryDoneTime = AdminManager.Now,
                            DeliveryDistance = currentDelivery.DeliveryDistance ?? 0
                        };

                        lock (AdminManager.BlMutex) //stage 7
                            s_dal.Delivery.Update(updated);

                        deliveriesListChanged = true;
                        courierIdsToNotify.Add(updated.CourierID);
                        orderIdsToNotify.Add(updated.OrderID);
                    }
                    else
                    {
                        if (s_rand.NextDouble() > 0.10)
                            continue;

                        var updated = currentDelivery with
                        {
                            DeliveryDoneType = DO.ProcessResult.Cancelled,
                            DeliveryDoneTime = AdminManager.Now,
                            DeliveryDistance = currentDelivery.DeliveryDistance ?? 0
                        };

                        lock (AdminManager.BlMutex) //stage 7
                            s_dal.Delivery.Update(updated);

                        deliveriesListChanged = true;
                        courierIdsToNotify.Add(updated.CourierID);
                        orderIdsToNotify.Add(updated.OrderID);
                    }
                }
            }

            foreach (var id in courierIdsToNotify)
                Observers.NotifyItemUpdated(id);
            if (courierIdsToNotify.Count > 0)
                Observers.NotifyListUpdated();

            foreach (var id in orderIdsToNotify)
                OrderManager.Observers.NotifyItemUpdated(id);
            if (orderIdsToNotify.Count > 0)
                OrderManager.Observers.NotifyListUpdated();

            if (deliveriesListChanged)
                DeliveryManager.Observers.NotifyListUpdated();
        }
        finally
        {
            s_simulationMutex.UnsetInProgress();
        }
    }

    /// <summary>
    /// Checks if a courier is active by their ID.
    /// </summary>
    /// <param name="courierId"></param>
    /// <returns></returns>
    internal static bool IsCourierActive(int courierId)
    {
        var courier = GetCourierById(courierId);
        return courier.IsActive;
    }

    /// <summary>
    /// Gets the maximum delivery range of a courier by their ID.
    /// </summary>
    /// <param name="courierId"></param>
    /// <returns></returns>
    internal static double? GetCourierMaxRange(int courierId)
    {
        var courier = GetCourierById(courierId);
        return courier.MaxDeliveryDistanceKm;
    }

    /// <summary>
    /// Validates courier credentials.
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    internal static bool ValidateCourierCredentials(string username, string password)
    {
        try
        {
            string hashedInput = Tools.HashPassword(password);

            // 1) Fixed courier credentials stored in config (stored as plaintext in config dict)
            if (int.TryParse(username, out int configCourierId))
            {
                lock (AdminManager.BlMutex) //stage 7
                {
                    if (s_dal.Config.Couriers.TryGetValue(configCourierId, out var configPwd)
                        && configPwd == password) // config passwords are not hashed
                        return true;
                }
            }

            // 2) Regular couriers — passwords stored as SHA-256 hashes
            if (!int.TryParse(username, out int courierId))
                return false;

            var courier = GetCourierById(courierId);
            // Accept both hashed (BL-created) and plaintext (seeded via DalTest) stored passwords,
            // so couriers initialized directly in the DAL can still log in.
            return courier != null
                && (courier.Password == hashedInput || courier.Password == password);
        }
        catch
        {
            return false;
        }
    }
}
