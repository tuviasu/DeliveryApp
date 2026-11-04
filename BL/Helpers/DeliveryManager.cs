namespace Helpers;
using DalApi;

/// <summary>
/// Manages delivery-related operations.
/// Provides methods for CRUD operations and business logic for deliveries.
/// </summary>
internal static class DeliveryManager
{
    internal static ObserverManager Observers = new();  //stage 5
    private static readonly IDal s_dal = Factory.Get;

    /// <summary>
    /// Gets a delivery by its ID.
    /// </summary>
    /// <param name="deliveryId">The ID of the delivery to retrieve.</param>
    /// <returns>The delivery object if found, null otherwise.</returns>
    internal static DO.Delivery? GetDeliveryById(int deliveryId)
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.Read(m => m.DeliveryID == deliveryId);
    }

    /// <summary>
    /// Gets all deliveries from the database.
    /// </summary>
    /// <returns>Collection of all deliveries.</returns>
    internal static IEnumerable<DO.Delivery> GetAllDeliveries()
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.ReadAll().ToList();
    }

    /// <summary>
    /// Gets deliveries that match the specified filter.
    /// </summary>
    /// <param name="filter">Filter function to apply on deliveries.</param>
    /// <returns>Collection of deliveries matching the filter.</returns>
    internal static IEnumerable<DO.Delivery> GetDeliveriesByFilter(Func<DO.Delivery, bool> filter)
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.ReadAll(filter).ToList();
    }

    /// <summary>
    /// Adds a new delivery to the database.
    /// </summary>
    /// <param name="delivery">The delivery object to add.</param>
    internal static void AddDelivery(DO.Delivery delivery)
    {
        lock (AdminManager.BlMutex) //stage 7
            s_dal.Delivery.Create(delivery);

        Observers.NotifyListUpdated(); //stage 5

        // Notify the courier that they now have a new delivery
        CourierManager.Observers.NotifyItemUpdated(delivery.CourierID);
        CourierManager.Observers.NotifyListUpdated();
    }

    /// <summary>
    /// Updates an existing delivery in the database.
    /// </summary>
    /// <param name="delivery">The delivery object with updated information.</param>
    internal static void UpdateDelivery(DO.Delivery delivery)
    {
        lock (AdminManager.BlMutex) //stage 7
            s_dal.Delivery.Update(delivery);

        Observers.NotifyListUpdated(); //stage 5

        // Notify the courier that their delivery status changed
        CourierManager.Observers.NotifyItemUpdated(delivery.CourierID);
        CourierManager.Observers.NotifyListUpdated();
    }

    /// <summary>
    /// Deletes a delivery from the database by its ID.
    /// </summary>
    /// <param name="deliveryId">The ID of the delivery to delete.</param>
    internal static void DeleteDelivery(int deliveryId)
    {
        DO.Delivery? delivery;
        lock (AdminManager.BlMutex) //stage 7
            delivery = s_dal.Delivery.Read(d => d.DeliveryID == deliveryId);

        int? courierId = delivery?.CourierID;

        lock (AdminManager.BlMutex) //stage 7
            s_dal.Delivery.Delete(deliveryId);

        Observers.NotifyItemUpdated(deliveryId);
        Observers.NotifyListUpdated(); //stage 5

        if (courierId.HasValue)
        {
            // Notify the courier that their delivery was removed
            CourierManager.Observers.NotifyItemUpdated(courierId.Value);
            CourierManager.Observers.NotifyListUpdated();
        }
    }

    /// <summary>
    /// Checks if a delivery exists in the database.
    /// </summary>
    /// <param name="deliveryId">The ID of the delivery to check.</param>
    /// <returns>True if the delivery exists, false otherwise.</returns>
    internal static bool IsDeliveryExists(int deliveryId)
    {
        try
        {
            lock (AdminManager.BlMutex) //stage 7
                s_dal.Delivery.Read(d => d.DeliveryID == deliveryId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current active (incomplete) delivery for a specific courier.
    /// </summary>
    /// <param name="courierId">The ID of the courier.</param>
    /// <returns>The active delivery if exists, null otherwise.</returns>
    internal static DO.Delivery? GetCurrentDeliveryForCourier(int courierId)
    {
        try
        {
            lock (AdminManager.BlMutex) //stage 7
                return s_dal.Delivery.Read(d => d.CourierID == courierId && d.DeliveryDoneTime == null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the most recent delivery for a specific order.
    /// </summary>
    /// <param name="orderId">The ID of the order.</param>
    /// <returns>The last delivery for the order, or null if no deliveries exist.</returns>
    internal static DO.Delivery? GetLastDeliveryForOrder(int orderId)
    {
        List<DO.Delivery> deliveries;
        lock (AdminManager.BlMutex) //stage 7
            deliveries = s_dal.Delivery.ReadAll(d => d.OrderID == orderId).ToList();
        return deliveries.OrderByDescending(d => d.DeliveryStartTime).FirstOrDefault();
    }

    /// <summary>
    /// Gets all deliveries assigned to a specific courier.
    /// </summary>
    /// <param name="courierId">The ID of the courier.</param>
    /// <returns>Collection of deliveries for the courier.</returns>
    internal static IEnumerable<DO.Delivery> GetDeliveriesByCourier(int courierId)
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.ReadAll(d => d.CourierID == courierId).ToList();
    }

    /// <summary>
    /// Gets all deliveries for a specific order.
    /// </summary>
    /// <param name="orderId">The ID of the order.</param>
    /// <returns>Collection of deliveries for the order.</returns>
    internal static IEnumerable<DO.Delivery> GetDeliveriesByOrder(int orderId)
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.ReadAll(d => d.OrderID == orderId).ToList();
    }

    /// <summary>
    /// Gets all completed deliveries (deliveries with a done time).
    /// </summary>
    /// <returns>Collection of completed deliveries.</returns>
    internal static IEnumerable<DO.Delivery> GetCompletedDeliveries()
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.ReadAll(d => d.DeliveryDoneTime != null).ToList();
    }

    /// <summary>
    /// Gets all active deliveries (deliveries without a done time).
    /// </summary>
    /// <returns>Collection of active deliveries.</returns>
    internal static IEnumerable<DO.Delivery> GetActiveDeliveries()
    {
        lock (AdminManager.BlMutex) //stage 7
            return s_dal.Delivery.ReadAll(d => d.DeliveryDoneTime == null).ToList();
    }

    /// <summary>
    /// Checks if a courier is currently delivering an order.
    /// </summary>
    /// <param name="courierId">The ID of the courier.</param>
    /// <returns>True if the courier has an active delivery, false otherwise.</returns>
    internal static bool IsCourierCurrentlyDelivering(int courierId)
    {
        return GetCurrentDeliveryForCourier(courierId) != null;
    }

    /// <summary>
    /// Checks if an order is currently being delivered.
    /// </summary>
    /// <param name="orderId">The ID of the order.</param>
    /// <returns>True if the order has an active delivery, false otherwise.</returns>
    internal static bool IsOrderCurrentlyBeingDelivered(int orderId)
    {
        var lastDelivery = GetLastDeliveryForOrder(orderId);
        return lastDelivery != null && lastDelivery.DeliveryDoneTime == null;
    }

    /// <summary>
    /// Counts the number of deliveries for a specific courier, optionally with a filter.
    /// </summary>
    /// <param name="courierId">The ID of the courier.</param>
    /// <param name="filter">Optional filter to apply on the deliveries.</param>
    /// <returns>Count of deliveries matching the criteria.</returns>
    internal static int CountDeliveriesByCourier(int courierId, Func<DO.Delivery, bool>? filter = null)
    {
        var deliveries = GetDeliveriesByCourier(courierId);
        return filter != null ? deliveries.Count(filter) : deliveries.Count();
    }

    #region Order availability / assignment

    /// <summary>
    /// Whether an order can be handed to a courier, derived from its delivery attempts.
    /// </summary>
    internal enum OrderAvailability
    {
        /// <summary>No attempt yet, or every attempt ended Failed / CustomerNotFound.</summary>
        Available,
        /// <summary>An attempt is still running (no done time) - nobody else may take it.</summary>
        InProgress,
        /// <summary>An attempt ended Completed - the order is closed.</summary>
        Delivered,
        /// <summary>An attempt ended CustomerRefused - the order is closed.</summary>
        CustomerRefused,
        /// <summary>The order was cancelled - it must never be re-assigned.</summary>
        Cancelled
    }

    /// <summary>
    /// Single source of truth for "is this order open for a courier?".
    /// Looks at ALL attempts (not only the last one), so a delivered or cancelled order can
    /// never become available again, regardless of how attempts are ordered by time or ID.
    /// </summary>
    internal static OrderAvailability GetOrderAvailability(IEnumerable<DO.Delivery> deliveriesOfOrder)
    {
        var list = deliveriesOfOrder as IReadOnlyCollection<DO.Delivery> ?? deliveriesOfOrder.ToList();

        if (list.Any(d => d.DeliveryDoneTime == null))
            return OrderAvailability.InProgress;
        if (list.Any(d => d.DeliveryDoneType == DO.ProcessResult.Cancelled))
            return OrderAvailability.Cancelled;
        if (list.Any(d => d.DeliveryDoneType == DO.ProcessResult.Completed))
            return OrderAvailability.Delivered;
        if (list.Any(d => d.DeliveryDoneType == DO.ProcessResult.CustomerRefused))
            return OrderAvailability.CustomerRefused;

        // No attempts, or only Failed / CustomerNotFound (or a closed attempt with no result) -> may be retried.
        return OrderAvailability.Available;
    }

    /// <summary>
    /// Atomically checks that the courier is free and the order is available, and creates the
    /// new delivery attempt - all under one lock, so two couriers can never take the same order
    /// (and one courier can never take two orders) even when requests arrive concurrently.
    /// Previous attempts are never modified, so the full attempt history is preserved.
    /// </summary>
    /// <param name="failureReason">Why the assignment was refused, when it returns false.</param>
    /// <returns>true if the delivery was created.</returns>
    internal static bool TryAssignOrderToCourier(int orderId, int courierId, out string? failureReason)
    {
        lock (AdminManager.BlMutex) //stage 7
        {
            if (s_dal.Delivery.ReadAll(d => d.CourierID == courierId && d.DeliveryDoneTime == null).Any())
            {
                failureReason = $"Courier {courierId} is already handling another order";
                return false;
            }

            var availability = GetOrderAvailability(s_dal.Delivery.ReadAll(d => d.OrderID == orderId).ToList());
            if (availability != OrderAvailability.Available)
            {
                failureReason = availability switch
                {
                    OrderAvailability.InProgress => $"Order {orderId} is already being delivered by another courier",
                    OrderAvailability.Delivered => $"Order {orderId} was already delivered and is closed",
                    OrderAvailability.CustomerRefused => $"Order {orderId} was refused by the customer and is closed",
                    OrderAvailability.Cancelled => $"Order {orderId} was cancelled and cannot be assigned",
                    _ => $"Order {orderId} is not available"
                };
                return false;
            }

            s_dal.Delivery.Create(new DO.Delivery(
                DeliveryID: 0,
                OrderID: orderId,
                CourierID: courierId,
                DeliveryType: DO.DeliveryType.Regular,
                DeliveryStartTime: AdminManager.Now,
                DeliveryDistance: null,
                DeliveryDoneType: null,
                DeliveryDoneTime: null));
        }

        failureReason = null;

        // Notify outside the lock (observers may call back into the BL).
        Observers.NotifyListUpdated(); //stage 5
        CourierManager.Observers.NotifyItemUpdated(courierId);
        CourierManager.Observers.NotifyListUpdated();
        return true;
    }

    #endregion
}
