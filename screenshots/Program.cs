using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PL;
using PL.Courier;
using PL.Order;

internal static class Program
{
    // Screenshot generator for the README: opens each window against the bundled
    // xml\ demo data, lets it load, and renders its client area to a PNG.
    // Usage: screenshots.exe <output-dir>

    private const int ADMIN = 204857392;
    private const int COURIER = 312458962;
    private const int ORDER = 1000;
    private const double SCALE = 1.5; // render at 144 DPI for crisp images

    [STAThread]
    private static int Main(string[] args)
    {
        string outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "screenshots-out");
        Directory.CreateDirectory(outDir);

        Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
        {
            Console.WriteLine("  dispatcher: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
            e.Handled = true;
        };

        var app = new PL.App();
        app.InitializeComponent();

        var specs = new (string File, Func<Window> Factory)[]
        {
            ("login",            () => new LoginPage()),
            ("admin-dashboard",  () => new MainWindow(ADMIN)),
            ("order-list",       () => new OrderListWindow(ADMIN.ToString())),
            ("order-details",    () => new OrderWindow(ADMIN.ToString(), ORDER)),
            ("order-add",        () => new OrderWindow(ADMIN.ToString())),
            ("courier-list",     () => new CourierListWindow()),
            ("courier-details",  () => new CourierWindow(COURIER)),
            ("courier-dashboard",() => new CourierSelfWindow(COURIER)),
            ("choose-order",     () => new ChooseOrderWindow(COURIER)),
            ("courier-history",  () => new CourierHistoryWindow(COURIER)),
        };

        int failures = 0;
        foreach (var (file, factory) in specs)
        {
            Window? win = null;
            try
            {
                win = factory();
                win.WindowStartupLocation = WindowStartupLocation.Manual;
                win.Left = 0;
                win.Top = 0;
                win.Show();
                win.Activate();
                PumpDispatcher(1500);

                string path = Path.Combine(outDir, file + ".png");
                Capture(win, path);
                Console.WriteLine($"  saved {path}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL {file} -> {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                try { win?.Close(); } catch { }
                PumpDispatcher(200);
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static void Capture(Window win, string path)
    {
        if (win.Content is not FrameworkElement content)
            throw new InvalidOperationException("Window has no visual content");

        double w = content.ActualWidth, h = content.ActualHeight;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"Content has no size ({w}x{h})");

        // Paint the window background first so transparent regions don't come out black.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(win.Background ?? Brushes.White, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, w, h));
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
