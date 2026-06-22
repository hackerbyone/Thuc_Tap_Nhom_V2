using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Repository;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StatisticsController : ControllerBase
    {
        private readonly MySqlDbContext _context;

        public StatisticsController(MySqlDbContext context)
        {
            _context = context;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonthStart = thisMonthStart.AddMonths(1);

            var pendingStatuses = new[] { "WaitingDeposit", "DepositPaid", "Processing", "Shipping" };

            var totalRevenue = await _context.Orders
                .Where(o => o.Status == "Completed")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var thisMonthRevenue = await _context.Orders
                .Where(o => o.Status == "Completed"
                         && o.OrderDate >= thisMonthStart
                         && o.OrderDate < nextMonthStart)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var completedOrders = await _context.Orders.CountAsync(o => o.Status == "Completed");
            var pendingOrders   = await _context.Orders.CountAsync(o => pendingStatuses.Contains(o.Status));
            var cancelledOrders = await _context.Orders.CountAsync(o => o.Status == "Cancelled");
            var todayOrders     = await _context.Orders.CountAsync(o => o.OrderDate >= today && o.OrderDate < today.AddDays(1));

            return Ok(new
            {
                totalRevenue,
                thisMonthRevenue,
                completedOrders,
                pendingOrders,
                cancelledOrders,
                todayOrders
            });
        }

        [HttpGet("daily-revenue")]
        public async Task<IActionResult> GetDailyRevenue([FromQuery] int year, [FromQuery] int month)
        {
            if (year < 2000 || year > 2100 || month < 1 || month > 12)
                return BadRequest(new { message = "Năm/tháng không hợp lệ" });

            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end   = start.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var orders = await _context.Orders
                .Where(o => o.Status == "Completed" && o.OrderDate >= start && o.OrderDate < end)
                .Select(o => new { o.OrderDate.Day, o.TotalAmount })
                .ToListAsync();

            var grouped = orders
                .GroupBy(o => o.Day)
                .ToDictionary(
                    g => g.Key,
                    g => (Revenue: g.Sum(x => x.TotalAmount), Count: g.Count())
                );

            var result = Enumerable.Range(1, daysInMonth).Select(day =>
            {
                var found = grouped.TryGetValue(day, out var d);
                return new { day, revenue = found ? d.Revenue : 0m, orderCount = found ? d.Count : 0 };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReport([FromQuery] string? from, [FromQuery] string? to)
        {
            var now = DateTime.UtcNow;
            var defaultStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);

            var startDate = ParseDateOrDefault(from, defaultStart).Date;
            var endDate = ParseDateOrDefault(to, defaultEnd).Date;

            if (endDate < startDate)
                return BadRequest(new { message = "Khoảng thời gian không hợp lệ" });

            var endExclusive = endDate.AddDays(1);
            var pendingStatuses = new[] { "WaitingDeposit", "DepositPaid", "Processing", "Shipping" };

            var ordersInRange = await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate < endExclusive)
                .Select(o => new { o.Id, o.OrderDate, o.TotalAmount, o.Status })
                .ToListAsync();

            var completedOrders = ordersInRange
                .Where(o => o.Status == "Completed")
                .ToList();

            var completedOrderIds = completedOrders.Select(o => o.Id).ToList();

            var details = await _context.OrderDetails
                .Where(d => completedOrderIds.Contains(d.OrderId))
                .Include(d => d.Product)
                .ThenInclude(p => p!.Category)
                .Select(d => new
                {
                    d.OrderId,
                    d.ProductId,
                    ProductName = d.Product != null ? d.Product.Name : "Sản phẩm",
                    CategoryId = d.Product != null ? d.Product.CategoryId : 0,
                    CategoryName = d.Product != null && d.Product.Category != null ? d.Product.Category.Name : "Chưa phân loại",
                    d.Quantity,
                    d.UnitPrice
                })
                .ToListAsync();

            var completedByDay = completedOrders
                .GroupBy(o => o.OrderDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        revenue = g.Sum(x => x.TotalAmount),
                        orderCount = g.Count()
                    });

            var soldQuantityByOrder = details
                .GroupBy(d => d.OrderId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var dayCount = (endDate - startDate).Days + 1;
            var dailyRevenue = Enumerable.Range(0, dayCount).Select(offset =>
            {
                var date = startDate.AddDays(offset);
                var stats = completedByDay.TryGetValue(date, out var found)
                    ? found
                    : new { revenue = 0m, orderCount = 0 };

                var quantity = completedOrders
                    .Where(o => o.OrderDate.Date == date)
                    .Sum(o => soldQuantityByOrder.TryGetValue(o.Id, out var q) ? q : 0);

                return new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    label = date.ToString("dd/MM"),
                    revenue = stats.revenue,
                    orderCount = stats.orderCount,
                    quantitySold = quantity
                };
            }).ToList();

            var categoryRevenue = details
                .GroupBy(d => new { d.CategoryId, d.CategoryName })
                .Select(g => new
                {
                    categoryId = g.Key.CategoryId,
                    categoryName = g.Key.CategoryName ?? "Chưa phân loại",
                    revenue = g.Sum(x => x.UnitPrice * x.Quantity),
                    quantitySold = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.revenue)
                .ToList();

            var topProducts = details
                .GroupBy(d => new { d.ProductId, d.ProductName, d.CategoryName })
                .Select(g => new
                {
                    productId = g.Key.ProductId,
                    productName = g.Key.ProductName ?? "Sản phẩm",
                    categoryName = g.Key.CategoryName ?? "Chưa phân loại",
                    quantitySold = g.Sum(x => x.Quantity),
                    revenue = g.Sum(x => x.UnitPrice * x.Quantity)
                })
                .OrderByDescending(x => x.quantitySold)
                .ThenByDescending(x => x.revenue)
                .Take(10)
                .ToList();

            var statusCounts = ordersInRange
                .GroupBy(o => o.Status)
                .Select(g => new
                {
                    status = g.Key,
                    label = GetStatusLabel(g.Key),
                    count = g.Count(),
                    revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderBy(x => GetStatusOrder(x.status))
                .ToList();

            return Ok(new
            {
                range = new
                {
                    from = startDate.ToString("yyyy-MM-dd"),
                    to = endDate.ToString("yyyy-MM-dd")
                },
                summary = new
                {
                    revenue = completedOrders.Sum(o => o.TotalAmount),
                    completedOrders = completedOrders.Count,
                    pendingOrders = ordersInRange.Count(o => pendingStatuses.Contains(o.Status)),
                    cancelledOrders = ordersInRange.Count(o => o.Status == "Cancelled"),
                    totalOrders = ordersInRange.Count,
                    quantitySold = details.Sum(d => d.Quantity)
                },
                dailyRevenue,
                categoryRevenue,
                topProducts,
                statusCounts
            });
        }

        private static DateTime ParseDateOrDefault(string? value, DateTime fallback)
        {
            return DateTime.TryParse(value, out var parsed)
                ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
                : fallback;
        }

        private static string GetStatusLabel(string status) => status switch
        {
            "WaitingDeposit" => "Chờ đặt cọc",
            "DepositPaid" => "Đã đặt cọc",
            "Processing" => "Đang xử lý",
            "Shipping" => "Đang giao",
            "Completed" => "Hoàn thành",
            "Cancelled" => "Đã hủy",
            _ => status
        };

        private static int GetStatusOrder(string status) => status switch
        {
            "WaitingDeposit" => 1,
            "DepositPaid" => 2,
            "Processing" => 3,
            "Shipping" => 4,
            "Completed" => 5,
            "Cancelled" => 6,
            _ => 99
        };
    }
}
