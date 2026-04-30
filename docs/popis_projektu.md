# 🍴 Objednávací systém v menze (UTB Minute)

Semestrální projekt do předmětu Aplikační frameworky.

---

## 👥 Členové týmu a poměr práce

| Jméno             | Role v týmu            | Poměr práce |
| ----------------- | ---------------------- | ----------- |
| Egor              | Datový model           | 1           |
| Artem             | WebAPI & SSE & Backend | 1           |
| Ruslana           | Blazor klient & UI     | 1           |
| Vitalik – vedoucí | Testovani              | 1           |

_Poznámka: Poměr práce 1:1:1 značí rovnoměrný přínos všech členů._

---

## 🚀 Spuštění projektu

**Požadavky:** .NET 10 SDK, Docker Desktop (nutný pro běh SQL Serveru a Keycloaku v Aspire).

**Postup:**

1. Spusťte Docker Desktop.
2. Otevřete solution **UTB.Minute.sln** ve JetBrains Rider.
3. Nastavte projekt **UTB.Minute.AppHost** jako **Start-up projekt**.
4. Spusťte projekt.
5. V prohlížeči se otevře **.NET Aspire Dashboard**, kde uvidíte stav všech služeb a odkazy na klientské aplikace (AdminClient, CanteenClient) a WebApi.

_Poznámka: Keycloak realm a uživatelé se importují automaticky ze souborů `AppHost/Realms/` při každém startu. SQL data se seedují automaticky při startu WebAPI. Ruční import Docker volumes není potřeba._

---

## 🔑 Testovací účty

| Uživatel | Heslo   | Role    |
| -------- | ------- | ------- |
| admin    | admin   | Admin   |
| cook     | cook    | Cook    |
| student1 | student | Student |
| student2 | student | Student |

## 📂 Struktura řešení

Struktura odpovídá zadání. Jeden CanteenClient slouží jak pro studenty, tak pro kuchařky; přístup je rozlišen podle role (Student / Cook) v menu i na API.

| Projekt                      | Popis                                                                                                                                                        |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **UTB.Minute.AppHost**       | Aspire orchestrace: SQL Server, Keycloak, DbManager, WebApi, AdminClient, CanteenClient. Service Discovery, datové volume pro SQL a Keycloak.                |
| **UTB.Minute.Db**            | Datové entity (Food, MenuItem, Order) a DbContext.                                                                                                           |
| **UTB.Minute.DbManager**     | Obsahuje endpoint **POST /db/reset** pro reset databáze (smazání, vytvoření). Seed dat probíhá při startu WebApi.                                            |
| **UTB.Minute.Contracts**     | Sdílená DTO a enum OrderStatus; zajištěna typová shoda mezi WebAPI a klienty.                                                                                |
| **UTB.Minute.WebApi**        | Hlavní API: jídla, menu, objednávky, SSE notifikace. Autorizace dle rolí.                                                                                    |
| **UTB.Minute.AdminClient**   | Blazor Server aplikace pro vedení menzy (správa jídel a menu, kopírování menu). Pouze role Admin.                                                            |
| **UTB.Minute.CanteenClient** | Blazor Server pro studenty a kuchařky: studenti – dnešní menu a mé objednávky; kuchařky – kuchyň (aktivní objednávky). Autorizace dle rolí (Student / Cook). |

---

## 🛠️ Klíčová implementační rozhodnutí

### 1. Autorizace a Keycloak

- **Keycloak** je spouštěn přes Aspire z AppHostu, realm je importován z `AppHost/Realms` (např. `minute-realm.json`).
- **Role v Keycloaku:** `Admin`, `Student`, `Cook`. Klienti (minute-blazor) a API (minute.api) používají stejný realm.
- **WebAPI** ověřuje JWT tokeny (AddKeycloakJwtBearer). Endpointy jsou zabezpečeny pomocí **RequireAuthorization** a **RequireRole**:
  - `/foods` – role **Admin**
  - `/menu` (CRUD) – role **Admin**
  - `/menu/today` – role **Student**
  - `/orders` (GET my orders, POST create) – role **Student** (Admin explicitně odmítnut – Forbid)
  - `/orders/active`, PUT `/orders/{id}/status` – role **Cook**
