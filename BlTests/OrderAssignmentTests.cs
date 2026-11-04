using BO;
using ProcessResult = DO.ProcessResult;

namespace BlTests;

/// <summary>
/// Rules for which orders a courier may take and how delivery attempts are recorded:
/// 1. Completed           -> order is closed, never offered again.
/// 2. Failed / NotFound   -> order may be offered again (new attempt).
/// 3. In progress         -> no other courier may take it (also under concurrency).
/// 4. Cancelled           -> never re-assigned.
/// 5. Every attempt stays in the history, even with several couriers.
/// </summary>
public class OrderAssignmentTests
{
    private const string Admin = "204857392";
    private const double CompanyLat = 32.064012;
    private const double CompanyLon = 34.774101;

    private readonly DalApi.IDal _dal = DalApi.Factory.Get;
    private readonly BlApi.IBl _bl = BlApi.Factory.Get();
    private int _nextCourierId = 400000001;

    public OrderAssignmentTests()
    {
        _dal.ResetDB();
    }

    // ---------- helpers ----------

    private int AddCourier()
    {
        int id = _nextCourierId++;
        _dal.Courier.Create(new DO.Courier(
            CourierID: id,
            FullName: $"Courier {id}",
            Phone: "0501234567",
            Email: $"c{id}@test.com",
            StartWorkInCompany: _dal.Config.Clock.AddYears(-1),
            TransportType: DO.DeliveryTransport.Car,
            IsActive: true,
            MaxDeliveryDistanceKm: 50));
        return id;
    }

    private int AddOrder()
    {
        int id = _dal.Order.ReadAll().Select(o => o.OrderID).DefaultIfEmpty(999).Max() + 1;
        _dal.Order.Create(new DO.Order(
            OrderID: id,
            Description: "test",
            FullAddress: "Rothschild 1, Tel Aviv",
            Latitude: CompanyLat + 0.001,
            Longitude: CompanyLon + 0.001,
            CustomerName: "Customer",
            CustomerPhone: "0500000000",
            OrderType: DO.OrderType.Individual,
            PizzaSize: DO.DeviceType.Desktop,
            OrderOpeningTime: _dal.Config.Clock.AddMinutes(-30)));
        return id;
    }

    /// <summary>Adds a finished attempt directly in the DAL (as historical/seed data would look).</summary>
    private void AddFinishedAttempt(int orderId, int courierId, ProcessResult result, int startMinutesAgo, int endMinutesAgo)
    {
        var now = _dal.Config.Clock;
        _dal.Delivery.Create(new DO.Delivery(
            DeliveryID: 0,
            OrderID: orderId,
            CourierID: courierId,
            DeliveryType: DO.DeliveryType.Regular,
            DeliveryStartTime: now.AddMinutes(-startMinutesAgo),
            DeliveryDistance: 1,
            DeliveryDoneType: result,
            DeliveryDoneTime: now.AddMinutes(-endMinutesAgo)));
    }

    private bool IsOpenFor(int courierId, int orderId) =>
        _bl.Order.GetOpenOrdersForCourier(Admin, courierId.ToString(), null, null)
            .Any(o => o.OrderID == orderId);

    private DO.Delivery ActiveDeliveryOf(int courierId) =>
        _dal.Delivery.ReadAll(d => d.CourierID == courierId && d.DeliveryDoneTime == null).Single();

    // ---------- rule 1: Completed closes the order (the order-1000 bug) ----------

    [Fact]
    public void CompletedOrder_IsNotOfferedAgain_AndCannotBeTakenBySecondCourier()
    {
        int c1 = AddCourier(), c2 = AddCourier();
        int order = AddOrder();

        Assert.True(IsOpenFor(c1, order));

        _bl.Order.SelectOrderForHandling(Admin, c1.ToString(), order);
        _bl.Order.CompleteOrderHandling(Admin, c1.ToString(), ActiveDeliveryOf(c1).DeliveryID);

        Assert.False(IsOpenFor(c2, order));
        Assert.Throws<BlInvalidIdException>(() => _bl.Order.SelectOrderForHandling(Admin, c2.ToString(), order));

        var details = _bl.Order.GetOrderDetails(Admin, order);
        Assert.Equal(BO.OrderStatus.Delivered, details.OrderStatus);
        Assert.Single(details.DeliveriesForOrder);
    }

