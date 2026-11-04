namespace DalTest;

using DalApi;
using DO;
using System;
using System.Collections.Generic;
using System.Linq;

public static class Initialization
{
    private static readonly IDal s_dal = Factory.Get;
    private static readonly Random s_rand = new();

    // Running IDs
    private static int s_nextCourierId = 300000000;
    private static int s_nextOrderId = 1000;
    private static int s_nextDeliveryId = 100000;

    // Store location (Tel Aviv)
    private const double STORE_LAT = 32.064012;
    private const double STORE_LON = 34.774101;

    // ============================
    // Entry point
    // ============================
    public static void Do()
    {
        Console.WriteLine("\n=== INITIALIZING DATABASE ===\n");

        s_dal.ResetDB();

        // Reset() zeroes the operational config; restore realistic demo values so the
        // generated orders have a genuine delivery window (mix of OnTime / InRisk / Late)
        // and real air distances from the store location.
        SetRealisticConfig();

        CreateCouriers();
        CreateOrders();
        CreateDeliveries();

        // Keep the simulation clock aligned with real time so the freshly generated
        // (DateTime.Now-relative) orders are evaluated against "now".
        s_dal.Config.Clock = DateTime.Now;

        Console.WriteLine("\n=== Initialization Completed ===\n");
    }

    private static void SetRealisticConfig()
    {
        s_dal.Config.MaxDeliveryDistance = 30;                       // km
        s_dal.Config.MaxDeliveryTimeRange = TimeSpan.FromMinutes(90); // 1h30m window
        s_dal.Config.RiskRange = TimeSpan.FromMinutes(15);            // "at risk" threshold
        s_dal.Config.InactivityTimeRange = TimeSpan.FromHours(8);
        s_dal.Config.Latitude = STORE_LAT;
        s_dal.Config.Longitude = STORE_LON;
    }

    // ============================
    // Couriers
    // ============================
    private static void CreateCouriers()
    {
        Console.WriteLine("Creating Couriers...");

        string[] names =
        {
            "Amit Barak", "Noam Levi", "Daniel Stern", "Yuval Cohen",
            "Itay Shalom", "Lior Ben David", "Erez Ohayon", "Moran Feldman",
            "Shai Azulay", "Tomer Katz", "Adi Peretz", "Niv Rosen"
        };

        var transports = Enum.GetValues(typeof(DeliveryTransport));
        int count = s_rand.Next(20, 35);

        for (int i = 0; i < count; i++)
        {
            Courier courier = new Courier(
                CourierID: s_nextCourierId++,
                FullName: names[s_rand.Next(names.Length)],
                Phone: "05" + s_rand.Next(10000000, 99999999),
                Email: $"user{i}@mail.com",
                DeliveryType: null,
                StartWorkInCompany: DateTime.Now.AddDays(-s_rand.Next(100, 1200)),
                TransportType: (DeliveryTransport)transports.GetValue(
                    s_rand.Next(transports.Length))!,
                Password: "Pass" + s_rand.Next(1000, 9999),
                IsActive: s_rand.Next(100) < 80,
                MaxDeliveryDistanceKm: s_rand.Next(100) < 70
                    ? s_rand.Next(3, 30)
                    : null,
                OrderInProgress: null,
                DeliveredInTime: s_rand.Next(0, 50),
                DeliveredNotInTime: s_rand.Next(0, 10)
            );

            s_dal.Courier.Create(courier);
        }

        Console.WriteLine($"Created {count} couriers.");
    }

