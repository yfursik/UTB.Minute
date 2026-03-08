# UTB Minute — Architecture Overview

This document explains how the solution is structured and how its parts work together. No prior knowledge of Aspire or Blazor is required.

---

## What the system does (in one sentence)

**Students** order food from today’s menu on a panel; **cooks** see and update those orders in the kitchen; **admins** manage dishes and menu; everyone sees updates in **real time** via Server-Sent Events (SSE).

---

## High-level picture

```
                    ┌─────────────────────────────────────────────────────────┐
                    │              UTB.Minute.AppHost (orchestrator)            │
                    │  Starts everything, provides dashboard and config        │
                    └─────────────────────────────────────────────────────────┘
                                              │
         ┌────────────────────────────────────┼────────────────────────────────────┐
         │                                    │                                    │
         ▼                                    ▼                                    ▼
┌─────────────────┐                 ┌─────────────────┐                 ┌─────────────────┐
│  SQL Server     │                 │  Keycloak      │                 │  DbManager      │
│  (database)     │                 │  (login/roles) │                 │  POST /db/reset │
└────────┬────────┘                 └────────┬────────┘                 └────────┬────────┘
         │                                    │                                    │
         │         ┌──────────────────────────┴──────────────────────────┐         │
         └────────►│              UTB.Minute.WebApi                      │◄────────┘
                   │  Foods, Menu, Orders, SSE — single backend for all  │
                   └──────────────────────────┬──────────────────────────┘
                                              │
                    ┌─────────────────────────┼─────────────────────────┐
                    │                         │                         │
                    ▼                         ▼                         ▼
         ┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
         │ AdminClient     │       │ CanteenClient   │       │ Browser (SSE)    │
         │ (Blazor)        │       │ (Blazor)        │       │ GET /sse/orders  │
         │ Admin only      │       │ Student + Cook  │       │ (real-time)      │
         └─────────────────┘       └─────────────────┘       └─────────────────┘
```

- **AppHost** starts SQL Server, Keycloak, DbManager, WebApi, AdminClient, and CanteenClient and wires URLs via Service Discovery (no hardcoded IPs).
- **WebApi** is the only component that talks to the database and Keycloak; clients only call WebApi over HTTP (and optionally open the SSE stream).
- **AdminClient** and **CanteenClient** are two separate Blazor Server apps; both use the same WebApi and the same Contracts (DTOs).

---

## Projects and their roles

| Project                      | Purpose                                                                                          | Depends on                                                                              |
| ---------------------------- | ------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------- |
| **UTB.Minute.AppHost**       | Starts all services (DB, Keycloak, WebApi, clients), Aspire Dashboard                            | References all other app projects (for metadata); no runtime dependency on Db/Contracts |
| **UTB.Minute.Db**            | Entity classes (Food, MenuItem, Order) and MinuteDbContext                                       | Contracts (for OrderStatus enum)                                                        |
| **UTB.Minute.DbManager**     | Small API with `POST /db/reset` to drop and recreate the database; used by Aspire / Http Command | Db, SQL connection from AppHost                                                         |
| **UTB.Minute.Contracts**     | DTOs and shared types (e.g. FoodDto, OrderDto, OrderStatus); no logic                            | Nothing (leaf project)                                                                  |
| **UTB.Minute.WebApi**        | All business logic: foods, menu, orders, SSE; validates tokens and roles                         | Db, Contracts                                                                           |
| **UTB.Minute.AdminClient**   | Blazor UI for admins: manage foods and menu                                                      | Contracts, HTTP client to WebApi, Keycloak                                              |
| **UTB.Minute.CanteenClient** | Blazor UI for students (menu, my orders) and cooks (kitchen orders); role-based nav and pages    | Contracts, HTTP client to WebApi, Keycloak                                              |

**Important:** Clients never reference **UTB.Minute.Db**. They only know **Contracts** (DTOs). So “how they connect” is: **HTTP + JSON + shared DTOs**.

---

## How data flows

1. **Admin adds a food**  
   AdminClient → `POST /foods` (with auth token) → WebApi → Db (insert Food) → response with FoodDto.

2. **Admin adds a menu item**  
   AdminClient → `POST /menu` with date, foodId, portions → WebApi → Db (insert MenuItem) → response.

3. **Student sees today’s menu**  
   CanteenClient → `GET /menu/today` (Student role) → WebApi → Db (menu items for today) → list of MenuItemDto.

4. **Student creates an order**  
   CanteenClient → `POST /orders` with menuItemId → WebApi decreases portions, inserts Order, then calls **OrderNotificationService.Notify("order-created")** → response.  
   All open SSE clients (e.g. cook’s Orders page) receive the event and refetch their list.

5. **Cook changes order status**  
   CanteenClient → `PUT /orders/{id}/status` with new status → WebApi updates Order, then **Notify("order-updated")** → response.  
   All SSE clients (e.g. student’s My Orders) receive the event and refetch; UI updates automatically.

6. **SSE (real-time)**  
   Any client can open `GET /sse/orders` (no auth). The response is a long-lived stream. When CreateOrder or UpdateOrderStatus runs, WebApi notifies all such streams; each Blazor page that subscribed refetches its data (my orders or active orders) and calls `StateHasChanged()`, so the UI updates without a manual refresh.

So the “connection” between projects is: **HTTP for requests**, **SSE for push**, and **Contracts for the shape of data**. No direct DB or entity types in the clients.

---

## Where things live (summary)

- **Entities and database:** UTB.Minute.Db (Food, MenuItem, Order, MinuteDbContext).
- **API and business rules:** UTB.Minute.WebApi (endpoints, authorization, SSE, OrderNotificationService).
- **Shared data shapes:** UTB.Minute.Contracts (DTOs, OrderStatus).
- **Orchestration and startup:** UTB.Minute.AppHost (Aspire).
- **Reset DB:** UTB.Minute.DbManager (`POST /db/reset`).
- **UI:** UTB.Minute.AdminClient and UTB.Minute.CanteenClient (Blazor Server, calling WebApi and optionally subscribing to SSE).

This structure keeps a single backend, clear separation (DB vs API vs UI), and one place for DTOs so clients and API stay in sync.