    [Fact]
    public void LegacyDataLikeOrder1000_TwoOverlappingCompletedAttempts_StaysClosed()
    {
        // Same shape as order 1000 in xml/deliveries.xml: two couriers, overlapping times, both Completed.
        int c1 = AddCourier(), c2 = AddCourier(), c3 = AddCourier();
        int order = AddOrder();
        AddFinishedAttempt(order, c1, ProcessResult.Completed, startMinutesAgo: 120, endMinutesAgo: 40);
        AddFinishedAttempt(order, c2, ProcessResult.Completed, startMinutesAgo: 150, endMinutesAgo: 70);

        Assert.False(IsOpenFor(c3, order));
        Assert.Throws<BlInvalidIdException>(() => _bl.Order.SelectOrderForHandling(Admin, c3.ToString(), order));
        Assert.Equal(2, _dal.Delivery.ReadAll(d => d.OrderID == order).Count());
    }

    [Fact]
    public async Task AllOpenOrderQueries_ExcludeClosedOrders()
    {
        int courier = AddCourier(), other = AddCourier();
        int completed = AddOrder(), refused = AddOrder(), cancelled = AddOrder();
        AddFinishedAttempt(completed, other, ProcessResult.Completed, 60, 30);
        AddFinishedAttempt(refused, other, ProcessResult.CustomerRefused, 60, 30);
        AddFinishedAttempt(cancelled, other, ProcessResult.Cancelled, 60, 30);
        var closed = new[] { completed, refused, cancelled };

        var sync = _bl.Order.GetOpenOrdersForCourier(Admin, courier.ToString(), null, null).Select(o => o.OrderID);
        var async = (await _bl.Order.GetOpenOrdersForCourierAsync(Admin, courier.ToString(), null, null)).Select(o => o.OrderID);
        var streamed = new List<int>();
        await foreach (var o in _bl.Order.StreamOpenOrdersForCourierAsync(Admin, courier.ToString(), null, null))
            streamed.Add(o.OrderID);

        Assert.Empty(sync.Intersect(closed));
        Assert.Empty(async.Intersect(closed));
        Assert.Empty(streamed.Intersect(closed));
    }

    // ---------- rule 2: Failed / CustomerNotFound may be retried ----------

    [Theory]
    [InlineData(ProcessResult.Failed)]
    [InlineData(ProcessResult.CustomerNotFound)]
    public void FailedAttempt_OrderReturnsToOpenList_AndHistoryKeepsBothCouriers(ProcessResult result)
    {
        int c1 = AddCourier(), c2 = AddCourier();
        int order = AddOrder();
        AddFinishedAttempt(order, c1, result, 60, 30);

        Assert.True(IsOpenFor(c2, order));
        _bl.Order.SelectOrderForHandling(Admin, c2.ToString(), order);

        var history = _bl.Order.GetOrderDetails(Admin, order).DeliveriesForOrder;
        Assert.Equal(2, history.Count);
        Assert.Equal(new int?[] { c1, c2 }.OrderBy(x => x), history.Select(h => h.CourierID).OrderBy(x => x));
        Assert.Contains(history, h => h.deliveryDoneType == (BO.DeliveryDoneType)result);
    }

    // ---------- rule 3: in progress blocks other couriers ----------

    [Fact]
    public void InProgressOrder_IsNotOffered_AndSecondCourierIsRejected()
    {
        int c1 = AddCourier(), c2 = AddCourier();
        int order = AddOrder();

        _bl.Order.SelectOrderForHandling(Admin, c1.ToString(), order);

        Assert.False(IsOpenFor(c2, order));
        Assert.Throws<BlInvalidIdException>(() => _bl.Order.SelectOrderForHandling(Admin, c2.ToString(), order));
        Assert.Single(_dal.Delivery.ReadAll(d => d.OrderID == order));
    }

