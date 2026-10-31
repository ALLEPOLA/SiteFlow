using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.Services;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

// Run from backend/SiteFlow.API. The configured server must permit CREATE DATABASE.
// Only a unique, disposable siteflow_test_* database is migrated and removed.
var apiDirectory = Directory.GetCurrentDirectory();
using var configured = new AppDbContextFactory().CreateDbContext(Array.Empty<string>());
var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SITEFLOW_TEST_CONNECTION") ?? configured.Database.GetConnectionString() ?? throw new Exception("A test connection is required."));
var database = "siteflow_test_" + Guid.NewGuid().ToString("N");
connection.Database = database;
var options = new DbContextOptionsBuilder<AppDbContext>().UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36))).Options;
Process? server = null;
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}

// Validate export containers independently of the HTTP API.
var sample = new List<string[]> { new[] { "Name", "Value" }, new[] { "=1+1", "100.50" }, new[] { "Cement & steel", "සිංහල" } };
using (var zip = new ZipArchive(new MemoryStream(ReportFiles.Excel(sample))))
{
    Check(zip.GetEntry("xl/workbook.xml") is not null, "Excel workbook package exists");
    using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
    var xml = reader.ReadToEnd();
    Check(xml.Contains("inlineStr") && xml.Contains("=1+1") && !xml.Contains("<f>"), "Excel exports user text without executable formulas");
    Check(xml.Contains("සිංහල"), "Excel export preserves Unicode");
}
var pdf = Encoding.ASCII.GetString(ReportFiles.Pdf("Test", sample));
Check(pdf.StartsWith("%PDF-1.4") && pdf.Contains("startxref") && pdf.Contains("%%EOF"), "PDF has document structure and cross-reference table");
Check(OperationRules.Money(0.001m, 5m) == 0.01m, "Half-cent line totals round away from zero");
Check(OperationRules.Quantity(-1.001m) && !OperationRules.Quantity(0.0001m) && !OperationRules.Quantity(1000000000m), "Stock quantities enforce precision and bounds");
Check(OperationRules.Price(0) && !OperationRules.Price(-1) && !OperationRules.Price(1.001m), "Prices enforce nonnegative two-decimal values");
var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "assigned-user"), new Claim(ClaimTypes.Role, "SiteEngineer") }, "tests"));
using (var scoped = new AppDbContext(options, http))
{
    var queries = new[] {
        scoped.Projects.Select(p => p.Id).ToQueryString(),
        scoped.MaterialRequests.Select(p => p.Id).ToQueryString(),
        scoped.PurchaseOrders.Select(p => p.Id).ToQueryString(),
        scoped.GoodsReceiptLines.Select(p => p.Id).ToQueryString(),
        scoped.MaterialIssues.Select(p => p.Id).ToQueryString(),
        scoped.StockMovements.Select(p => p.Id).ToQueryString(),
        scoped.SupplierInvoices.Select(p => p.Id).ToQueryString(),
        scoped.DocumentAttachments.Select(p => p.Id).ToQueryString()
    };
    Check(queries.All(q => q.Contains("ProjectAssignments") && q.Contains("assigned-user")), "Project filters compile through every project-owned record type");
}
if (args.Contains("--offline"))
{
    var script = configured.GetService<IMigrator>().GenerateScript();
    Check(script.Contains("CREATE TABLE `InventoryOperations`") && script.Contains("CREATE TABLE `SupplierInvoices`") && script.Contains("CREATE TABLE `AuditEntries`"), "MySQL migration script includes ERP tables");
    Check(!configured.Database.HasPendingModelChanges(), "Migration snapshot matches the current data model");
    Console.WriteLine($"ALL {checks} OFFLINE CHECKS PASSED. Database integration checks require a disposable test database.");
    return;
}

