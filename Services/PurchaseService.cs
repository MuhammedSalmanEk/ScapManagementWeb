using Microsoft.EntityFrameworkCore;
using ScrapManagement.Web.Data;
using ScrapManagement.Web.Infrastructure;
using ScrapManagement.Web.Models;
using ScrapManagement.Web.Models.ViewModels;

namespace ScrapManagement.Web.Services;

public interface IPurchaseService
{
    Task<PurchaseBill?> GetAsync(int id, CancellationToken ct = default);
    Task<List<PurchaseBill>> ListAsync(PurchaseListViewModel filter, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveDraftAsync(PurchaseBillFormModel model, CurrentUser user, bool canAddProducts, CancellationToken ct = default);
    Task<ServiceResult<PurchaseBill>> FinalizeAsync(int id, CurrentUser user, CancellationToken ct = default);
    Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
}

public class PurchaseService : IPurchaseService
{
    private const decimal MaxQty = 1_000_000m;
    private const decimal MaxRate = 1_000_000m;
    public const int NewProductMarker = 0;

    private readonly ApplicationDbContext _db;
    private readonly IClock _clock;

    public PurchaseService(ApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<PurchaseBill?> GetAsync(int id, CancellationToken ct = default) =>
        _db.PurchaseBills.AsNoTracking()
            .Include(b => b.Items.OrderBy(i => i.Id))
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<List<PurchaseBill>> ListAsync(PurchaseListViewModel f, CancellationToken ct = default)
    {
        var q = _db.PurchaseBills.AsNoTracking().AsQueryable();
        // set by the controller for workers who may not see everyone's bills
        if (!string.IsNullOrEmpty(f.OnlyCreatedById)) q = q.Where(b => b.CreatedById == f.OnlyCreatedById);
        if (f.Status.HasValue) q = q.Where(b => b.Status == f.Status.Value);
        if (f.From.HasValue) q = q.Where(b => b.BillDate >= f.From.Value);
        if (f.To.HasValue) q = q.Where(b => b.BillDate <= f.To.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            q = q.Where(b => b.SellerName.ToLower().Contains(term)
                             || (b.InvoiceNumber != null && b.InvoiceNumber.ToLower().Contains(term)));
        }
        return await q.OrderByDescending(b => b.BillDate).ThenByDescending(b => b.Id).Take(200).ToListAsync(ct);
    }

    // ------------------------------------------------------------------ save draft

    public async Task<ServiceResult<int>> SaveDraftAsync(PurchaseBillFormModel model, CurrentUser user, bool canAddProducts, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var today = _clock.Today;
        var billDate = model.BillDate ?? today;
        if (billDate > today) errors.Add("Bill date cannot be in the future.");

        var rows = model.Items
            .Where(i => i.ProductId.HasValue || i.QuantityKg.HasValue || i.RatePerKg.HasValue || !string.IsNullOrWhiteSpace(i.NewProductName))
            .ToList();
        if (rows.Count == 0) errors.Add("Add at least one item.");

        for (var idx = 0; idx < rows.Count; idx++)
        {
            var r = rows[idx];
            var n = idx + 1;
            if (r.ProductId is null or < 0) errors.Add($"Item {n}: select a product.");
            else if (r.ProductId == NewProductMarker)
            {
                if (!canAddProducts) errors.Add($"Item {n}: you do not have permission to add new products.");
                else if (string.IsNullOrWhiteSpace(r.NewProductName)) errors.Add($"Item {n}: enter the new product name.");
            }
            if (r.QuantityKg is not > 0 || r.QuantityKg > MaxQty) errors.Add($"Item {n}: quantity (kg) must be greater than 0.");
            if (r.RatePerKg is not > 0 || r.RatePerKg > MaxRate) errors.Add($"Item {n}: rate per kg must be greater than 0.");
            if (r.NewOtherRatePerKg is < 0 || r.NewOtherRatePerKg > MaxRate) errors.Add($"Item {n}: the optional second rate is not valid.");
        }
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors);

        var existingIds = rows.Where(r => r.ProductId > 0).Select(r => r.ProductId!.Value).Distinct().ToList();
        var products = await _db.Products.Where(p => existingIds.Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != existingIds.Count)
            return ServiceResult<int>.Fail("One of the selected products no longer exists or is inactive.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 1. Create any new products requested inline (reuse a product with the same name).
        var rowProduct = new Dictionary<PurchaseItemFormModel, Product>();
        var newByName = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (r.ProductId > 0) { rowProduct[r] = products[r.ProductId!.Value]; continue; }

            var name = r.NewProductName!.Trim();
            if (newByName.TryGetValue(name, out var already)) { rowProduct[r] = already; continue; }

            var lower = name.ToLower();
            var found = await _db.Products.FirstOrDefaultAsync(p => p.Name.ToLower() == lower, ct);
            if (found is not null)
            {
                if (!found.IsActive) return ServiceResult<int>.Fail($"Product \"{found.Name}\" exists but is inactive. Re-activate it first.");
                newByName[name] = found;
                rowProduct[r] = found;
                continue;
            }

            int? categoryId = null;
            if (r.NewCategoryId.HasValue && await _db.Categories.AnyAsync(c => c.Id == r.NewCategoryId.Value && c.IsActive, ct))
                categoryId = r.NewCategoryId;

            // The rate typed on the line goes to the selected price list (retail or wholesale);
            // the other price list uses the optional second rate.
            var lineRate = PurchaseCalculator.RoundRate(r.RatePerKg!.Value);
            decimal? otherRate = r.NewOtherRatePerKg is > 0 ? PurchaseCalculator.RoundRate(r.NewOtherRatePerKg.Value) : null;
            var isWholesale = model.RateType == RateType.Wholesale;

            var created = new Product
            {
                Name = name,
                CategoryId = categoryId,
                DefaultRatePerKg   = isWholesale ? (otherRate ?? lineRate) : lineRate,   // retail
                WholesaleRatePerKg = isWholesale ? lineRate : (otherRate ?? 0),          // wholesale (0 = use retail)
                IsActive = true
            };
            _db.Products.Add(created);
            newByName[name] = created;
            rowProduct[r] = created;
        }
        await _db.SaveChangesAsync(ct); // gives new products their ids

        // 2. Create or load the draft.
        PurchaseBill bill;
        if (model.Id is null)
        {
            bill = new PurchaseBill { CreatedById = user.Id, CreatedByName = user.Name, CreatedAt = _clock.UtcNow };
            _db.PurchaseBills.Add(bill);
        }
        else
        {
            var loaded = await _db.PurchaseBills.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == model.Id, ct);
            if (loaded is null) return ServiceResult<int>.Fail("Bill not found.");
            if (loaded.Status != BillStatus.Draft) return ServiceResult<int>.Fail("A finalized bill cannot be changed.");
            bill = loaded;
            _db.PurchaseBillItems.RemoveRange(bill.Items);
            bill.Items.Clear();
        }

