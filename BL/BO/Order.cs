using Helpers;

namespace BO;

public class Order
{
    /// <summary>
    /// Unique identifier for the order
    /// </summary>
    public int OrderID { get; init; }

    /// <summary>
    /// Type of the order (e.g., single, group, etc.)
    /// </summary>
    public OrderType OrderType { get; set; }

    /// <summary>
    /// Description of the order
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Full delivery address
    /// </summary>
    public string FullAddress { get; set; }

    /// <summary>
    /// Latitude of the delivery location
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Longitude of the delivery location
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Straight-line distance to the delivery address
    /// </summary>
    public double AirDistance { get; init; }

    /// <summary>
    /// Full name of the customer
    /// </summary>
    public string CustomerFullName { get; set; }

    /// <summary>
    /// Phone number of the customer
    /// </summary>
    public string CustomerPhone { get; set; }

    /// <summary>
    /// Size of the pizza (e.g., small, medium, large)
    /// </summary>
    public DeviceType PizzaSize { get; set; }

    /// <summary>
    /// Time when the order was opened
    /// </summary>
    public DateTime OrderOpenTime { get; init; }

    /// <summary>
    /// Estimated delivery time
    /// </summary>
    public DateTime? EstimatedDeliveryTime { get; init; }

    /// <summary>
    /// Maximum allowed delivery time
    /// </summary>
    public DateTime MaxDeliveryTime { get; set; }

    /// <summary>
    /// Current status of the order
    /// </summary>
    public OrderStatus OrderStatus { get; init; }

    /// <summary>
    /// Status of the delivery schedule (e.g., on time, delayed, etc.)
    /// </summary>
    public ScheduleStatus ScheduleStatus { get; init; }

    /// <summary>
    /// Time remaining to complete the delivery
    /// </summary>
    public TimeSpan TimeRemainingToComplete { get; init; }

    /// <summary>
    /// List of deliveries associated with the order
    /// </summary>
    public List<DeliveryPerOrderInList>? DeliveriesForOrder { get; init; }

    public override string ToString() => this.ToStringProperty();
}