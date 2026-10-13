using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PL;
using PL.Courier;
using PL.Order;

internal static class Program
{
    // Raw capture tool for the README media: opens each window against the bundled
    // xml\ demo data and renders its client area to PNG.
    //   <out>\stills\*.png + stills.json   one image per screen
    //   <out>\tour\*.png   + tour.json     scripted walkthrough frames for the demo video
    // screenshots\compose.py turns these into the framed images / GIF / MP4.
    // Usage: screenshots.exe <output-dir>

    private const int ADMIN = 204857392;
    private const int COURIER = 312458962;
    private const int ORDER = 1000;
    private const double SCALE = 1.5; // render at 144 DPI for crisp images

    private enum Fit { None, Grow, ScrollContent }

    private record Shot(string File, string Title, string? Caption, int HoldMs);

    private static string s_outDir = "";
    private static readonly List<Shot> s_tour = new();

    [STAThread]
    private static int Main(string[] args)
    {
        s_outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "screenshots-out");
        Directory.CreateDirectory(Path.Combine(s_outDir, "stills"));
        Directory.CreateDirectory(Path.Combine(s_outDir, "tour"));

        Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
        {
            Console.WriteLine("  dispatcher: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
            e.Handled = true;
        };

        var app = new PL.App();
        app.InitializeComponent();

        int failures = CaptureStills();

        try { CaptureTour(); }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine($"  FAIL tour -> {ex}");
        }
        WriteJson("tour.json", s_tour);

