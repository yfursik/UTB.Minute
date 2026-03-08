# Frontend guide — how our UTB Minute app works

This guide is for the person who owns the **full frontend** of the canteen ordering system. It explains how **our** Blazor applications are built: which app does what, which pages call which API, and where real-time updates come from. You don’t need to know every Razor detail — just how this solution is wired so you can change and extend it.

---

## 1. We have two Blazor apps (and one shared “contracts” project)

The solution has **two separate Blazor Server applications** that talk to the same WebApi:

| App                          | Who uses it                | Purpose                                                      |
| ---------------------------- | -------------------------- | ------------------------------------------------------------ |
| **UTB.Minute.AdminClient**   | Admin (canteen management) | Manage foods and menu. No ordering.                          |
| **UTB.Minute.CanteenClient** | Student and Cook           | Students: today’s menu and my orders. Cooks: kitchen orders. |

Both apps use the same **UTB.Minute.Contracts** project for DTOs (`FoodDto`, `MenuItemDto`, `OrderDto`, `OrderStatus`, etc.). So when the API returns JSON, we deserialize it into those types. All API calls go to the **WebApi** (URL comes from Aspire / config); there is no direct database access from the frontend.

---

## 2. AdminClient — structure and pages

AdminClient is **Admin-only**. After login (Keycloak), the user sees a nav menu with:

- **Food Management** (`/`) — list foods, add food, edit, deactivate (we don’t delete).
- **Menu Management** (`/menu`) — list menu items, add item (date + food + portions), edit (date, portions), delete, and **Copy menu** from one date to another.
- **Access Token** (`/token`) — debug page to see the JWT (optional).

**How it works in our app:**

- Every page has `@attribute [Authorize(Roles = "Admin")]` and `@inject IHttpClientFactory ClientFactory`.
- Data is loaded with the **"api"** client: `ClientFactory.CreateClient("api")`. Base URL is set in **Program.cs** from config (Aspire service discovery).
- **Food Management (Home.razor):** On load it calls **GET /foods**, fills a table. “Add” calls **POST /foods** with `CreateFoodDto`, then refetches. Edit opens a modal and calls **PUT /foods/{id}** with `UpdateFoodDto` (including `IsActive` for deactivate). So: one page, one place for list + add + edit.
- **Menu.razor:** Loads **GET /menu** and **GET /foods** (for the “Add to menu” dropdown). Add menu item = **POST /menu** with `CreateMenuItemDto`. Edit = **PUT /menu/{id}** with `UpdateMenuItemDto` (date + portions). Delete = **DELETE /menu/{id}`. Copy = **POST /menu/copy\*\* with `CopyMenuDto(FromDate, ToDate)`.
- There is **no SSE** in AdminClient; it’s plain load → show → button → API call → refetch.

So in our app, AdminClient is the place where you change anything about **foods** and **menu** (labels, layout, modals, copy UI, etc.).

---

## 3. CanteenClient — structure, roles, and pages

CanteenClient serves **Student** and **Cook**. Who sees what is controlled by **roles** and by the **NavMenu**.

**NavMenu (our app):**  
It uses **AuthorizeView**. If the user is **Student** (and not Cook), we show “Today’s Menu” and “My Orders”. If the user is **Cook**, we show only “Kitchen (Orders)”. If not logged in, we show “Sign in”. So the menu itself is role-based and lives in **Components/Layout/NavMenu.razor**.

**Pages:**

| Route        | Role                         | Page               | What it does in our app                                                                                                                                                                                                                                                                                                                                             |
| ------------ | ---------------------------- | ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/`          | Student (Cook is redirected) | **Home.razor**     | Today’s menu. Calls **GET /menu/today**. Shows cards with food name, description, price, portions. Sold-out items (0 portions) are grayed and show “Sold out”; “Order Now” is disabled. Student clicks “Order Now” → **POST /orders** with `CreateOrderDto(menuItemId)` → then refetches menu. On conflict (e.g. last portion taken) we show the API error message. |
| `/my-orders` | Student                      | **MyOrders.razor** | List of my orders. Calls **GET /orders**. Shows cards with order id, food name, status, created at. **Subscribes to SSE** (see below) so when the cook changes status, the list refreshes automatically.                                                                                                                                                            |
| `/orders`    | Cook                         | **Orders.razor**   | Kitchen dashboard. Calls **GET /orders/active**. Shows active orders with buttons: “Mark as Ready”, “Cancel”, “Handed out (Complete)”, etc. Each button calls **PUT /orders/{id}/status** with `UpdateOrderStatusDto`. **Subscribes to SSE** so when a student places an order, the list updates without refresh.                                                   |