    [Fact]
    public void ConcurrentSelection_OnlyOneCourierGetsTheOrder()
    {
        const int couriersCount = 16;
        var couriers = Enumerable.Range(0, couriersCount).Select(_ => AddCourier()).ToArray();
        int order = AddOrder();

        for (int round = 0; round < 20; round++)
        {
            // fresh order each round to hit the race repeatedly
            if (round > 0)
            {
                foreach (var c in couriers)
                {
                    var active = _dal.Delivery.ReadAll(d => d.CourierID == c && d.DeliveryDoneTime == null).SingleOrDefault();
                    if (active != null)
                        _bl.Order.CompleteOrderHandling(Admin, c.ToString(), active.DeliveryID);
                }
                order = AddOrder();
            }

            int successes = 0;
            using var start = new Barrier(couriersCount);
            var threads = couriers.Select(c => new Thread(() =>
            {
                start.SignalAndWait();
                try
                {
                    _bl.Order.SelectOrderForHandling(Admin, c.ToString(), order);
                    Interlocked.Increment(ref successes);
                }
                catch (BlInvalidIdException) { /* expected for the losers */ }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());

            Assert.Equal(1, successes);
            Assert.Single(_dal.Delivery.ReadAll(d => d.OrderID == order));
        }
    }

    // ---------- rule 4: cancelled is never re-assigned ----------

    [Fact]
    public void CancelledOrder_IsNotOffered_AndCannotBeAssigned()
    {
        int c1 = AddCourier();
        int order = AddOrder();

        _bl.Order.CancelOrder(Admin, order);

        Assert.False(IsOpenFor(c1, order));
        Assert.Throws<BlInvalidIdException>(() => _bl.Order.SelectOrderForHandling(Admin, c1.ToString(), order));
        Assert.Equal(BO.OrderStatus.Cancelled, _bl.Order.GetOrderDetails(Admin, order).OrderStatus);
    }

    [Fact]
    public void CancellingInProgressOrder_ClosesIt_AndKeepsTheAttemptInHistory()
    {
        int c1 = AddCourier(), c2 = AddCourier();
        int order = AddOrder();

        _bl.Order.SelectOrderForHandling(Admin, c1.ToString(), order);
        _bl.Order.CancelOrder(Admin, order);

        Assert.False(IsOpenFor(c2, order));
        Assert.Throws<BlInvalidIdException>(() => _bl.Order.SelectOrderForHandling(Admin, c2.ToString(), order));
        var history = _bl.Order.GetOrderDetails(Admin, order).DeliveriesForOrder;
        Assert.Single(history);
        Assert.Equal(c1, history[0].CourierID);
    }

    [Fact]
    public void DeliveredOrder_CannotBeCancelledAfterwards()
    {
        int c1 = AddCourier();
        int order = AddOrder();
        AddFinishedAttempt(order, c1, ProcessResult.Completed, 60, 30);

        Assert.Throws<BlOperationNotAllowedException>(() => _bl.Order.CancelOrder(Admin, order));
        Assert.Equal(BO.OrderStatus.Delivered, _bl.Order.GetOrderDetails(Admin, order).OrderStatus);
        Assert.Single(_dal.Delivery.ReadAll(d => d.OrderID == order));
    }

    // ---------- unchanged behaviour ----------

    [Fact]
    public void NewOrder_IsOffered_AndCourierCannotTakeTwoOrdersAtOnce()
    {
        int c1 = AddCourier();
        int o1 = AddOrder(), o2 = AddOrder();

        Assert.True(IsOpenFor(c1, o1));
        _bl.Order.SelectOrderForHandling(Admin, c1.ToString(), o1);
        Assert.Throws<BlInvalidIdException>(() => _bl.Order.SelectOrderForHandling(Admin, c1.ToString(), o2));
        Assert.True(IsOpenFor(c1, o2));
    }
}
