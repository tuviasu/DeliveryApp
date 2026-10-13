# DeliveryApp

### Desktop Logistics & Delivery Control Center — a specialized delivery management system for computer-equipment & tech retailers. Built with WPF / .NET 8 on a clean 3-tier architecture.

<p align="center">
  <em>Role-based (Admin / Courier) delivery management with a live dashboard, real-time observer-driven updates, and swappable XML / in-memory persistence.</em>
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/"><img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white"></a>
  <a href="https://learn.microsoft.com/en-us/dotnet/csharp/"><img alt="C# 12" src="https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white"></a>
  <a href="https://learn.microsoft.com/en-us/dotnet/desktop/wpf/"><img alt="WPF" src="https://img.shields.io/badge/WPF-XAML-4F46E5?logo=windows&logoColor=white"></a>
  <img alt="Architecture" src="https://img.shields.io/badge/Architecture-3--Tier%20Layered-F59E0B">
  <img alt="Persistence" src="https://img.shields.io/badge/Persistence-XML%20%2F%20In--memory-10B981">
  <img alt="Patterns" src="https://img.shields.io/badge/Patterns-Factory%20%C2%B7%20Singleton%20%C2%B7%20Observer-8B5CF6">
  <img alt="License" src="https://img.shields.io/badge/License-Proprietary-94A3B8">
</p>

<p align="center">
  <a href="#-download--quick-start">Download</a> ·
  <a href="#-demo-login-credentials">Credentials</a> ·
  <a href="#-screenshots">Screenshots</a> ·
  <a href="#-architecture--design-patterns">Architecture</a>
</p>

---

## Download & Quick Start

> DeliveryApp is a **Windows desktop application** (WPF / .NET 8). Download the ZIP, extract, and double-click `DeliveryApp.exe` — no .NET installation required.

### [Download DeliveryApp.zip from GitHub Releases](https://github.com/tuviasu/DeliveryApp/releases/latest)

*(Self-contained Windows x64 · ~80 MB · No .NET install needed)*

**Installation steps:**

1. Click the link above and download **`DeliveryApp.zip`**.
2. Extract the ZIP anywhere on your computer.
3. Open the extracted folder and double-click **`DeliveryApp.exe`** to launch.
   - No CMD/console window will appear.
   - No BAT file needed.
   - The `xml/` data folder must stay next to `app/`.
4. Sign in with one of the demo accounts below.

---

## Demo Login Credentials

> These are **intentional public demo credentials** included so anyone can download and explore the application. They survive every Reset DB / Init DB operation.

| Role | User ID | Password | Description |
|---|---|---|---|
| **Admin / Manager** | `204857392` | `admin123` | Full access: live dashboard, orders, couriers, clock, DB tools |
| **Courier** | `312458962` | `courier123` | Courier workspace: pick orders, deliver, view history |

**How to log in:**

1. Launch `DeliveryApp.exe`.
2. Enter the **User ID** and **Password** from the table above.
3. Click **Sign In** — the app automatically routes you to the correct screen based on your role.

> Both roles are fully operational. The Admin role provides full management capability. The Courier role provides a focused personal workspace for picking up and completing deliveries.

---

## Screenshots

> Screenshots of the live application are pending capture. The screenshot folder structure is in place at `docs/screenshots/`. See [`docs/screenshots/SCREENSHOTS_PENDING.md`](docs/screenshots/SCREENSHOTS_PENDING.md) for capture instructions.
>
> **To contribute screenshots:** run `DeliveryApp.exe`, capture each screen listed in the file above, and save PNGs to `docs/assets/`.

---

## Key Highlights

- **Clean 3-Tier Architecture** — `PL` (WPF UI) → `BL` (business logic) → `DAL` (data). Every layer depends only on the interface of the one below it.
- **Pluggable persistence** — switch between XML serialization (`DalXml`) and in-memory lists (`DalList`) by editing one config line.
- **Observer-driven live UI** — BL exposes `AddObserver / RemoveObserver`; WPF windows refresh only affected data. No polling, no timers.
- **Thread-safe dispatch** — background notifications marshaled to the UI thread via WPF `Dispatcher`, with `ObserverMutex` preventing re-entrant refresh storms.
- **Auto Order IDs** — new orders receive the next sequential ID automatically; the Order ID field is read-only and never shows `0`.
- **Modern UI theme** — shared `App.xaml` keeps every window visually consistent: rounded cards, gradient buttons, focus-highlight inputs, semantic badge colors.

---

## Project Overview

**DeliveryApp** manages the end-to-end delivery operation of a computer-equipment & tech retailer. It handles **orders**, **couriers**, and **deliveries** through two distinct role experiences:

**Admin / Manager:**
- Live system dashboard with configurable clock
- Orders summary with filtering by status, schedule, type
- Full CRUD over orders and couriers
- System configuration (max distance, time windows, etc.)
- Database tools: Init DB, Reset DB
- Background delivery simulator

**Courier:**
- Personal profile view and edit
- Current in-progress delivery details
- Browse and claim available orders
- Complete deliveries with result type
- Full delivery history

---

## Architecture & Design Patterns

```
┌──────────────────────────────────────────────┐
│   PL  — Presentation Layer (WPF / XAML)      │  Data binding · converters · observers
├──────────────────────────────────────────────┤
│   BL  — Business Logic Layer (.NET 8)        │  Validation · rules · notifications
├──────────────────────────────────────────────┤
│   DalFacade — DAL contract (interfaces)      │  ICrud<T> · IDal · factory · config
├──────────────────────────────────────────────┤
│   DalList / DalXml — DAL implementations     │  In-memory list  ·  XML serialization
└──────────────────────────────────────────────┘
```