    // ============================
    // Orders (WITH ENUM VARIETY)
    // ============================
    private static void CreateOrders()
    {
        Console.WriteLine("Creating Orders...");

        string[] customers =
        {
            "Yonatan Katz", "Adi Peretz", "Shai Azulay",
            "Moran Feldman", "Erez Ohayon", "Noa Levi"
        };

        var pizzaSizes = Enum.GetValues(typeof(DeviceType));
        int count = s_rand.Next(40, 70);

        for (int i = 0; i < count; i++)
        {
            OrderType orderType = PickByProbability(new[]
            {
                (OrderType.Individual, 45),
                (OrderType.Group, 35),
                (OrderType.Corporate, 20)
            });

            Order order = new Order(
                OrderID: s_nextOrderId++,
                OrderType: orderType,
                Description: "Random generated order",
                FullAddress: "Somewhere in Tel Aviv",
                Latitude: STORE_LAT + s_rand.NextDouble() * 0.05,
                Longitude: STORE_LON + s_rand.NextDouble() * 0.05,
                CustomerName: customers[s_rand.Next(customers.Length)],
                CustomerPhone: "05" + s_rand.Next(10000000, 99999999),
                PizzaSize: (DeviceType)pizzaSizes.GetValue(
                    s_rand.Next(pizzaSizes.Length))!,
                OrderOpeningTime: DateTime.Now.AddMinutes(-s_rand.Next(30, 5000))
            );

            s_dal.Order.Create(order);
        }

        Console.WriteLine($"Created {count} orders.");
    }

    // ============================
    // Deliveries (FULL VARIETY)
    // ============================
    private static void CreateDeliveries()
    {
        Console.WriteLine("Creating Deliveries...");

        var orders = s_dal.Order.ReadAll().ToList();
        var couriers = s_dal.Courier.ReadAll()
            .Where(c => c.IsActive)
            .ToList();

        if (!couriers.Any())
        {
            Console.WriteLine("No active couriers – skipping deliveries.");
            return;
        }

        int created = 0;

        foreach (var order in orders)
        {
            // Attempts for one order are sequential: each starts after the previous one ended.
            // Only the LAST attempt may close the order (Completed / Cancelled / CustomerRefused);
            // earlier attempts are retries (Failed / CustomerNotFound). This keeps the history
            // realistic - a delivered order never has a second, overlapping courier.
            int deliveriesForOrder = s_rand.Next(1, 3);
            DateTime nextStart = order.OrderOpeningTime.AddMinutes(s_rand.Next(10, 120));

            for (int i = 0; i < deliveriesForOrder; i++)
            {
                Courier courier = couriers[s_rand.Next(couriers.Count)];
                bool isLast = i == deliveriesForOrder - 1;

                ProcessResult doneType = isLast
                    ? PickByProbability(new[]
                    {
                        (ProcessResult.Completed, 60),
                        (ProcessResult.Failed, 15),
                        (ProcessResult.CustomerNotFound, 10),
                        (ProcessResult.Cancelled, 10),
                        (ProcessResult.CustomerRefused, 5)
                    })
                    : PickByProbability(new[]
                    {
                        (ProcessResult.Failed, 60),
                        (ProcessResult.CustomerNotFound, 40)
                    });

                DeliveryType deliveryType = PickByProbability(new[]
                {
                    (DeliveryType.Regular, 70),
                    (DeliveryType.Express, 30)
                });

                DateTime start = nextStart;
                DateTime end = start.AddMinutes(s_rand.Next(15, 90));
                nextStart = end.AddMinutes(s_rand.Next(5, 60));

                Delivery delivery = new Delivery(
                    DeliveryID: s_nextDeliveryId++,
                    OrderID: order.OrderID,
                    CourierID: courier.CourierID,
                    DeliveryType: deliveryType,
                    DeliveryStartTime: start,
                    DeliveryDistance: doneType == ProcessResult.Completed
                        ? s_rand.NextDouble() * 20
                        : null,
                    DeliveryDoneType: doneType,
                    DeliveryDoneTime: end   // every seeded attempt is finished; none is left "in progress"
                );

                s_dal.Delivery.Create(delivery);
                created++;
            }
        }

        Console.WriteLine($"Created {created} deliveries.");
    }

    // ============================
    // Probability helper
    // ============================
    private static T PickByProbability<T>((T value, int weight)[] options)
    {
        int total = options.Sum(o => o.weight);
        int rnd = s_rand.Next(total);

        int cumulative = 0;
        foreach (var (value, weight) in options)
        {
            cumulative += weight;
            if (rnd < cumulative)
                return value;
        }

        return options[0].value;
    }
}
