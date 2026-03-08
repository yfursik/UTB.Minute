# Použití předimportovaného Docker volume při startu aplikace

Aspire v tomto projektu používá datová volume pro **Keycloak** a **SQL Server**. Následující postup popisuje export Keycloak dat nativním příkazem Keycloaku a import na novém počítači.

---

## Jak Aspire volume používá

V `UTB.Minute.AppHost/AppHost.cs` jsou definována volume:

- `WithDataVolume("sql")` – data SQL Serveru (databáze Minute)
- `WithDataVolume("keycloak")` – data Keycloaku (realm, uživatelé, relace)

Při prvním spuštění Aspire vytvoří volume s názvem odvozeným od projektu a resource. Přesný název zjistíte po prvním startu příkazem `docker volume ls` (hledejte `keycloak` v názvu).

---

<!--
## Keycloak: export (na stroji, kde už aplikace běžela)

1. Zastavte aplikaci (Aspire i kontejnery).
2. Zjistěte název volume, které Keycloak používá:
   ```bash
   docker volume ls
   ```
   Hledejte volume s `keycloak` v názvu (např. `minute_keycloak` nebo `keycloak`).
3. Vytvořte složku pro export (např. `C:\temp\kc-export`).
4. Spusťte nativní export Keycloaku. Do příkazu doplňte **skutečný název** vašeho keycloak volume místo `keycloak`:

   **PowerShell:**

   ```powershell
   docker run --rm `
     -v "C:\temp\kc-export:/opt/keycloak/data/export" `
     -v "keycloak:/opt/keycloak/data" `
     quay.io/keycloak/keycloak:26.4 `
     export --dir /opt/keycloak/data/export
   ```

   **Bash (Linux/macOS):**

   ```bash
   docker run --rm \
     -v "C:\temp\kc-export:/opt/keycloak/data/export" \
     -v "keycloak:/opt/keycloak/data" \
     quay.io/keycloak/keycloak:26.4 \
     export --dir /opt/keycloak/data/export
   ```

   Pokud se vaše volume jmenuje jinak (např. `minute_keycloak`), nahraďte druhý `-v` např. `-v "minute_keycloak:/opt/keycloak/data"`.

5. Po dokončení bude export v `C:\temp\kc-export`. Složku zazipujte a přeneste na nový počítač (USB, síť, cloud). -->

---

## Keycloak: import na novém zařízení (nový Docker)

1. Nainstalujte Docker Desktop a .NET 10 SDK.
2. Rozbalte přenesený export do složky (např. `C:\temp\kc-export` na Windows nebo `/tmp/kc-export` na Linuxu).
3. Vytvořte nové prázdné volume pro Keycloak. Název by měl odpovídat tomu, které Aspire použije pro Keycloak. Po prvním spuštění AppHostu můžete zkontrolovat `docker volume ls` a název sjednotit; pro první import stačí např. `keycloak`:
   ```bash
   docker volume create keycloak
   ```
4. Spusťte nativní import Keycloaku (obrázek 26.4, stejný jako u exportu). Cestu k rozbalenému exportu upravte podle vašeho systému:

   **PowerShell (Windows):**

   ```powershell
   docker run --rm `
     -v "C:\temp\kc-export:/opt/keycloak/data/import" `
     -v "keycloak:/opt/keycloak/data" `
     quay.io/keycloak/keycloak:26.4 `
     import --dir /opt/keycloak/data/import
   ```

   **Bash (Linux/macOS):**

   ```bash
   docker run --rm \
     -v "/tmp/kc-export:/opt/keycloak/data/import" \
     -v "keycloak:/opt/keycloak/data" \
     quay.io/keycloak/keycloak:26.4 \
     import --dir /opt/keycloak/data/import
   ```

5. Po dokončení importu je volume `keycloak` naplněné realmem, uživateli a klienty. Aby je Aspire použil při startu aplikace, musí Keycloak běžet s tímto volume:
   - **Varianta A:** Přejmenujte volume na název, který Aspire vytváří pro Keycloak (zjistíte po prvním spuštění AppHostu pomocí `docker volume ls`). Pak volume smažte, který Aspire vytvořil, a při příštím startu Aspire použije váš přejmenovaný volume.
   - **Varianta B:** Po prvním spuštění AppHostu zastavte aplikaci. Zkopírujte obsah volume `keycloak` do volume, které Aspire vytvořil (např. pomocí dočasného kontejneru a `tar` nebo `cp`). Pak při dalších startech Aspire použije již naplněné volume.
6. Spusťte solution s **UTB.Minute.AppHost** jako start-up projektem. Keycloak naběhne s předimportovanými daty (realm minute, uživatelé, klienti).

---

## Ověření po importu (volitelně)

V prohlížeči otevřete http://localhost:8080, přihlaste se jako admin a zkontrolujte realm minute a uživatele. Kontejner pak ukončete (Ctrl+C) a před startem Aspire přejmenujte nebo zkopírujte volume podle kroku 5 výše.