**Redirect in our app:**  
Home.razor runs `OnInitializedAsync`, checks `AuthState`. If the user is in role **Cook**, it calls `Navigation.NavigateTo("/orders", forceLoad: true)` and returns — so Cook never sees the menu as home, only the kitchen view.

So in our app, CanteenClient is where you change **today’s menu** look, **my orders** list, **kitchen** list, buttons, and any copy or styling.

---

## 4. How our pages get data (the pattern we use)

Every page that shows data from the API follows the same pattern in **this** project:

1. A **field** to hold the data (e.g. `OrderDto[]? orders`, `MenuItemDto[]? menuItems`). Types come from **UTB.Minute.Contracts**.
2. A **method that loads data** (e.g. `LoadOrders()`, `LoadMenu()`): it creates a client with `ClientFactory.CreateClient("api")`, calls the right endpoint (e.g. **GET /orders**, **GET /menu/today**, **GET /orders/active**), and assigns the result to the field. Errors are stored in something like `errorMessage` and shown in the markup.
3. **OnInitializedAsync** calls that load method once when the page opens.
4. The **markup** uses the field: `@if (orders == null)` for loading, `@foreach (var order in orders)` for the list, Bootstrap cards/tables for layout.

So in our app: **open page → load method runs → API → field updated → UI renders**. When you add or change a page, you repeat this pattern and point to the right endpoint and DTO.

---

## 5. Buttons and API calls in our app

When the user clicks a button, we run C# that calls the API, then we refetch or update state so the UI reflects the new data. Examples from our app:

- **Home.razor (CanteenClient):** “Order Now” → `PlaceOrder(item.Id)` → **POST /orders** with `CreateOrderDto(menuItemId)` → on success we set `successMessage` and call `LoadMenu()` again (so portions update and sold-out appears if needed).
- **Orders.razor (Cook):** “Mark as Ready” → `UpdateStatus(order.Id, OrderStatus.Ready)` → **PUT /orders/{id}/status** with `UpdateOrderStatusDto(OrderStatus.Ready)` → on success we call `LoadOrders()` so the list refreshes.
- **AdminClient Home.razor:** “Add” food → **POST /foods** → then refetch foods. Edit → **PUT /foods/{id}** → refetch.

We use **@onclick** on buttons and pass a method (or lambda). No JavaScript. After the API call we either call the load method again or set a message and call **StateHasChanged()** so Blazor redraws. So in our app: **click → C# → HTTP call → refetch or set state → UI updates**.

---

## 6. Real-time updates (SSE) in our app

Only **two pages** in our project use real-time: **MyOrders.razor** (Student) and **Orders.razor** (Cook). The WebApi exposes **GET /sse/orders**: a long-lived stream that sends a line like `data: order-created` or `data: order-updated` when an order is created or its status changes.

**How we do it in our app:**