try
{
    await using (var db = new AppDbContext(options)) await db.Database.MigrateAsync();
    Console.WriteLine("Disposable database migrated.");
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var password = "Integration!9aA" + Guid.NewGuid().ToString("N");
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = apiDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add(Path.Combine(apiDirectory, "bin", "erp-check", "SiteFlow.API.dll"));
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    start.Environment["ConnectionStrings__DefaultConnection"] = connection.ConnectionString;
    start.Environment["InitialAdmin__Email"] = "admin@siteflow-tests.invalid";
    start.Environment["InitialAdmin__Password"] = password;
    start.Environment["Logging__EventLog__LogLevel__Default"] = "None";
    start.Environment["DataProtection__KeyPath"] = Path.Combine(apiDirectory, "bin", "erp-check", "test-keys");
    server = Process.Start(start)!;
    var log = new StringBuilder();
    server.OutputDataReceived += (_, e) => { lock (log) { if (e.Data is not null) log.AppendLine(e.Data); } };
    server.ErrorDataReceived += (_, e) => { lock (log) { if (e.Data is not null) log.AppendLine(e.Data); } };
    server.BeginOutputReadLine(); server.BeginErrorReadLine();
    HttpClient Client() => new(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    using var admin = Client();
    var ready = false;
    for (var i = 0; i < 80; i++)
    {
        if (server.HasExited) throw new Exception("Test API exited during startup. Check configured MySQL permissions.");
        try { using var response = await admin.GetAsync("/api/auth/csrf"); if (response.IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    if (!ready) throw new Exception("Test API did not start.");

    async Task<JsonNode> Request(HttpClient client, string method, string path, object? body = null, int expected = 200)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET")
        {
            var csrf = await client.GetFromJsonAsync<JsonNode>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", csrf!["token"]!.GetValue<string>());
            request.Content = JsonContent.Create(body);
        }
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != expected)
            throw new Exception($"{method} {path}: expected {expected}, got {(int)response.StatusCode}. {content[..Math.Min(1500, content.Length)]}");
        checks++;
        return string.IsNullOrWhiteSpace(content) ? new JsonObject() : JsonNode.Parse(content) ?? new JsonObject();
    }
    int Id(JsonNode node) => node["id"]!.GetValue<int>();
    async Task Login(HttpClient client, string email, string pass) => await Request(client, "POST", "/api/auth/login", new { email, password = pass });
    await Login(admin, "admin@siteflow-tests.invalid", password);
    using var anonymous = Client();
    await Request(anonymous, "GET", "/api/project-costs", expected: 401);
    Check(true, "Anonymous access is denied");
    var project = await Request(admin, "POST", "/api/projects", new { code = "TEST1", name = "Test Project", location = "Test", budget = 1000 }, 201);
    var project2 = await Request(admin, "POST", "/api/projects", new { code = "TEST2", name = "Other Project", location = "Test", budget = 1000 }, 201);
    var material = await Request(admin, "POST", "/api/materials", new { code = "TESTMAT", name = "Test cement", category = "Test", unit = "bag" }, 201);
    var material2 = await Request(admin, "POST", "/api/materials", new { code = "TESTMAT2", name = "Test steel", category = "Test", unit = "kg" }, 201);
    var supplier = await Request(admin, "POST", "/api/suppliers", new { code = "TESTSUP", name = "Test supplier" }, 201);
    var p = Id(project); var p2 = Id(project2); var m = Id(material); var m2 = Id(material2); var s = Id(supplier);
    var staff = await Request(admin, "POST", "/api/users", new { fullName = "Engineer", email = "engineer@siteflow-tests.invalid", password, role = "SiteEngineer" }, 201);
    var userId = staff["id"]!.GetValue<string>();
    using var engineer = Client();
    await Login(engineer, "engineer@siteflow-tests.invalid", password);
    await Request(engineer, "GET", "/api/projects", expected: 403);
    var newPassword = "Changed!9aA" + Guid.NewGuid().ToString("N");
    await Request(engineer, "POST", "/api/profile/change-password", new { currentPassword = password, newPassword, confirmPassword = newPassword });
    await Login(engineer, "engineer@siteflow-tests.invalid", newPassword);
    var noProjects = await Request(engineer, "GET", "/api/projects");
    Check(noProjects.AsArray().Count == 0, "Staff with no assignments see no projects");
    await Request(admin, "POST", "/api/administration/assignments", new { projectId = p, userId, reason = "Integration assignment" });
    var assigned = await Request(engineer, "GET", "/api/projects");
    Check(assigned.AsArray().Count == 1 && assigned[0]!["id"]!.GetValue<int>() == p, "Project queries enforce staff assignment");
    await Request(engineer, "GET", $"/api/projects/{p2}", expected: 404);
    await Request(engineer, "GET", "/api/users", expected: 403);

    var day = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
    var request = await Request(engineer, "POST", "/api/material-requests", new { projectId = p, requiredDate = day, reason = "Materials", lines = new[] { new { materialId = m, quantity = 10, estimatedUnitPrice = 10 }, new { materialId = m2, quantity = 10, estimatedUnitPrice = 10 } } }, 201);
    var r = Id(request);
    await Request(engineer, "PUT", $"/api/corrections/requests/{r}", new { requiredDate = day, reasonText = "Edited materials", reason = "Draft correction", lines = new[] { new { materialId = m, quantity = 10, price = 10 }, new { materialId = m2, quantity = 10, price = 10 } } });
    await Request(engineer, "POST", $"/api/material-requests/{r}/submit");
    await Request(admin, "POST", $"/api/material-requests/{r}/approve", new { comment = "Approved" });
    request = await Request(admin, "GET", $"/api/material-requests/{r}");
    var line1 = request["lines"]![0]!["id"]!.GetValue<int>();
    var line2 = request["lines"]![1]!["id"]!.GetValue<int>();
    var order = await Request(admin, "POST", "/api/purchase-orders", new { materialRequestId = r, supplierId = s, orderDate = day, expectedDeliveryDate = day, lines = new[] { new { materialRequestLineId = line1, quantity = 10, unitPrice = 10 }, new { materialRequestLineId = line2, quantity = 10, unitPrice = 10 } } }, 201);
    var o = Id(order);
    await Request(admin, "PUT", $"/api/corrections/orders/{o}", new { supplierId = s, orderDate = day, deliveryDate = day, reason = "Invalid excess", lines = new[] { new { materialId = line1, quantity = 11, price = 10 } } }, 409);
    await Request(admin, "PUT", $"/api/corrections/orders/{o}", new { supplierId = s, orderDate = day, deliveryDate = day, reason = "Price correction", lines = new[] { new { materialId = line1, quantity = 10, price = 10 }, new { materialId = line2, quantity = 10, price = 10 } } });
    await Request(admin, "POST", $"/api/purchase-orders/{o}/issue");
    order = await Request(admin, "GET", $"/api/purchase-orders/{o}");
    var ol1 = order["lines"]![0]!["id"]!.GetValue<int>();
    var ol2 = order["lines"]![1]!["id"]!.GetValue<int>();
    var receipt = await Request(admin, "POST", "/api/goods-receipts", new { clientRequestId = Guid.NewGuid(), purchaseOrderId = o, receivedDate = day, deliveryNoteNumber = "TEST1", lines = new[] { new { purchaseOrderLineId = ol1, quantity = 10 } } }, 201);
    var receipt2 = await Request(admin, "POST", "/api/goods-receipts", new { clientRequestId = Guid.NewGuid(), purchaseOrderId = o, receivedDate = day, deliveryNoteNumber = "TEST2", lines = new[] { new { purchaseOrderLineId = ol2, quantity = 10 } } }, 201);
    var gr = Id(receipt); var gr2 = Id(receipt2);
    var rl = receipt["lines"]![0]!["id"]!.GetValue<int>();
    var rl2 = receipt2["lines"]![0]!["id"]!.GetValue<int>();
    await Request(admin, "POST", $"/api/corrections/orders/{o}/cancel", new { reason = "Has receipts" }, 409);
    var issue = await Request(admin, "POST", "/api/material-issues", new { clientRequestId = Guid.NewGuid(), projectId = p, issueDate = day, purpose = "Work", receivedBy = "Test", lines = new[] { new { materialId = m, quantity = 4 } } }, 201);
    Check(issue["totalValue"]!.GetValue<decimal>() == 40, "Issues use stock cost");
    await Request(admin, "POST", $"/api/corrections/receipts/{gr}/reverse", new { reason = "Dependent issue" }, 409);
    await Request(admin, "POST", $"/api/corrections/issues/{Id(issue)}/reverse", new { reason = "Wrong issue" });
    await Request(admin, "POST", $"/api/corrections/issues/{Id(issue)}/reverse", new { reason = "Repeat" }, 409);
    await Request(admin, "POST", "/api/supplier-accounts/invoices", new { clientRequestId = Guid.NewGuid(), purchaseOrderId = o, number = "EXCESS", invoiceDate = day, dueDate = day, lines = new[] { new { receiptLineId = rl, quantity = 11, unitPrice = 10 } } }, 409);
    var invoice = await Request(admin, "POST", "/api/supplier-accounts/invoices", new { clientRequestId = Guid.NewGuid(), purchaseOrderId = o, number = "INV1", invoiceDate = day, dueDate = day, lines = new[] { new { receiptLineId = rl, quantity = 5, unitPrice = 10 } } });
    var inv = Id(invoice);
    await Request(admin, "POST", $"/api/supplier-accounts/invoices/{inv}/payments", new { clientRequestId = Guid.NewGuid(), amount = 51, date = day, reference = "Too much" }, 409);
    var payment = new { clientRequestId = Guid.NewGuid(), amount = 20, date = day, reference = "BANK1" };
    await Request(admin, "POST", $"/api/supplier-accounts/invoices/{inv}/payments", payment);
    await Request(admin, "POST", $"/api/supplier-accounts/invoices/{inv}/payments", payment);
    var invoiceList = await Request(admin, "GET", "/api/supplier-accounts");
    Check(invoiceList[0]!["outstanding"]!.GetValue<decimal>() == 30 && invoiceList[0]!["payments"]!.AsArray().Count == 1, "Payment retries do not duplicate and balances reconcile");
    await Request(admin, "POST", $"/api/supplier-accounts/invoices/{inv}/cancel", new { reason = "Paid invoice" }, 409);
    await Request(admin, "POST", $"/api/corrections/receipts/{gr}/reverse", new { reason = "Invoiced receipt" }, 409);
    var count = new { clientRequestId = Guid.NewGuid(), projectId = p, materialId = m, kind = "Count", quantity = 12, unitCost = 0, date = day, reason = "Physical count" };
    await Request(admin, "POST", "/api/inventory-operations", count);
    await Request(admin, "POST", "/api/inventory-operations", count);
    await Request(admin, "POST", "/api/inventory-operations", new { clientRequestId = count.clientRequestId, projectId = p, materialId = m, kind = "Count", quantity = 14, unitCost = 0, date = day, reason = "Physical count" }, 409);
    await Request(admin, "POST", "/api/inventory-operations", new { clientRequestId = Guid.NewGuid(), projectId = p, destinationProjectId = p2, materialId = m, kind = "Transfer", quantity = 2, unitCost = 0, date = day, reason = "Move stock" });
    await Request(admin, "POST", "/api/inventory-operations", new { clientRequestId = Guid.NewGuid(), projectId = p, materialId = m, kind = "Adjustment", quantity = -100, unitCost = 0, date = day, reason = "Excess" }, 409);
    await Request(admin, "POST", "/api/inventory-operations", new { clientRequestId = Guid.NewGuid(), projectId = p, materialId = m2, kind = "SupplierReturn", quantity = 2, unitCost = 0, goodsReceiptLineId = rl2, date = day, reason = "Damaged steel" });
    var balances = await Request(admin, "GET", "/api/inventory/balances");
    decimal Balance(int projectId, int materialId) => balances.AsArray().Single(b => b!["projectId"]!.GetValue<int>() == projectId && b["materialId"]!.GetValue<int>() == materialId)!["quantityOnHand"]!.GetValue<decimal>();
    Check(Balance(p, m) == 10 && Balance(p2, m) == 2 && Balance(p, m2) == 8, "Counts, transfers, and supplier returns reconcile project stock");
    var costs = await Request(admin, "GET", "/api/project-costs");
    var projectCost = costs["projects"]!.AsArray().Single(c => c!["id"]!.GetValue<int>() == p)!;
    Check(projectCost["receivedMaterialValue"]!.GetValue<decimal>() == 180 && projectCost["consumedMaterialCost"]!.GetValue<decimal>() == 0, "Cost report excludes reversed issues and subtracts supplier returns");
    var operations = (await Request(admin, "GET", "/api/inventory-operations")).AsArray();
    var returnedOperation = operations.Single(n => n!["kind"]!.GetValue<string>() == "SupplierReturn")!;
    await Request(admin, "POST", $"/api/inventory-operations/{Id(returnedOperation)}/reverse", new { reason = "Return correction" });
    await Request(admin, "POST", $"/api/corrections/receipts/{gr2}/reverse", new { reason = "Receipt correction after undoing return" });
    await Request(admin, "POST", $"/api/corrections/receipts/{gr2}/reverse", new { reason = "Duplicate reversal" }, 409);
    var transferOperation = operations.Single(n => n!["kind"]!.GetValue<string>() == "Transfer")!;
    await Request(admin, "POST", $"/api/inventory-operations/{Id(transferOperation)}/reverse", new { reason = "Transfer correction" });
    var countOperation = operations.Single(n => n!["kind"]!.GetValue<string>() == "Count")!;
    await Request(admin, "POST", $"/api/inventory-operations/{Id(countOperation)}/reverse", new { reason = "Count correction" });
    balances = await Request(admin, "GET", "/api/inventory/balances");
    Check(Balance(p, m) == 10 && Balance(p2, m) == 0 && Balance(p, m2) == 0, "Stock operation and receipt reversals preserve ledger quantities");

    using var secondAdmin = Client();
    await Login(secondAdmin, "admin@siteflow-tests.invalid", password);
    async Task<int> ConcurrentPayment(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<JsonNode>("/api/auth/csrf");
        using var pay = new HttpRequestMessage(HttpMethod.Post, $"/api/supplier-accounts/invoices/{inv}/payments");
        pay.Headers.Add("X-CSRF-TOKEN", csrf!["token"]!.GetValue<string>());
        pay.Content = JsonContent.Create(new { clientRequestId = Guid.NewGuid(), amount = 20, date = day, reference = "Concurrent payment" });
        using var response = await client.SendAsync(pay);
        return (int)response.StatusCode;
    }
    var paymentStatuses = await Task.WhenAll(ConcurrentPayment(admin), ConcurrentPayment(secondAdmin));
    Check(paymentStatuses.Order().SequenceEqual(new[] { 200, 409 }), "Concurrent payments cannot overpay an invoice");
    await Request(admin, "PUT", $"/api/notifications/reorder-level/{m}", new { quantity = 20 });
    var notifications = await Request(engineer, "GET", "/api/notifications");
    Check(notifications.AsArray().All(n => !n!["message"]!.GetValue<string>().Contains($"project {p2} ")), "Notifications respect project visibility");
    var readKey = notifications[0]!["key"]!.GetValue<string>();
    await Request(engineer, "POST", "/api/notifications/read", new { key = readKey });
    Check((await Request(engineer, "GET", "/api/notifications"))[0]!["isRead"]!.GetValue<bool>(), "Notification read state persists per user");

    using (var form = new MultipartFormDataContent())
    {
        form.Add(new StringContent("Project"), "resourceType"); form.Add(new StringContent(p.ToString()), "resourceId");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Integration attachment")), "file", "note.txt");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
        var csrf = await engineer.GetFromJsonAsync<JsonNode>("/api/auth/csrf"); req.Headers.Add("X-CSRF-TOKEN", csrf!["token"]!.GetValue<string>());
        using var response = await engineer.SendAsync(req);
        Check(response.IsSuccessStatusCode, "Assigned staff can attach documents");
    }
    await Request(engineer, "GET", $"/api/documents?resourceType=Project&resourceId={p2}", expected: 404);
    using (var file = await engineer.GetAsync("/api/reports/costs/xlsx"))
    {
        Check(file.IsSuccessStatusCode, "Assigned project reports export successfully");
        using var archive = new ZipArchive(new MemoryStream(await file.Content.ReadAsByteArrayAsync()));
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        Check(!reader.ReadToEnd().Contains("Other Project"), "Exports exclude unassigned projects");
    }
    var audit = await Request(admin, "GET", "/api/administration/audit");
    Check(audit["totalCount"]!.GetValue<int>() > 10, "Successful operations produce audit records");
    Check(!audit.ToJsonString().Contains(newPassword) && !audit.ToJsonString().Contains(password), "Audit trail excludes passwords");
    await Request(admin, "PUT", $"/api/users/{userId}", new { fullName = "Engineer", role = "SiteEngineer", isActive = false, reason = "Disable staff" });
    await Request(engineer, "GET", "/api/projects", expected: 401);
    await Request(engineer, "POST", "/api/auth/login", new { email = "engineer@siteflow-tests.invalid", password = newPassword }, 401);
    await Request(admin, "PUT", $"/api/users/{userId}", new { fullName = "Updated staff", role = "Storekeeper", isActive = true, reason = "Role correction" });
    var resetPassword = "Reset!9aA" + Guid.NewGuid().ToString("N");
    await Request(admin, "POST", $"/api/users/{userId}/reset-password", new { password = resetPassword, reason = "Password recovery" });
    await Login(engineer, "engineer@siteflow-tests.invalid", resetPassword);
    await Request(engineer, "GET", "/api/projects", expected: 403);
    await Request(engineer, "POST", "/api/profile/change-password", new { currentPassword = resetPassword, newPassword, confirmPassword = newPassword });
    await Login(engineer, "engineer@siteflow-tests.invalid", newPassword);
    await Request(engineer, "GET", "/api/projects");
    await Request(engineer, "POST", "/api/material-requests", new { projectId = p, requiredDate = day, reason = "Wrong role", lines = new[] { new { materialId = m, quantity = 1, estimatedUnitPrice = 10 } } }, 403);
    Check(true, "Staff reset forces password change and updated roles take effect");
    Console.WriteLine($"ALL {checks} CHECKS PASSED.");
}
finally
{
    if (server is { HasExited: false }) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    server?.Dispose();
    if (!database.StartsWith("siteflow_test_", StringComparison.Ordinal) || database.Length != 46) throw new Exception("Refusing unexpected test database cleanup.");
    var cleanupConnection = new MySqlConnectionStringBuilder(connection.ConnectionString) { Database = "" };
    try
    {
        await using var cleanup = new MySqlConnection(cleanupConnection.ConnectionString);
        await cleanup.OpenAsync();
        await using var drop = new MySqlCommand($"DROP DATABASE IF EXISTS `{database}`", cleanup);
        await drop.ExecuteNonQueryAsync();
        Console.WriteLine("Disposable test database removed.");
    }
    catch (MySqlException) { Console.WriteLine("Test database cleanup was unavailable; verify CREATE/DROP permission for the test account."); }
}
