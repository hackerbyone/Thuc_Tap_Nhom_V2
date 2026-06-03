using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using BaseCore.Repository;
using System.Text.Json;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WarehouseController : ControllerBase
    {
        private readonly MySqlDbContext _context;

        public WarehouseController(MySqlDbContext context)
        {
            _context = context;
        }

        // ────────────────────────────────────────────────────────────
        //  BỂ CÁ (Tank Fish Tracking)
        // ────────────────────────────────────────────────────────────

        [HttpGet("tanks")]
        public async Task<IActionResult> GetTanks([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.TankFishTrackings
                .Include(t => t.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(t => t.TankName.Contains(keyword) ||
                                         (t.Product != null && t.Product.Name.Contains(keyword)));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.LastUpdated)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id, t.TankName, t.ProductId,
                    ProductName = t.Product != null ? t.Product.Name : "",
                    ProductImage = t.Product != null ? t.Product.ImageUrl : "",
                    t.MaleCount, t.FemaleCount,
                    TotalCount = t.MaleCount + t.FemaleCount,
                    t.Notes, t.LastUpdated, t.LastUpdatedBy, t.LastUpdatedByName
                })
                .ToListAsync();

            return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
        }

        [HttpGet("tanks/{id}")]
        public async Task<IActionResult> GetTank(int id)
        {
            var tank = await _context.TankFishTrackings
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tank == null) return NotFound(new { message = "Không tìm thấy bể cá" });

            return Ok(new
            {
                tank.Id, tank.TankName, tank.ProductId,
                ProductName = tank.Product?.Name ?? "",
                ProductImage = tank.Product?.ImageUrl ?? "",
                tank.MaleCount, tank.FemaleCount,
                TotalCount = tank.MaleCount + tank.FemaleCount,
                tank.Notes, tank.LastUpdated, tank.LastUpdatedBy, tank.LastUpdatedByName
            });
        }

        [HttpPost("tanks")]
        public async Task<IActionResult> CreateTank([FromBody] TankRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TankName))
                return BadRequest(new { message = "Tên bể không được để trống" });

            var product = await _context.Products.FindAsync(req.ProductId);
            if (product == null)
                return BadRequest(new { message = "Sản phẩm không tồn tại" });

            var tank = new TankFishTracking
            {
                TankName         = req.TankName,
                ProductId        = req.ProductId,
                MaleCount        = req.MaleCount,
                FemaleCount      = req.FemaleCount,
                Notes            = req.Notes,
                LastUpdated      = DateTime.Now,
                LastUpdatedBy    = req.StaffId,
                LastUpdatedByName = req.StaffName
            };
            _context.TankFishTrackings.Add(tank);
            await _context.SaveChangesAsync();

            // Ghi commit
            await AddCommit(req.StaffId, req.StaffName,
                req.CommitMessage ?? $"Tạo bể mới: {req.TankName}",
                "Fish", tank.Id, req.TankName,
                null,
                JsonSerializer.Serialize(new { req.MaleCount, req.FemaleCount }));

            return Ok(new { tank.Id, message = "Tạo bể thành công" });
        }

        [HttpPut("tanks/{id}")]
        public async Task<IActionResult> UpdateTank(int id, [FromBody] TankRequest req)
        {
            var tank = await _context.TankFishTrackings.FindAsync(id);
            if (tank == null) return NotFound(new { message = "Không tìm thấy bể cá" });

            var oldValue = JsonSerializer.Serialize(new { tank.MaleCount, tank.FemaleCount, tank.Notes });

            tank.TankName         = req.TankName ?? tank.TankName;
            tank.ProductId        = req.ProductId > 0 ? req.ProductId : tank.ProductId;
            tank.MaleCount        = req.MaleCount;
            tank.FemaleCount      = req.FemaleCount;
            tank.Notes            = req.Notes;
            tank.LastUpdated      = DateTime.Now;
            tank.LastUpdatedBy    = req.StaffId;
            tank.LastUpdatedByName = req.StaffName;

            await _context.SaveChangesAsync();

            await AddCommit(req.StaffId, req.StaffName,
                req.CommitMessage ?? $"Cập nhật bể: {tank.TankName}",
                "Fish", tank.Id, tank.TankName,
                oldValue,
                JsonSerializer.Serialize(new { req.MaleCount, req.FemaleCount, req.Notes }));

            return Ok(new { message = "Cập nhật thành công" });
        }

        [HttpDelete("tanks/{id}")]
        public async Task<IActionResult> DeleteTank(int id, [FromQuery] string staffId = "", [FromQuery] string staffName = "")
        {
            var tank = await _context.TankFishTrackings.FindAsync(id);
            if (tank == null) return NotFound(new { message = "Không tìm thấy bể cá" });

            await AddCommit(staffId, staffName,
                $"Xoá bể: {tank.TankName}",
                "Fish", tank.Id, tank.TankName,
                JsonSerializer.Serialize(new { tank.MaleCount, tank.FemaleCount }),
                null);

            _context.TankFishTrackings.Remove(tank);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xoá bể thành công" });
        }

        // ────────────────────────────────────────────────────────────
        //  PHỤ KIỆN & THIẾT BỊ (Accessories)
        // ────────────────────────────────────────────────────────────

        [HttpGet("accessories")]
        public async Task<IActionResult> GetAccessories(
            [FromQuery] string? keyword,
            [FromQuery] string? type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.Accessories.Where(a => a.IsActive).AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(a => a.Name.Contains(keyword));
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(a => a.Type == type);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.Modified ?? a.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
        }

        [HttpPost("accessories")]
        public async Task<IActionResult> CreateAccessory([FromBody] AccessoryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Tên phụ kiện không được để trống" });

            var accessory = new Accessory
            {
                Name         = req.Name,
                Type         = req.Type ?? "Accessory",
                Quantity     = req.Quantity,
                Unit         = req.Unit,
                Status       = req.Status ?? "Good",
                Description  = req.Description,
                IsActive     = true,
                Created      = DateTime.Now,
                CreatedBy    = req.StaffId,
                CreatedByName = req.StaffName
            };
            _context.Accessories.Add(accessory);
            await _context.SaveChangesAsync();

            await AddCommit(req.StaffId, req.StaffName,
                req.CommitMessage ?? $"Thêm {(req.Type == "Equipment" ? "thiết bị" : "phụ kiện")}: {req.Name}",
                "Accessory", accessory.Id, req.Name,
                null,
                JsonSerializer.Serialize(new { req.Quantity, req.Status }));

            return Ok(new { accessory.Id, message = "Tạo thành công" });
        }

        [HttpPut("accessories/{id}")]
        public async Task<IActionResult> UpdateAccessory(int id, [FromBody] AccessoryRequest req)
        {
            var accessory = await _context.Accessories.FindAsync(id);
            if (accessory == null) return NotFound(new { message = "Không tìm thấy phụ kiện" });

            var oldValue = JsonSerializer.Serialize(new { accessory.Quantity, accessory.Status });

            accessory.Name        = req.Name ?? accessory.Name;
            accessory.Type        = req.Type ?? accessory.Type;
            accessory.Quantity    = req.Quantity;
            accessory.Unit        = req.Unit ?? accessory.Unit;
            accessory.Status      = req.Status ?? accessory.Status;
            accessory.Description = req.Description ?? accessory.Description;
            accessory.Modified    = DateTime.Now;

            await _context.SaveChangesAsync();

            await AddCommit(req.StaffId, req.StaffName,
                req.CommitMessage ?? $"Cập nhật {(accessory.Type == "Equipment" ? "thiết bị" : "phụ kiện")}: {accessory.Name}",
                "Accessory", accessory.Id, accessory.Name,
                oldValue,
                JsonSerializer.Serialize(new { req.Quantity, req.Status }));

            return Ok(new { message = "Cập nhật thành công" });
        }

        [HttpDelete("accessories/{id}")]
        public async Task<IActionResult> DeleteAccessory(int id, [FromQuery] string staffId = "", [FromQuery] string staffName = "")
        {
            var accessory = await _context.Accessories.FindAsync(id);
            if (accessory == null) return NotFound(new { message = "Không tìm thấy phụ kiện" });

            await AddCommit(staffId, staffName,
                $"Xoá {(accessory.Type == "Equipment" ? "thiết bị" : "phụ kiện")}: {accessory.Name}",
                "Accessory", accessory.Id, accessory.Name,
                JsonSerializer.Serialize(new { accessory.Quantity, accessory.Status }),
                null);

            accessory.IsActive = false;
            accessory.Modified = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xoá thành công" });
        }

        // ────────────────────────────────────────────────────────────
        //  COMMIT LOG (Thông báo nội bộ)
        // ────────────────────────────────────────────────────────────

        [HttpGet("commits")]
        public async Task<IActionResult> GetCommits(
            [FromQuery] string? staffId,
            [FromQuery] string? targetType,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var query = _context.InventoryCommits.AsQueryable();

            if (!string.IsNullOrWhiteSpace(staffId))
                query = query.Where(c => c.StaffId == staffId);
            if (!string.IsNullOrWhiteSpace(targetType))
                query = query.Where(c => c.TargetType == targetType);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
        }

        // ────────────────────────────────────────────────────────────
        //  HELPER
        // ────────────────────────────────────────────────────────────

        private async Task AddCommit(string staffId, string staffName, string message,
            string targetType, int? targetId, string targetName,
            string? oldValue, string? newValue)
        {
            _context.InventoryCommits.Add(new InventoryCommit
            {
                StaffId       = staffId,
                StaffName     = staffName,
                CommitMessage = message,
                TargetType    = targetType,
                TargetId      = targetId,
                TargetName    = targetName,
                OldValue      = oldValue,
                NewValue      = newValue,
                Created       = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  REQUEST DTOs
    // ────────────────────────────────────────────────────────────

    public class TankRequest
    {
        public string TankName { get; set; } = "";
        public int ProductId { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public string? Notes { get; set; }
        public string StaffId { get; set; } = "";
        public string StaffName { get; set; } = "";
        public string? CommitMessage { get; set; }
    }

    public class AccessoryRequest
    {
        public string Name { get; set; } = "";
        public string? Type { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string StaffId { get; set; } = "";
        public string StaffName { get; set; } = "";
        public string? CommitMessage { get; set; }
    }
}