| Layer | Project | Responsibility |
|---|---|---|
| **PL** | `PL/` | WPF windows, data binding, value converters, observer-driven refresh |
| **BL** | `BL/` | Business rules, DTOs (`BO/`), manager helpers, observer hubs |
| **DalFacade** | `DalFacade/` | DAL interfaces (`ICrud<T>`, `IDal`), DO entities, `Factory`, exceptions |
| **DalList** | `DalList/` | In-memory implementation with seeded data |
| **DalXml** | `DalXml/` | XML-file persistence (serialization) |
| **DalTest** | `DalTest/` | Interactive DAL test console |
| **BITest** | `BITest/` | Backwards-compatibility / integration tests |
| **uitest** | `uitest/` | Headless UI + BL integration test harness |

### Design Patterns

- **Singleton** — `Factory.Get()` returns a single shared `IBl` / `IDal` instance.
- **Factory** — `BlApi.Factory` and `DalApi.Factory` decouple creation from implementation.
- **Observer** — the BL exposes `AddObserver / RemoveObserver`; PL windows subscribe and refresh only affected data.
- **Adapter / Facade** — `DalFacade` presents a uniform CRUD interface over two very different backends.
- **Dependency Injection via interfaces** — every layer programs to `IBl`, `ICourier`, `IOrder`, `IAdmin`, `ICrud<T>`.
- **Thread-safe observer dispatch** — `ObserverMutex` + `Dispatcher.BeginInvoke` marshal background notifications onto the UI thread.

---

## Technologies

| | |
|---|---|
| **Language** | C# 12 |
| **Platform** | .NET 8 (WPF, `net8.0-windows`) |
| **UI** | WPF + XAML, data binding, value converters, shared styled theme |
| **Data** | XML serialization (`DalXml`) / in-memory lists (`DalList`) |
| **Patterns** | Singleton, Factory, Observer, Facade |
| **Tests** | Headless UI + BL integration harness (`uitest`) |

---

## Local Setup & Run

### Prerequisites
- **.NET 8 SDK** (with WPF / Windows desktop workload) — Windows required for WPF.
- (Optional) Visual Studio 2022.

### 1. Clone
```bash
git clone https://github.com/tuviasu/DeliveryApp.git
cd DeliveryApp
```

### 2. Build
```bash
dotnet build DeliveryApp.sln -c Debug
```

### 3. Run
```bash
# From the repo root — the shared bin/ output satisfies the xml\ path requirement
bin/DeliveryApp.exe
```
Sign in with the demo credentials above.

### 4. Switch the data backend (optional)
Edit `xml/dal-config.xml`:
```xml
<dal>xml</dal>   <!-- or: list -->
```

### 5. Run tests
```bash
dotnet run --project uitest/uitest.csproj
```
Prints `ALL TESTS OK.` on success.

---

## Build & Release Package

GitHub Actions builds and publishes the self-contained `DeliveryApp.zip` automatically on every push to `main`. There is always exactly **one permanent release** at:

```
https://github.com/tuviasu/DeliveryApp/releases/latest
```

To build manually:
```bash
dotnet publish PL/PL.csproj -c Release -r win-x64 --self-contained true -o publish/app
cp -r xml publish/xml
# zip publish/ -> DeliveryApp.zip
```

---

## Project Structure

```
DeliveryApp/
├── PL/                     # WPF presentation layer (startup project)
│   ├── App.xaml            # Shared modern theme (buttons, cards, inputs)
│   ├── LoginPage.xaml      # Role-aware login screen
│   ├── MainWindow.xaml     # Admin dashboard
│   ├── Courier/            # Courier windows
│   ├── Order/              # Order windows
│   ├── Converters/         # Value converters
│   └── Helpers/            # ObserverMutex, etc.
├── BL/                     # Business logic + BO DTOs + manager helpers
├── DalFacade/              # DAL interfaces & DO entities
├── DalList/                # In-memory DAL
├── DalXml/                 # XML-persistence DAL
├── DalTest/                # Interactive DAL test console
├── BITest/                 # Integration tests
├── uitest/                 # Headless UI + BL integration harness
├── docs/                   # Screenshots and documentation
├── xml/                    # Runtime / seed data files
└── DeliveryApp.sln
```

---

## Changelog

### Latest (current)
- Single permanent GitHub Release — always the latest build at the same URL.
- Both login roles (Admin and Courier) verified and documented.
- Demo credentials displayed publicly in README and Release description.
- Cleanup of old numbered versioned releases.

### v2.0
- Auto-assigned sequential Order IDs (max existing + 1); Order ID field read-only.
- Direct `DeliveryApp.exe` launch — no BAT file, no CMD window.
- Fixed Delivery Type badge colors: blue-gray slate for null/unknown values.
- Self-contained Windows x64 package, no .NET install required.

### v1.0
- Initial release.

---

## Notes for Reviewers

- **Layering is enforced through interfaces**, not just folders — the BL references `DalFacade` (the contract), never a concrete DAL.
- **Live UI updates** are driven by the Observer pattern — a background change in the BL notifies only the subscribed windows, no timers.
- **The DAL is swappable at runtime** via a single config line, demonstrating the Factory + Facade patterns.
- **UI consistency** is centralized in `App.xaml` (`AppPrimaryButton`, `AppCard`, `AppTextBox`, …).
- **Demo credentials** are restored on every Reset DB / Init DB, so login always works.

---

## License

Proprietary — provided as-is for portfolio and demonstration purposes.
