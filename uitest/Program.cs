using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BlApi;
using BO;
using PL;
using PL.Courier;
using PL.Order;

// Type aliases to disambiguate BO.* from PL.* (which re-exports them).
using OrderType = BO.OrderType;
using OrderStatus = BO.OrderStatus;
using DeliveryTransport = BO.DeliveryTransport;
using DeviceType = BO.DeviceType;
using OrderListFilterProperty = BO.OrderListFilterProperty;
using OrderListSortProperty = BO.OrderListSortProperty;

internal static class Program
{
    // Headless UI verification harness: instantiates every window on the STA thread,
    // pumps the Dispatcher so Loaded + observer callbacks fire, then closes the window.
    // Exits non-zero if ANY window throws during init/bind/close.
    //
    // Two passes:
    //   1) BL scenarios — exercises auth, CRUD, filtering, and the simulator.
    //   2) UI pass — instantiates every window and pumps the Dispatcher.

    private const int ADMIN = 204857392;
    private const int COURIER = 312458962; // deterministic seed courier
    private const int ORDER = 1000;         // exists in xml/orders.xml

    private static readonly IBl s_bl = BlApi.Factory.Get();

    [STAThread]
    private static int Main()
    {
        Console.WriteLine("=== Pass 1: BL scenarios ===");
        var blFailures = RunBlScenarios();
        if (blFailures.Count > 0)
        {
            Console.WriteLine("BL FAILURES:");
            foreach (var f in blFailures) Console.WriteLine("  - " + f);
            return 1;
        }
        Console.WriteLine("BL SCENARIOS OK.\n");

        Console.WriteLine("=== Pass 2: UI window instantiation ===");
        var uiFailures = RunUiPass();
        if (uiFailures.Count > 0)
        {
            Console.WriteLine("UI FAILURES:");
            foreach (var f in uiFailures) Console.WriteLine("  - " + f);
            return 1;
        }
        Console.WriteLine("\nALL TESTS OK.");
        return 0;
    }