- When the page loads, we call `LoadOrders()` once, then we start a **background loop** `SubscribeToOrderEventsAsync` (we don’t await it so the page can render). We use the **"api-sse"** client (configured in CanteenClient **Program.cs** with infinite timeout) and **GET sse/orders**, then read the response stream line by line. When we see a line starting with `data: `, we call **InvokeAsync** so we’re back on the Blazor sync context, then we call **LoadOrders()** and **StateHasChanged()**. So the list refetches and the UI updates without the user doing anything.
- We pass a **CancellationToken** from a **CancellationTokenSource** stored in the page. We implement **IDisposable** and in **Dispose** we cancel that source so when the user navigates away, the loop exits and we don’t leak connections.

So in our app, real-time means: **Student’s “My Orders” and Cook’s “Kitchen” both subscribe to the same SSE endpoint; when any order event is pushed, they refetch their list and redraw.** You don’t need to change the SSE logic to style the pages or add new buttons; only if you add another “live” list would you reuse the same pattern (SubscribeToOrderEventsAsync + Load + StateHasChanged).

---

## 7. Layout and navigation in our app

- **AdminClient:** **MainLayout** and **NavMenu** in `Components/Layout/`. The menu is wrapped in **AuthorizeView Roles="Admin"**: only Admin sees “Food Management”, “Menu Management”, “Access Token”; otherwise “Sign in”.
- **CanteenClient:** Same idea. **NavMenu** uses **AuthorizeView** and **IsInRole("Student")** / **IsInRole("Cook")** to show either “Today’s Menu” + “My Orders” or “Kitchen (Orders)”. So when you want to change “what links appear for whom”, you edit **NavMenu.razor** in the right app.
- **Routes:** Each page’s **@page** defines the URL: AdminClient has `/`, `/menu`, `/token`. CanteenClient has `/`, `/my-orders`, `/orders`. To add a new screen in our app, add a new `.razor` with its own `@page` and, if it should be in the menu, add a **NavLink** in the right **AuthorizeView** block in **NavMenu**.

---

## 8. What you’ll change most of the time (in our app)

- **Texts, labels, layout, styling** — Edit the markup and Bootstrap classes in the `.razor` files. No need to touch C# if you’re only changing how things look.
- **AdminClient:** Food table, menu table, “Add” forms, “Copy menu” UI, modals for edit — all in **Home.razor** and **Menu.razor**.
- **CanteenClient:** Today’s menu cards, “Sold out” styling, “My Orders” / “Kitchen” cards, button labels — in **Home.razor**, **MyOrders.razor**, **Orders.razor**.
- **Who sees which link** — **NavMenu.razor** in each app (AuthorizeView and role checks).
- **New page** — New `.razor` with `@page`, inject `IHttpClientFactory`, load in `OnInitializedAsync` from the API endpoint we already have (or you’ll need a new one in WebApi), add a link in **NavMenu** in the right role block.
- **New button that does an action** — Add the button, set **@onclick** to a method that calls the API (same client `"api"`, same base URL), then refetch or set a message and **StateHasChanged()**.

---

## 9. Quick reference

| App           | Page            | Route        | API calls (our app)                                                                   | SSE? |
| ------------- | --------------- | ------------ | ------------------------------------------------------------------------------------- | ---- |
| AdminClient   | Food Management | `/`          | GET /foods, POST /foods, PUT /foods/{id}                                              | No   |
| AdminClient   | Menu Management | `/menu`      | GET /menu, GET /foods, POST /menu, PUT /menu/{id}, DELETE /menu/{id}, POST /menu/copy | No   |
| CanteenClient | Today’s Menu    | `/`          | GET /menu/today, POST /orders                                                         | No   |
| CanteenClient | My Orders       | `/my-orders` | GET /orders                                                                           | Yes  |
| CanteenClient | Kitchen         | `/orders`    | GET /orders/active, PUT /orders/{id}/status                                           | Yes  |

All HTTP calls use the **"api"** client except the SSE stream, which uses **"api-sse"** (same base URL, no timeout). DTOs and enums are in **UTB.Minute.Contracts**. With this map you can jump to the right file and the right endpoint when you change or extend the frontend.