        return failures == 0 ? 0 : 1;
    }

    // ── Stills ───────────────────────────────────────────────────────────────

    private static int CaptureStills()
    {
        // FitContent: grow the window so its scrollable form fits (limited by the screen size).
        // ScrollContent: render the whole content of the main ScrollViewer, however tall.
        var specs = new (string File, Func<Window> Factory, double Width, Fit Fit)[]
        {
            ("login",             () => new LoginPage(),                             0,    Fit.None),
            ("admin-dashboard",   () => new MainWindow(ADMIN),                       0,    Fit.ScrollContent),
            ("order-list",        () => new OrderListWindow(ADMIN.ToString()),       1280, Fit.None),
            ("order-details",     () => new OrderWindow(ADMIN.ToString(), ORDER),    0,    Fit.None),
            ("order-add",         () => new OrderWindow(ADMIN.ToString()),           0,    Fit.None),
            ("courier-list",      () => new CourierListWindow(),                     1380, Fit.None),
            ("courier-details",   () => new CourierWindow(COURIER),                  0,    Fit.Grow),
            ("courier-dashboard", () => new CourierSelfWindow(COURIER),              0,    Fit.None),
            ("choose-order",      () => new ChooseOrderWindow(COURIER),              0,    Fit.None),
            ("courier-history",   () => new CourierHistoryWindow(COURIER),           0,    Fit.None),
        };

        var manifest = new List<Shot>();
        int failures = 0;
        Console.WriteLine($"  screen: {SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}");
        foreach (var (file, factory, width, fit) in specs)
        {
            Window? win = null;
            try
            {
                win = Open(factory, width);
                if (fit == Fit.Grow) GrowToFitScrollContent(win);
                var element = fit == Fit.ScrollContent
                    ? FindDescendant<ScrollViewer>(win)?.Content as FrameworkElement
                    : null;

                Capture(win, Path.Combine(s_outDir, "stills", file + ".png"), element);
                manifest.Add(new Shot(file + ".png", win.Title, null, 0));
                Console.WriteLine($"  saved still {file}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL {file} -> {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Close(win);
            }
        }
        WriteJson("stills.json", manifest);
        return failures;
    }

    // ── Demo tour ────────────────────────────────────────────────────────────

    private static void CaptureTour()
    {
        // 1. Sign in as admin: type the credentials character by character.
        var login = (LoginPage)Open(() => new LoginPage());
        var userBox = (TextBox)login.FindName("txtUserId");
        var pwdBox = (PasswordBox)login.FindName("pwdBox");
        const string caption1 = "Sign in — the app routes Admins and Couriers to their own workspace";
        Snap(login, caption1, 900);
        foreach (char c in ADMIN.ToString())
        {
            userBox.Text += c;
            userBox.CaretIndex = userBox.Text.Length;
            Snap(login, caption1, 70);
        }
        foreach (char c in "admin123")
        {
            pwdBox.Password += c;
            Snap(login, caption1, 70);
        }
        Snap(login, caption1, 1000);
        Close(login);

        // 2. Admin dashboard: advance the simulated clock, then scroll through it.
        var dash = Open(() => new MainWindow(ADMIN));
        const string caption2 = "Admin dashboard — simulated clock, live configuration and order summary";
        Snap(dash, caption2, 1400);
        var hourButton = FindButton(dash, "+ Hour");
        for (int i = 0; i < 3; i++)
        {
            hourButton?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(250);
            Snap(dash, caption2, 450);
        }
        var scroller = FindDescendant<ScrollViewer>(dash);
        if (scroller != null && scroller.ScrollableHeight > 0)
        {
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                scroller.ScrollToVerticalOffset(scroller.ScrollableHeight * (0.5 - 0.5 * Math.Cos(Math.PI * t)));
                Pump(30);
                Snap(dash, caption2, 50);
            }
        }
        Snap(dash, caption2, 1600);
        Close(dash);

        // 3. Order management: filter by status.
        var orders = (OrderListWindow)Open(() => new OrderListWindow(ADMIN.ToString()), 1280);
        const string caption3 = "Order management — filter and sort every order in real time";
        Snap(orders, caption3, 1600);
        orders.FilterProperty = BO.OrderListFilterProperty.Status;
        Pump(300);
        Snap(orders, caption3, 1000);
        orders.FilterValue = BO.OrderStatus.Delivered;
        Pump(300);
        Snap(orders, caption3, 1800);
        Close(orders);

        // 4. Order details.
        Still(() => new OrderWindow(ADMIN.ToString(), ORDER), 0,
              "Order details — customer, device, deadline and every delivery attempt", 2600);

        // 5. Couriers.
        Still(() => new CourierListWindow(), 1380,
              "Courier fleet — transport, availability and on-time performance at a glance", 2800);

        // 6. Courier workspace.
        Still(() => new CourierSelfWindow(COURIER), 0,
              "Courier workspace — personal profile and the order currently in progress", 2400);
        Still(() => new ChooseOrderWindow(COURIER), 0,
              "Couriers pick an open order within their delivery range", 2400);
        Still(() => new CourierHistoryWindow(COURIER), 0,
              "Full delivery history for every courier", 2600);
    }

    private static void Still(Func<Window> factory, double width, string caption, int holdMs)
    {
        var win = Open(factory, width);
        Snap(win, caption, holdMs);
        Close(win);
    }

    private static void Snap(Window win, string caption, int holdMs)
    {
        string file = $"{s_tour.Count:D4}.png";
        Capture(win, Path.Combine(s_outDir, "tour", file));
        s_tour.Add(new Shot(file, win.Title, caption, holdMs));
    }

    // ── Window helpers ───────────────────────────────────────────────────────

    private static Window Open(Func<Window> factory, double width = 0)
    {
        var win = factory();
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = 0;
        win.Top = 0;
        if (width > 0) win.Width = width;
        win.Show();
        win.Activate();
        Pump(1500);
        return win;
    }

    private static void Close(Window? win)
    {
        try { win?.Close(); } catch { }
        Pump(200);
    }

    // Makes the window tall enough that its main ScrollViewer shows everything.
    private static void GrowToFitScrollContent(Window win)
    {
        var scroller = FindDescendant<ScrollViewer>(win);
        if (scroller == null || scroller.ScrollableHeight <= 0) return;
        win.Height = Math.Min(win.ActualHeight + scroller.ScrollableHeight + 4, 2000);
        Pump(600);
    }

    private static Button? FindButton(DependencyObject root, string content) =>
        Descendants(root).OfType<Button>().FirstOrDefault(b => (b.Content as string)?.Trim() == content);

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject =>
        Descendants(root).OfType<T>().FirstOrDefault();

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node != root) yield return node;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                queue.Enqueue(VisualTreeHelper.GetChild(node, i));
        }
    }

    // Renders the window's client area (its template root, which carries the
    // background and the content's margins), or just the given element, to a PNG.
    private static void Capture(Window win, string path, FrameworkElement? element = null)
    {
        var root = element ?? (VisualTreeHelper.GetChildrenCount(win) > 0 ? VisualTreeHelper.GetChild(win, 0) as FrameworkElement : null)
            ?? throw new InvalidOperationException("Window has no visual tree");

        double w = root.ActualWidth, h = root.ActualHeight;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"Window has no size ({w}x{h})");

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(win.Background ?? Brushes.White, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(root)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, w, h),
                Stretch = Stretch.Fill,
            }, null, new Rect(0, 0, w, h));
        }

        var bmp = new RenderTargetBitmap(
            (int)Math.Ceiling(w * SCALE), (int)Math.Ceiling(h * SCALE),
            96 * SCALE, 96 * SCALE, PixelFormats.Pbgra32);
        bmp.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    private static void WriteJson(string file, List<Shot> shots)
    {
        var json = JsonSerializer.Serialize(shots, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        File.WriteAllText(Path.Combine(s_outDir, file), json);
    }

    private static void Pump(int milliseconds)
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