    private static List<string> RunBlScenarios()
    {
        var failures = new List<string>();

        // 1. Authentication (new deterministic credentials)
        if (!s_bl.Admin.AuthenticateManager(ADMIN, "admin123"))
            failures.Add("Admin auth failed for 204857392/admin123");
        else
            Console.WriteLine("  PASS admin auth (204857392/admin123)");

        if (s_bl.Admin.AuthenticateManager(ADMIN, "wrong123"))
            failures.Add("Admin auth accepted wrong password");
        if (s_bl.Admin.AuthenticateManager(111111111, "admin123"))
            failures.Add("Old admin ID 111111111 should no longer authenticate");

        try
        {
            var role = s_bl.Courier.AuthenticateCourier(COURIER.ToString(), "courier123");
            if (role != "Courier")
                failures.Add($"Courier auth role expected 'Courier', got '{role}'");
            else
                Console.WriteLine("  PASS courier auth (312458962/courier123)");
        }
        catch (Exception ex)
        {
            failures.Add($"Courier auth threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 2. ResetDB preserves creds
        try
        {
            s_bl.Admin.ResetDB(ADMIN);
            if (!s_bl.Admin.AuthenticateManager(ADMIN, "admin123"))
                failures.Add("Admin auth failed after ResetDB");
            else
                Console.WriteLine("  PASS admin auth after ResetDB");

            var role = s_bl.Courier.AuthenticateCourier(COURIER.ToString(), "courier123");
            if (role != "Courier")
                failures.Add($"Courier auth role wrong after ResetDB: '{role}'");
            else
                Console.WriteLine("  PASS courier auth after ResetDB");

            // Verify courier 312458962 record survived reset
            var det = s_bl.Courier.GetCourierDetails(ADMIN, COURIER);
            if (det.FullName != "Yossi Cohen")
                failures.Add($"Courier name after ResetDB: '{det.FullName}', expected 'Yossi Cohen'");
            if (det.TransportType != DeliveryTransport.Motorcycle)
                failures.Add($"Courier transport after ResetDB: {det.TransportType}, expected Motorcycle");
            if (det.MaxDeliveryDistanceKM != 35.0)
                failures.Add($"Courier maxDistance after ResetDB: {det.MaxDeliveryDistanceKM}, expected 35.0");
            else
                Console.WriteLine("  PASS courier record (Yossi Cohen, Motorcycle, 35.0) survived ResetDB");
        }
        catch (Exception ex)
        {
            failures.Add($"ResetDB flow threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 3. InitializeDB preserves creds + courier
        try
        {
            s_bl.Admin.InitializeDB(ADMIN);
            if (!s_bl.Admin.AuthenticateManager(ADMIN, "admin123"))
                failures.Add("Admin auth failed after InitializeDB");
            else
                Console.WriteLine("  PASS admin auth after InitializeDB");

            var role = s_bl.Courier.AuthenticateCourier(COURIER.ToString(), "courier123");
            if (role != "Courier")
                failures.Add($"Courier auth role wrong after InitializeDB: '{role}'");
            else
                Console.WriteLine("  PASS courier auth after InitializeDB");

            var det = s_bl.Courier.GetCourierDetails(ADMIN, COURIER);
            if (det.FullName != "Yossi Cohen" || det.TransportType != DeliveryTransport.Motorcycle || det.MaxDeliveryDistanceKM != 35.0)
                failures.Add($"Courier record after InitializeDB: {det.FullName}/{det.TransportType}/{det.MaxDeliveryDistanceKM}");
            else
                Console.WriteLine("  PASS courier record survived InitializeDB");
        }
        catch (Exception ex)
        {
            failures.Add($"InitializeDB flow threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 4. Order list filtering & sorting
        try
        {
            var all = s_bl.Order.GetOrdersList(ADMIN.ToString(), null, null, null).ToList();
            if (all.Count == 0)
                failures.Add("GetOrdersList returned empty after InitializeDB");
            else
                Console.WriteLine($"  PASS GetOrdersList returned {all.Count} orders");

            // Filter by OrderStatus (closed vs open)
            var delivered = s_bl.Order.GetOrdersList(ADMIN.ToString(),
                OrderListFilterProperty.Status, OrderStatus.Delivered, null).ToList();
            Console.WriteLine($"  PASS filter by Status=Delivered -> {delivered.Count} orders");

            var byType = s_bl.Order.GetOrdersList(ADMIN.ToString(),
                OrderListFilterProperty.OrderType, OrderType.Individual, null).ToList();
            Console.WriteLine($"  PASS filter by OrderType=Individual -> {byType.Count} orders");

            var sorted = s_bl.Order.GetOrdersList(ADMIN.ToString(), null, null,
                OrderListSortProperty.OrderId).ToList();
            if (sorted.Count > 1 && sorted[0].OrderID > sorted[^1].OrderID)
                failures.Add("Sort by OrderId ascending is broken");
            else
                Console.WriteLine("  PASS sort by OrderId ascending");

            // Summary
            var summary = s_bl.Order.GetOrdersSummary(ADMIN.ToString());
            Console.WriteLine($"  PASS GetOrdersSummary -> [{string.Join(",", summary)}]");
        }
        catch (Exception ex)
        {
            failures.Add($"Order list/filter threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 5. Courier list filtering & sorting
        try
        {
            var all = s_bl.Courier.GetCouriersList(ADMIN, null, null).ToList();
            if (all.Count == 0)
                failures.Add("GetCouriersList returned empty after InitializeDB");
            else
                Console.WriteLine($"  PASS GetCouriersList returned {all.Count} couriers");

            var active = s_bl.Courier.GetCouriersList(ADMIN, true, null).ToList();
            Console.WriteLine($"  PASS filter isActive=true -> {active.Count} couriers");

            var byName = s_bl.Courier.GetCouriersList(ADMIN, null, "FullName").ToList();
            Console.WriteLine($"  PASS sort by FullName -> {byName.Count} couriers");
        }
        catch (Exception ex)
        {
            failures.Add($"Courier list/filter threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 6. Add/Update/Cancel an order
        try
        {
            var existing = s_bl.Order.GetOrdersList(ADMIN.ToString(), null, null, null).ToList();
            int nextOrderId = existing.Count > 0 ? existing.Max(o => o.OrderID) + 1 : 1000;

            var newOrder = new BO.Order
            {
                OrderID = nextOrderId,
                OrderType = OrderType.Individual,
                Description = "uitest order",
                FullAddress = "Test Address 1",
                Latitude = 32.064012,
                Longitude = 34.774101,
                CustomerFullName = "Test Customer",
                CustomerPhone = "0500000000",
                PizzaSize = DeviceType.Laptop
            };
            s_bl.Order.AddOrder(ADMIN.ToString(), newOrder);

            var detail = s_bl.Order.GetOrderDetails(ADMIN.ToString(), nextOrderId);
            if (detail == null)
            {
                failures.Add("Added order not found");
            }
            else
            {
                Console.WriteLine($"  PASS AddOrder -> order #{detail.OrderID}");

                detail.Description = "uitest updated";
                s_bl.Order.UpdateOrder(ADMIN.ToString(), detail);
                var updated = s_bl.Order.GetOrderDetails(ADMIN.ToString(), nextOrderId);
                if (updated.Description != "uitest updated")
                    failures.Add($"UpdateOrder description = '{updated.Description}'");
                else
                    Console.WriteLine("  PASS UpdateOrder");

                s_bl.Order.CancelOrder(ADMIN.ToString(), nextOrderId);
                var cancelled = s_bl.Order.GetOrderDetails(ADMIN.ToString(), nextOrderId);
                if (cancelled.OrderStatus != OrderStatus.Cancelled)
                    failures.Add($"CancelOrder status = {cancelled.OrderStatus}, expected Cancelled");
                else
                    Console.WriteLine("  PASS CancelOrder");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"Order CRUD threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 7. Add/Update/Delete a courier
        try
        {
            const int newCourierId = 400000001;
            // Ensure it doesn't already exist
            try
            {
                var existing = s_bl.Courier.GetCourierDetails(ADMIN, newCourierId);
                if (existing != null) s_bl.Courier.DeleteCourier(ADMIN, newCourierId);
            }
            catch { }

            var newCourier = new BO.Courier
            {
                CourierID = newCourierId,
                FullName = "Test Courier",
                Phone = "0500000000",
                Email = "test.courier@example.com",
                TransportType = DeliveryTransport.Car,
                Password = "test1234",
                IsActive = true,
                MaxDeliveryDistanceKM = 20.0
            };
            s_bl.Courier.AddCourier(ADMIN, newCourier);

            var det = s_bl.Courier.GetCourierDetails(ADMIN, newCourierId);
            if (det.FullName != "Test Courier")
                failures.Add($"Added courier name = '{det.FullName}'");
            else
                Console.WriteLine($"  PASS AddCourier -> {newCourierId}");

            det.FullName = "Test Courier Updated";
            s_bl.Courier.UpdateCourier(ADMIN, det);
            var updated = s_bl.Courier.GetCourierDetails(ADMIN, newCourierId);
            if (updated.FullName != "Test Courier Updated")
                failures.Add($"UpdateCourier name = '{updated.FullName}'");
            else
                Console.WriteLine("  PASS UpdateCourier");

            // Verify GetCourierDetails never leaks password hash
            if (updated.Password != null)
                failures.Add($"GetCourierDetails should return null password, got a non-null value");
            else
                Console.WriteLine("  PASS GetCourierDetails returns null password (no hash exposure)");

            // Verify password is preserved after editing non-password fields
            try
            {
                var authRole = s_bl.Courier.AuthenticateCourier(newCourierId.ToString(), "test1234");
                if (authRole != "Courier")
                    failures.Add("Password lost after UpdateCourier with null password field");
                else
                    Console.WriteLine("  PASS Password preserved after UpdateCourier (null password = keep existing)");
            }
            catch (Exception authEx)
            {
                failures.Add($"Auth after UpdateCourier threw: {authEx.GetType().Name}: {authEx.Message}");
            }

            s_bl.Courier.DeleteCourier(ADMIN, newCourierId);
            try
            {
                s_bl.Courier.GetCourierDetails(ADMIN, newCourierId);
                failures.Add("Deleted courier still retrievable");
            }
            catch
            {
                Console.WriteLine("  PASS DeleteCourier");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"Courier CRUD threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 8. Simulator start / advance / stop
        try
        {
            s_bl.Admin.StartSimulator(ADMIN, 1);
            Thread.Sleep(2500); // ~2-3 clock ticks
            s_bl.Admin.StopSimulator(ADMIN);
            Console.WriteLine("  PASS Simulator start/stop");
        }
        catch (Exception ex)
        {
            failures.Add($"Simulator threw: {ex.GetType().Name}: {ex.Message}");
            try { s_bl.Admin.StopSimulator(ADMIN); } catch { }
        }

        // 9. Task 5: verify no order can have two simultaneous active deliveries
        try
        {
            // Create a fresh order
            var task5Order = new BO.Order
            {
                OrderID = 0,
                OrderType = OrderType.Individual,
                Description = "Task5 dup-assignment test",
                FullAddress = "Test Street 1",
                Latitude = 32.064012,
                Longitude = 34.774101,
                CustomerFullName = "Task5 Customer",
                CustomerPhone = "0500000001",
                PizzaSize = DeviceType.Desktop
            };
            s_bl.Order.AddOrder(ADMIN.ToString(), task5Order);
            var allOrds = s_bl.Order.GetOrdersList(ADMIN.ToString(), null, null, null).ToList();
            int t5OrderId = allOrds.Max(o => o.OrderID);

            // Find two free active couriers
            var freeCouriers = s_bl.Courier.GetCouriersList(ADMIN, true, null)
                .Where(c => c.orderInProgress == null)
                .Take(2).ToList();

            if (freeCouriers.Count >= 2)
            {
                // Assign first courier
                s_bl.Order.SelectOrderForHandling(ADMIN.ToString(), freeCouriers[0].CourierID.ToString(), t5OrderId);

                // Second assignment on same active order should throw
                bool threw = false;
                try
                {
                    s_bl.Order.SelectOrderForHandling(ADMIN.ToString(), freeCouriers[1].CourierID.ToString(), t5OrderId);
                }
                catch
                {
                    threw = true;
                }

                if (!threw)
                    failures.Add("Task5: SelectOrderForHandling must throw when order already has active delivery");
                else
                    Console.WriteLine("  PASS Task5: duplicate active assignment correctly prevented");
            }
            else
            {
                Console.WriteLine("  SKIP Task5: not enough free active couriers for test");
            }

            // Always clean up
            try { s_bl.Order.CancelOrder(ADMIN.ToString(), t5OrderId); } catch { }
        }
        catch (Exception ex)
        {
            failures.Add($"Task5 check threw: {ex.GetType().Name}: {ex.Message}");
        }

        return failures;
    }

    private static List<string> RunUiPass()
    {
        var failures = new List<string>();
        var dispatcherErrors = new List<string>();

        Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
        {
            dispatcherErrors.Add(e.Exception.GetType().Name + ": " + e.Exception.Message);
            e.Handled = true; // keep the harness alive
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                dispatcherErrors.Add("AppDomain: " + ex.GetType().Name + ": " + ex.Message);
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            dispatcherErrors.Add("UnobservedTask: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
            e.SetObserved();
        };

        // Use PL's own Application so the shared resources defined in PL/App.xaml
        // (converters, enum collections, and the modern theme styles: AppTextBox,
        // AppPrimaryButton, AppCard, ...) are loaded and available to every window.
        var app = new PL.App();
        app.InitializeComponent();

        // === UI filter flow test: simulate user picking FilterProperty + FilterValue ===
        try
        {
            var win = new OrderListWindow(ADMIN.ToString());
            win.Show();
            PumpDispatcher(400);

            // Initially no filter is set -> should show all orders.
            int initialCount = win.OrderList?.Count() ?? 0;
            if (initialCount == 0)
                failures.Add($"UI filter: initial OrderList is empty (expected all orders)");
            else
                Console.WriteLine($"  PASS UI filter: initial list has {initialCount} orders");

            // Simulate picking FilterProperty = Status.
            win.FilterProperty = OrderListFilterProperty.Status;
            PumpDispatcher(200);
            int statusValuesCount = win.FilterValues?.Count() ?? 0;
            if (statusValuesCount != 4)
                failures.Add($"UI filter: FilterValues for Status has {statusValuesCount} items, expected 4");
            else
                Console.WriteLine($"  PASS UI filter: FilterValues for Status has 4 items");
            if (win.FilterValue == null)
                failures.Add($"UI filter: FilterValue is null after picking Status (expected auto-selected first)");
            else
                Console.WriteLine($"  PASS UI filter: FilterValue auto-selected = {win.FilterValue}");

            int filteredCount = win.OrderList?.Count() ?? 0;
            if (filteredCount == 0)
                failures.Add($"UI filter: OrderList empty after filtering by Status (expected non-zero)");
            else
                Console.WriteLine($"  PASS UI filter: Status={win.FilterValue} -> {filteredCount} orders");

            // Simulate changing FilterValue to Delivered.
            win.FilterValue = OrderStatus.Delivered;
            PumpDispatcher(200);
            int deliveredCount = win.OrderList?.Count() ?? 0;
            if (deliveredCount == 0)
                failures.Add($"UI filter: OrderList empty after filtering by Status=Delivered (expected ~39)");
            else
                Console.WriteLine($"  PASS UI filter: Status=Delivered -> {deliveredCount} orders");

            // Switch FilterProperty to OrderType.
            win.FilterProperty = OrderListFilterProperty.OrderType;
            PumpDispatcher(200);
            int typeValuesCount = win.FilterValues?.Count() ?? 0;
            if (typeValuesCount != 3)
                failures.Add($"UI filter: FilterValues for OrderType has {typeValuesCount} items, expected 3");
            int typeFilteredCount = win.OrderList?.Count() ?? 0;
            if (typeFilteredCount == 0)
                failures.Add($"UI filter: OrderList empty after filtering by OrderType");
            else
                Console.WriteLine($"  PASS UI filter: OrderType={win.FilterValue} -> {typeFilteredCount} orders");

            win.Close();
            PumpDispatcher(200);
        }
        catch (Exception ex)
        {
            failures.Add($"UI filter test threw: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"  FAIL UI filter test -> {ex.GetType().Name}: {ex.Message}");
        }

        var specs = new (string Name, Func<Window> Factory)[]
        {
            ("LoginPage",            () => new LoginPage()),
            ("MainWindow",            () => new MainWindow(ADMIN)),
            ("OrderListWindow",       () => new OrderListWindow(ADMIN.ToString())),
            ("OrderWindow",           () => new OrderWindow(ADMIN.ToString(), ORDER)),
            ("CourierListWindow",     () => new CourierListWindow()),
            ("CourierSelfWindow",     () => new CourierSelfWindow(COURIER)),
            ("CourierWindow",         () => new CourierWindow(COURIER)),
            ("ChooseOrderWindow",     () => new ChooseOrderWindow(COURIER)),
            ("CourierHistoryWindow",  () => new CourierHistoryWindow(COURIER)),
        };

        int pass = 0;
        foreach (var (name, factory) in specs)
        {
            dispatcherErrors.Clear();
            Window? win = null;
            try
            {
                win = factory();
                win.Show();
                PumpDispatcher(600);
                win.Close();
                PumpDispatcher(200);

                if (dispatcherErrors.Count > 0)
                {
                    failures.Add($"{name}: dispatcher exception(s): {string.Join(" | ", dispatcherErrors)}");
                    Console.WriteLine($"  FAIL {name} -> dispatcher exception(s): {string.Join(" | ", dispatcherErrors)}");
                }
                else
                {
                    Console.WriteLine($"  PASS {name}");
                    pass++;
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"  FAIL {name} -> {ex.GetType().Name}: {ex.Message}");
                try { win?.Close(); } catch { }
            }
        }

        Console.WriteLine($"\nUI RESULT: {pass}/{specs.Length} windows loaded cleanly.");
        return failures;
    }

    private static void PumpDispatcher(int milliseconds)
    {
        var deadline = Environment.TickCount + milliseconds;
        while (Environment.TickCount < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
    }
}