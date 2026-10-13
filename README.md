# 📦 DeliveryApp

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
  <a href="#-quick-start--download">⬇️ Download</a> ·
  <a href="#-demo-credentials">🔑 Credentials</a> ·
  <a href="#-screenshots--demo">📸 Screenshots</a> ·
  <a href="#-architecture--design-patterns">🏗️ Architecture</a>
</p>

---

## ⭐ Quick Start & Download

> DeliveryApp is a **Windows desktop application** (WPF / .NET 8) — it does not run in a browser tab. The fastest way to try it is the self-contained ZIP below: download, unzip, double-click the launcher, and you're in.

> ### 👉 [Download `DeliveryApp.zip` from GitHub Releases](https://github.com/tuviasu/DeliveryApp/releases/latest)  *(self-contained Windows x64, ~68 MB, no .NET install required)*

1. **Download** `DeliveryApp.zip` from the link above and **unzip** it anywhere.
2. Inside the unzipped folder, double-click **`DeliveryApp.exe`** to launch — no BAT file, no CMD window.
3. Sign in with the **Admin** credentials below.

> The data files live in `xml/` (sibling of `DeliveryApp.exe`). Keep them together — do not move the exe out of the folder.

Prefer to build from source? See [🛠️ Local Setup & Run](#-local-setup--run).

---

## 🔑 Demo Credentials

The app ships seeded with the credentials below. **These are permanent — they survive both `Reset DB` and `Init DB`.**

| Role | User ID | Password | Notes |
|---|---|---|---|
| **Admin / Manager** | `204857392` | `admin123` | Stored in `xml/data-config.xml` under `<Managers>`. |
| **Courier** | `312458962` | `courier123` | Name: **Yossi Cohen** · Transport: **Motorcycle** · Max distance: **35 km**. Seeded automatically on every launch and after every DB reset. |

> If you add more couriers via the Admin UI and then run **Init DB**, those new couriers are re-randomized — but the demo courier above is always restored with the same ID, name, and password.

---

## ✨ Key Highlights

- **🧱 Clean 3-Tier / Layered Architecture** — `PL` (WPF UI) → `BL` (business logic) → `DAL` (data). Every layer depends only on the **interface** of the one below it, so the BL never knows which storage engine is running.
- **🔌 Pluggable persistence** — switch between **XML serialization** (`DalXml`) and **in-memory lists** (`DalList`) by editing a single config line (`xml/dal-config.xml`). Demonstrates the Factory + Facade patterns in practice.
- **📡 Observer-driven live UI** — the BL exposes `AddObserver / RemoveObserver`; WPF windows subscribe and refresh only affected data (clock, config, orders, couriers). **No polling**, no timers. `INotifyPropertyChanged` + dependency properties drive the bindings.
- **🧵 Thread-safe dispatch** — background notifications are marshaled onto the UI thread via the WPF `Dispatcher`, with a dedicated `ObserverMutex` preventing re-entrant refresh storms.
- **🧩 Design patterns throughout** — Singleton (`Factory.Get()`), Factory (BL & DAL creation), Observer (live updates), Facade/Adapter (uniform CRUD over two backends), DI via interfaces.
- **🎨 Cohesive modern UI** — a shared theme (`App.xaml`) keeps every window visually consistent: rounded buttons/cards, focus-highlight inputs, gradient accents.

---

## 🎯 Project Overview

**DeliveryApp** is a desktop application that drives the end-to-end delivery operation of a computer-equipment & tech retailer. It manages **orders**, **couriers**, and **deliveries**, and exposes two distinct experiences through a single login screen:

- **Admin / Manager** — full operational control: a system dashboard with a configurable clock, live orders summary with filtering, system configuration, and CRUD over couriers and orders.
- **Courier** — a focused self-service workspace: view/edit personal details, pick an available order, handle an in-progress delivery, and review past delivery history.

DeliveryApp is engineered around **clean architecture, design patterns, and real-world system design**.

---

## 📸 Screenshots & Demo

> Screenshots are coming soon. See [`docs/screenshots/SCREENSHOTS_PENDING.md`](docs/screenshots/SCREENSHOTS_PENDING.md) for instructions on adding them. Placeholder paths below will be replaced once captures are available.

| Login Screen | Admin Dashboard | Courier Dashboard |
|---|---|---|
| <img src="docs/assets/login.png" alt="Login" width="420"> | <img src="docs/assets/admin_dashboard.png" alt="Admin Dashboard" width="420"> | <img src="docs/assets/courier_view.png" alt="Courier Dashboard" width="420"> |

| Order Management | Order Details | Courier List |
|---|---|---|
| <img src="docs/assets/order_management.png" alt="Order Management" width="420"> | <img src="docs/assets/order_details.png" alt="Order Details" width="420"> | <img src="docs/assets/courier_list.png" alt="Courier List" width="420"> |

| Choose Order | Delivery History | Courier Add/Edit |
|---|---|---|
| <img src="docs/assets/choose_order.png" alt="Choose Order" width="420"> | <img src="docs/assets/delivery_history.png" alt="Delivery History" width="420"> | <img src="docs/assets/courier_edit.png" alt="Courier Add/Edit" width="420"> |

---

## 🏗️ Architecture & Design Patterns

The solution follows a strict **3-Tier Layered Architecture** with a shared facade contract. Each layer depends only on the abstraction of the layer below it.

```
┌──────────────────────────────────────────────┐
│   PL  — Presentation Layer (WPF / XAML)      │  Data binding · commands · observers
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
| **PL** | `PL/` | WPF windows, data binding, converters, observer-driven refresh |
| **BL** | `BL/` | Business rules, DTOs (`BO/`), manager helpers, observer hubs |
| **DalFacade** | `DalFacade/` | DAL interfaces (`ICrud<T>`, `IDal`), DO entities, `Factory`, exceptions |
| **DalList** | `DalList/` | In-memory implementation with seeded data |
| **DalXml** | `DalXml/` | XML-file persistence (serialization) |
| **DalTest** | `DalTest/` | Interactive DAL test console |
| **BITest** | `BITest/` | Backwards-compatibility / integration tests |
| **uitest** | `uitest/` | Headless UI + BL integration test harness |

The active DAL is chosen at runtime via `xml/dal-config.xml` (`<dal>xml</dal>` → DalXml, `list` → DalList) and instantiated through the **Factory** pattern, so the BL never knows which storage engine is running.

### Design Patterns Used

- **Singleton** — `Factory.Get()` returns a single shared `IBl` / `IDal` instance.
- **Factory** — `BlApi.Factory` and `DalApi.Factory` decouple creation from implementation.
- **Observer** — the BL exposes `AddObserver / RemoveObserver`; PL windows subscribe and refresh only the affected data (clock, config, orders, couriers) — no polling. WPF `INotifyPropertyChanged` + dependency properties propagate changes to the UI.
- **Adapter / Facade** — `DalFacade` presents a uniform CRUD interface over two very different backends.
- **Dependency Injection via interfaces** — every layer programs to interfaces (`IBl`, `ICourier`, `IOrder`, `IAdmin`, `ICrud<T>`).
- **Thread-safe observer dispatch** — `ObserverMutex` + `Dispatcher.BeginInvoke` marshal background notifications onto the UI thread and prevent re-entrant refresh storms.

### Concurrency & Reliability

- Background observer callbacks are marshaled to the UI thread via the WPF `Dispatcher`.
- A dedicated `ObserverMutex` guards each refresh path against re-entrancy and schedules a re-run if a notification arrives mid-refresh.
- Password verification accepts both hashed and legacy plaintext stored values, so seeded demo data logs in cleanly.
- Network distance lookups (OSRM) degrade gracefully to `null` on any HTTP/parse failure, so the simulator and the open-orders list stay responsive even when the public OSRM demo server is unavailable.

---

## ✨ Core Features

### 🛡️ Admin / Manager
- **System Clock** — advance the simulated clock by minute / hour / day / month / year; the UI updates live.
- **System Configuration** — tune max delivery distance, max delivery time, risk range, and inactivity time.
- **Orders Summary** — filter the live order count by _Delivery Done Type_ and _Schedule Status_, then jump straight to the filtered order list.
- **Courier Management** — list, filter by transport, add, update, and delete couriers.
- **Order Management** — list, filter, sort, add, update, and cancel orders; inspect every delivery attached to an order.
- **Database Tools** — initialize (re-seed) or reset the database from the dashboard.

### 🚚 Courier
- **Personal Details** — view and update name, phone, email, transport, max distance, and delivery type.
- **Order In Progress** — see the active delivery (address, customer, phone, time windows) and finish handling with a result type.
- **Choose Order** — browse available orders and claim one.
- **Delivery History** — review every past delivery with handling time and result.

### 🔐 Authentication
- A single modern login screen routes users to the correct experience based on credentials.
- Inline validation feedback (no jarring message boxes for routine input errors).
- Show/hide password toggle, Enter-to-submit, role badges.

---

## 🛠️ Technologies

| | |
|---|---|
| **Language** | C# 12 |
| **Platform** | .NET 8 (WPF, `net8.0-windows`) |
| **UI** | WPF + XAML, data binding, value converters, shared styled theme |
| **Data** | XML serialization (`DalXml`) / in-memory lists (`DalList`) |
| **Patterns** | Singleton, Factory, Observer, Facade |
| **Query** | LINQ |
| **Tests** | Headless UI + BL integration harness (`uitest`), interactive DAL console (`DalTest`), integration tests (`BITest`) |

---

## 🚀 Local Setup & Run

### Prerequisites
- **.NET 8 SDK** (with WPF / Windows desktop workload) — Windows required for the WPF UI.
- (Optional) Visual Studio 2022 for an IDE experience.

### 1. Clone
```bash
git clone https://github.com/tuviasu/DeliveryApp.git
cd DeliveryApp
```

### 2. Build
```bash
dotnet build DeliveryApp.sln -c Debug
```
All projects share a common output directory: `bin/`.

### 3. Run
The startup project is **`PL`** (`StartupUri="LoginPage.xaml"`). The app must run from a directory whose **parent contains an `xml/` folder** (DalXml loads data from `..\xml\`). The shared `bin/` output satisfies this (`bin/../xml`).

```bash
# From the repo root:
dotnet bin/PL.dll
#   — or —
bin/PL.exe
```

Sign in with the **Admin** credentials above to reach the dashboard.

### 4. Switch the data backend (optional)
Edit `xml/dal-config.xml`:
```xml
<dal>xml</dal>   <!-- or: list -->
```

### 5. Run the headless test harness (optional)
```bash
dotnet build DeliveryApp.sln -c Debug
dotnet bin/uitest.dll
```
The harness exercises authentication, ResetDB/InitializeDB, order/courier CRUD, filtering, sorting, the simulator, and instantiates every WPF window — it prints `ALL TESTS OK.` on success.

---

## 📦 Release Package

A self-contained, no-.NET-install-required build is published for Windows x64:

```bash
dotnet publish PL/PL.csproj -c Release -r win-x64 --self-contained true -o publish/app
cp -r xml publish/xml
```

The packaged archive **`DeliveryApp.zip`** (on the [Releases page](https://github.com/tuviasu/DeliveryApp/releases/latest)) contains a `DeliveryApp_v2.0/` folder with:
- `DeliveryApp.exe` — the self-contained executable (double-click to launch, no BAT file needed),
- `xml/` — the data files (`orders.xml`, `deliveries.xml`, `couriers.xml`, `data-config.xml`, `dal-config.xml`),
- runtime DLLs and resources.

Unzip anywhere and double-click **`DeliveryApp.exe`**.

---

## 🗂️ Project Structure

```
DeliveryApp/
├── PL/                     # WPF presentation layer (startup project)
│   ├── App.xaml            # App + shared modern theme (buttons/cards/inputs)
│   ├── LoginPage.xaml      # Modern role-aware login screen
│   ├── MainWindow.xaml     # Admin dashboard (clock, config, summary, actions)
│   ├── Courier/            # Courier windows (list, self, edit, history, choose-order)
│   ├── Order/              # Order windows (list, details)
│   ├── Converters/         # Value converters
│   └── Helpers/            # ObserverMutex, etc.
├── BL/                     # Business logic + BO DTOs + manager helpers
├── DalFacade/              # DAL interfaces & DO entities
├── DalList/                # In-memory DAL
├── DalXml/                 # XML-persistence DAL
├── DalTest/                # Interactive DAL test console
├── BITest/                 # Integration tests
├── uitest/                 # Headless UI + BL integration harness
├── docs/assets/            # Screenshots & demo media
├── xml/                    # Runtime / seed data files
└── DeliveryApp.sln
```

---

## 🎬 Demo Video Script

A short (60–90s) walkthrough. See the full script in [`docs/DEMO_VIDEO_SCRIPT.md`](docs/DEMO_VIDEO_SCRIPT.md).

**Flow:** Login → Admin Order Management → Courier Assignment & Status Update → Architecture recap.

---

## 📝 Notes for Reviewers

- **Layering is enforced through interfaces**, not just folders — the BL references `DalFacade` (the contract), never a concrete DAL.
- **Live UI updates** are driven by the Observer pattern, not timers — a background change in the BL notifies only the subscribed windows.
- **The DAL is swappable at runtime** via a single config line, demonstrating the Factory + Facade patterns in practice.
- **UI consistency** is centralized: a shared theme in `App.xaml` (`AppPrimaryButton`, `AppCard`, `AppTextBox`, …) keeps every window visually coherent.
- **Deterministic demo credentials** are restored on every `Reset DB` / `Init DB`, so the login above always works.

---

## 📋 Changelog

### v2.0 (latest)
- **No more BAT file**: `DeliveryApp.exe` is now the direct entry point — just double-click, no CMD window appears.
- **Auto Order IDs**: New orders receive a sequential ID automatically (max existing ID + 1, starting from 1000). The Order ID field is now read-only in the Add form.
- **Better UI colors**: Delivery Type column now uses themed blue-gray instead of white for null/unknown values, matching the Transport Type column style.
- **Cleaner package structure**: Single folder with `DeliveryApp.exe` + `xml/` at the top level.

### v1.0
- Initial release.

---

## 📄 License

Proprietary — provided as-is for portfolio and demonstration purposes.