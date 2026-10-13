using Helpers;

namespace BO;

public class OrderInProgress
{
    /// <summary>
    /// Unique identifier for the delivery
    /// </summary>
    public int DeliveryID { get; init; }

    /// <summary>
    /// Unique identifier for the order
    /// </summary>
    public int OrderID { get; init; }

    /// <summary>
    /// Type of the order (e.g., pizza, pasta, etc.)
    /// </summary>
    public OrderType orderType { get; init; }

    /// <summary>
    /// Description of the order
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Full delivery address
    /// </summary>
    public string FullAddress { get; init; }

    /// <summary>
    /// Straight-line distance to the delivery address in kilometers
    /// </summary>
    public double AirDistanceKM { get; init; }

    /// <summary>
    /// Actual distance traveled for the delivery
    /// </summary>
    public double? RealDistance { get; init; }

    /// <summary>
    /// Full name of the person who invited the order
    /// </summary>
    public string? InviterFullName { get; init; }

    /// <summary>
    /// Phone number of the person who invited the order
    /// </summary>
    public string InviterPhone { get; init; }

    /// <summary>
    /// Time when the order was opened
    /// </summary>
    public DateTime OrderOpeningTime { get; init; }

    /// <summary>
    /// Time when the delivery started
    /// </summary>
    public DateTime DeliveryStartTime { get; init; }

    /// <summary>
    /// Estimated delivery time
    /// </summary>
    public DateTime EstimatedDeliveryTime { get; init; }

    /// <summary>
    /// Maximum allowed delivery time
    /// </summary>
    public DateTime MaxDeliveryTime { get; init; }

    /// <summary>
    /// Current status of the order
    /// </summary>
    public OrderStatus orderStatus { get; init; }

    /// <summary>
    /// Status of the delivery schedule (e.g., on time, delayed, etc.)
    /// </summary>
    public ScheduleStatus scheduleStatus { get; init; }

    /// <summary>
    /// Total time taken to complete the order
    /// </summary>
    public TimeSpan TotalTimeToCompleteAnOrder { get; init; }

    /// <summary>
    /// Time remaining until the order deadline (negative = overdue)
    /// </summary>
    public TimeSpan TimeRemainingToComplete { get; init; }

    public override string ToString() => this.ToStringProperty();
}