- **SSE** endpoint `/sse/orders` je **AllowAnonymous** (broadcast bez zabezpečení dle zadání).

### 2. Real-time notifikace (SSE)

- **WebAPI** používá službu **OrderNotificationService** (vzor INotifyPropertyChanged + IAsyncEnumerable). Při vytvoření objednávky se volá **Notify("order-created")**, při změně stavu **Notify("order-updated")**. Všichni připojení klienti na **GET /sse/orders** dostanou stejnou zprávu (TypedResults.ServerSentEvents, event type `order`).
- **Blazor** (CanteenClient): stránky **My Orders** (student) a **Orders** (kuchař) po startu otevřou HTTP stream na `/sse/orders` (klient `api-sse` s nekonečným timeoutem). Při přijetí řádku `data: ...` zavolají **InvokeAsync** → načtení dat (**LoadOrders**) a **StateHasChanged()**, čímž dojde k automatickému překreslení seznamu bez ručního refreshi.

### 3. Business pravidla

- **Počet porcí:** Při objednání (POST /orders) se sníží **AvailablePortions** u příslušné položky menu. Entita **MenuItem** má sloupec **[Timestamp] RowVersion** (EF Core optimistic concurrency). Při souběhu dvou objednávek poslední porce jedna z nich skončí **DbUpdateConcurrencyException**; API vrací **Conflict** s hláškou typu „Someone beat you to it…“. Tím je zajištěna korektní správa porcí bez přeobjednání.
- **Pravidla přechodů stavů objednávky** (Cook): v endpointu PUT `/orders/{id}/status` jsou povolené přechody (např. Preparing → Ready, Ready → Completed) validované; neplatné přechody vrací **BadRequest**.

---

## 📝 Poznámky k odevzdání

- **Stav:** Projekt je plně funkční.
- **Testování:** Projekt obsahuje 24 integračních testů (xUnit), které pokrývají CRUD operace pro Foods, Menu a Orders, včetně ověření stavů objednávek, sold-out scénářů a zpracování chyb. Testy běží na InMemory databázi s falešnou autentizací místo Keycloaku, kde se role řídí pomocí hlavičky X-Test-Role.
- **Problémy:** Největší obtíž byla celková konfigurace Keycloaku (realm, klienti, role, integrace s Aspire). Postupně jsme vše vyřešili podle návodu z YouTube kurzu, který vysvětluje Keycloak v kontextu Aspire aplikace a který nám pomohl s nastavením.

---

## 🧪 Seznam API endpointů (ukázka)

| Metoda | Endpoint            | Popis                                                | Autorizace |
| ------ | ------------------- | ---------------------------------------------------- | ---------- |
| GET    | /foods              | Seznam všech jídel                                   | Admin      |
| POST   | /foods              | Vytvoření jídla                                      | Admin      |
| PUT    | /foods/{id}         | Úprava jídla (včetně aktivace/deaktivace)            | Admin      |
| GET    | /menu               | Všechny položky menu                                 | Admin      |
| GET    | /menu/today         | Menu na dnešek                                       | Student    |
| POST   | /menu               | Přidání položky menu                                 | Admin      |
| PUT    | /menu/{id}          | Úprava položky menu (datum, porce)                   | Admin      |
| POST   | /menu/copy          | Kopírování menu z jednoho data na druhé              | Admin      |
| DELETE | /menu/{id}          | Smazání položky menu                                 | Admin      |
| GET    | /orders             | Mé objednávky (student)                              | Student    |
| POST   | /orders             | Vytvoření objednávky                                 | Student    |
| GET    | /orders/active      | Aktivní objednávky (nedokončené)                     | Cook       |
| PUT    | /orders/{id}/status | Změna stavu objednávky                               | Cook       |
| GET    | /sse/orders         | SSE stream notifikací (order-created, order-updated) | Anonymous  |
| POST   | /db/reset           | Reset databáze (DbManager)                           | —          |
