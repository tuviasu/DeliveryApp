namespace BlApi;

/// <summary>
/// Service interface for Order entity business logic.
/// Provides operations for order management, retrieval, and processing.
/// </summary>
public interface IOrder: IObservable
{
    /// <summary>
    /// Gets a summary count of orders by status.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <returns>Array of order counts by status</returns>
    int[] GetOrdersSummary(string requesterId);

    /// <summary>
    /// Gets a filtered and sorted list of orders.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="filterProperty">Property to filter by (nullable)</param>
    /// <param name="filterValue">Value to filter by (nullable)</param>
    /// <param name="sortProperty">Property to sort by (nullable)</param>
    /// <returns>Collection of OrderInList</returns>
    IEnumerable<BO.OrderInList> GetOrdersList(string requesterId, BO.OrderListFilterProperty? filterProperty, object? filterValue, BO.OrderListSortProperty? sortProperty);

    /// <summary>
    /// Gets details of a specific order.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="orderId">Order ID</param>
    /// <returns>Order details</returns>
    BO.Order GetOrderDetails(string requesterId, int orderId);

    /// <summary>
    /// Updates order details.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="order">Order object to update</param>
    void UpdateOrder(string requesterId, BO.Order order);

    /// <summary>
    /// Cancels an order.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="orderId">Order ID to cancel</param>
    void CancelOrder(string requesterId, int orderId);

    /// <summary>
    /// Deletes an order (not allowed in the system, throws exception).
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="orderId">Order ID</param>
    void DeleteOrder(string requesterId, int orderId);

    /// <summary>
    /// Gets the next available sequential order ID.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <returns>Next available order ID</returns>
    int GetNextOrderId(string requesterId);

    /// <summary>
    /// Adds a new order to the system.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="order">New order to add</param>
    void AddOrder(string requesterId, BO.Order order);

    /// <summary>
    /// Completes handling of an order by a courier.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="courierId">Courier ID</param>
    /// <param name="deliveryId">Delivery ID to complete</param>
    void CompleteOrderHandling(string requesterId, string courierId, int deliveryId);

    /// <summary>
    /// Selects an order for handling by a courier.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="courierId">Courier ID</param>
    /// <param name="orderId">Order ID to handle</param>
    void SelectOrderForHandling(string requesterId, string courierId, int orderId);

    /// <summary>
    /// Gets a list of closed orders handled by a courier.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="courierId">Courier ID</param>
    /// <param name="orderTypeFilter">Order type filter (nullable)</param>
    /// <param name="sortProperty">Property to sort by (nullable)</param>
    /// <returns>Collection of ClosedDeliveryInList</returns>
    IEnumerable<BO.ClosedDeliveryInList> GetClosedOrdersByCourier(string requesterId, string courierId, BO.OrderType? orderTypeFilter, BO.ClosedDeliveryListSortProperty? sortProperty);

    /// <summary>
    /// Gets a list of open orders available for selection by a courier.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="courierId">Courier ID</param>
    /// <param name="orderTypeFilter">Order type filter (nullable)</param>
    /// <param name="sortProperty">Property to sort by (nullable)</param>
    /// <returns>Collection of OpenOrderInList</returns>
    IEnumerable<BO.OpenOrderInList> GetOpenOrdersForCourier(string requesterId, string courierId, BO.OrderType? orderTypeFilter, BO.OpenOrderListSortProperty? sortProperty);

    /// <summary>
    /// Gets a list of open orders available for selection by a courier (async).
    /// Intended for UI list screens that should stay responsive while network distances are calculated.
    /// </summary>
    /// <param name="requesterId">ID of the requester</param>
    /// <param name="courierId">Courier ID</param>
    /// <param name="orderTypeFilter">Order type filter (nullable)</param>
    /// <param name="sortProperty">Property to sort by (nullable)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of OpenOrderInList</returns>
    Task<IEnumerable<BO.OpenOrderInList>> GetOpenOrdersForCourierAsync(
        string requesterId,
        string courierId,
        BO.OrderType? orderTypeFilter,
        BO.OpenOrderListSortProperty? sortProperty,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams open orders one-by-one (Appendix 9).
    /// Each item is produced after its network calculations complete, enabling per-item UI updates.
    /// </summary>
    IAsyncEnumerable<BO.OpenOrderInList> StreamOpenOrdersForCourierAsync(
        string requesterId,
        string courierId,
        BO.OrderType? orderTypeFilter,
        BO.OpenOrderListSortProperty? sortProperty,
        CancellationToken cancellationToken = default);
}