        bill.BillDate = billDate;
        bill.RateType = model.RateType;
        bill.SellerName = model.SellerName.Trim();
        bill.SellerPhone = model.SellerPhone?.Trim();
        bill.SellerAddress = model.SellerAddress?.Trim();
        bill.SellerIdNumber = model.SellerIdNumber?.Trim();
        bill.Notes = model.Notes?.Trim();

        foreach (var r in rows)
        {
            var p = rowProduct[r];
            var qty = PurchaseCalculator.RoundQty(r.QuantityKg!.Value);
            var rate = PurchaseCalculator.RoundRate(r.RatePerKg!.Value);
            bill.Items.Add(new PurchaseBillItem
            {
                ProductId = p.Id,
                ProductName = p.Name,
                QuantityKg = qty,
                RatePerKg = rate,
                LineTotal = PurchaseCalculator.LineTotal(qty, rate)
            });
        }
        bill.TotalKg = bill.Items.Sum(i => i.QuantityKg);
        bill.TotalAmount = bill.Items.Sum(i => i.LineTotal);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(bill.Id);
    }

    // ------------------------------------------------------------------ finalize

    /// <summary>
    /// Finalizes a draft. Everything happens in one transaction and the Draft→Finalized flip is a conditional
    /// UPDATE, so even if two requests arrive together only one can win and stock is added exactly once.
    /// A unique index on StockMovement.PurchaseBillItemId is a second safety net.
    /// </summary>
    public async Task<ServiceResult<PurchaseBill>> FinalizeAsync(int id, CurrentUser user, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var bill = await _db.PurchaseBills.AsNoTracking().Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bill is null) return ServiceResult<PurchaseBill>.Fail("Bill not found.");
        if (bill.Status == BillStatus.Finalized) return ServiceResult<PurchaseBill>.Fail("This bill is already finalized.");
        if (bill.Items.Count == 0) return ServiceResult<PurchaseBill>.Fail("The bill has no items.");

        // Next invoice number for this shop and year (atomic upsert; the row lock serialises finalizations per shop).
        var year = _clock.Today.Year;
        var shopId = bill.ShopId;
        var numbers = await _db.Database.SqlQuery<int>($"""
            INSERT INTO "InvoiceSequences" ("ShopId", "Year", "LastNumber")
            VALUES ({shopId}, {year}, 1)
            ON CONFLICT ("ShopId", "Year") DO UPDATE SET "LastNumber" = "InvoiceSequences"."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(ct);
        var invoiceNumber = InvoiceNumbers.Format(year, numbers.Single());

        var now = _clock.UtcNow;
        var flipped = await _db.PurchaseBills
            .Where(b => b.Id == id && b.Status == BillStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BillStatus.Finalized)
                .SetProperty(b => b.InvoiceNumber, invoiceNumber)
                .SetProperty(b => b.FinalizedAt, (DateTime?)now)
                .SetProperty(b => b.FinalizedById, user.Id)
                .SetProperty(b => b.FinalizedByName, user.Name), ct);

        if (flipped != 1)
        {
            await tx.RollbackAsync(ct); // also rolls back the invoice number
            return ServiceResult<PurchaseBill>.Fail("This bill was already finalized.");
        }

        foreach (var item in bill.Items)
        {
            var qty = item.QuantityKg;
            var updated = await _db.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockKg, p => p.StockKg + qty), ct);
            if (updated != 1) throw new InvalidOperationException($"Product {item.ProductId} not found while updating stock.");

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                Type = StockMovementType.Purchase,
                QuantityKg = qty,
                PurchaseBillId = bill.Id,
                PurchaseBillItemId = item.Id,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var result = await GetAsync(id, ct);
        return ServiceResult<PurchaseBill>.Ok(result!);
    }

    public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
    {
        var deleted = await _db.PurchaseBills
            .Where(b => b.Id == id && b.Status == BillStatus.Draft)
            .ExecuteDeleteAsync(ct); // items are removed by the database cascade
        return deleted == 1 ? ServiceResult.Ok() : ServiceResult.Fail("Only draft bills can be deleted.");
    }
}