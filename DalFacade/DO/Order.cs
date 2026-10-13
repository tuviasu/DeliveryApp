namespace DO;

/// <summary>
/// Creates a new order object with all required details
/// </summary>
/// <param name="OrderID">Unique identifier of the order</param>
/// <param name="Description">Description or additional notes for the order</param>
/// <param name="FullAddress">Full delivery address</param>
/// <param name="Latitude">Latitude coordinate of the address (for map location)</param>
/// <param name="Longitude">Longitude coordinate of the address (for map location)</param>
/// <param name="CustomerName">Name of the customer who placed the order</param>
/// <param name="CustomerPhone">Phone number of the customer</param>
/// <param name="OrderType">Type of the order (e.g., delivery, pickup, etc.)</param>
/// <param name="DeliveryTransport">Type of delivery transport (e.g., car, motorcycle, walking)</param>
/// <param name="OrderOpeningTime">The date and time when the order was created</param>

public record Order
(
    int OrderID,
    string? Description ,
    string? FullAddress,
    double Latitude,
    double Longitude,
    string? CustomerName,
    string? CustomerPhone,
    OrderType OrderType,
    DeviceType PizzaSize,
    DateTime OrderOpeningTime,
    DateTime? MaxDeliveryTime = null
)
{
public Order():this(0, null, null, 0.0, 0.0, null, null, OrderType.Individual, DeviceType.Desktop, DateTime.MinValue) { }
}
