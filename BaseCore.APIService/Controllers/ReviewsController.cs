using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using BaseCore.Repository;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _context;

        public ReviewsController(MySqlDbContext context)
        {
            _context = context;
        }

        private string? GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("id")?.Value;

        // ── GET /api/reviews/product/{productId} — Lấy reviews của sản phẩm (public) ──
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetByProduct(int productId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.ProductId,
                    r.Rating,
                    r.Comment,
                    r.CustomerName,
                    r.CreatedAt,
                    r.ReviewImageUrl
                })
                .ToListAsync();

            var avgRating = reviews.Count > 0
                ? Math.Round(reviews.Average(r => (double)r.Rating), 1)
                : 0.0;

            return Ok(new
            {
                reviews,
                totalCount = reviews.Count,
                averageRating = avgRating,
                ratingBreakdown = Enumerable.Range(1, 5).Select(star => new
                {
                    star,
                    count = reviews.Count(r => r.Rating == star)
                }).Reverse()
            });
        }

        // ── GET /api/reviews/my-reviewed-products — List {orderId, productId} đã review ──
        [HttpGet("my-reviewed-products")]
        [Authorize]
        public async Task<IActionResult> MyReviewedProducts()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var pairs = await _context.Reviews
                .Where(r => r.UserId == userId && r.OrderId != null)
                .Select(r => new { orderId = r.OrderId!.Value, productId = r.ProductId })
                .ToListAsync();

            return Ok(pairs);
        }

        // ── POST /api/reviews — Tạo đánh giá cho 1 sản phẩm trong đơn hàng ──
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Số sao phải từ 1 đến 5" });

            if (dto.OrderId == null)
                return BadRequest(new { message = "Thiếu mã đơn hàng" });

            if (!dto.ProductId.HasValue)
                return BadRequest(new { message = "Thiếu mã sản phẩm" });

            // Verify đơn hàng thuộc về user và chứa đúng sản phẩm này
            var orderDetail = await _context.Set<OrderDetail>()
                .Where(d => d.Order != null
                         && d.Order.Id == dto.OrderId.Value
                         && d.Order.UserId == userId
                         && d.ProductId == dto.ProductId.Value)
                .Select(d => new { Status = d.Order!.Status })
                .FirstOrDefaultAsync();

            if (orderDetail == null)
                return BadRequest(new { message = "Không tìm thấy sản phẩm trong đơn hàng hoặc đơn hàng không thuộc về bạn" });

            if (orderDetail.Status != "Completed")
                return BadRequest(new { message = "Chỉ có thể đánh giá khi đơn hàng đã hoàn thành" });

            // Kiểm tra (orderId, productId) đã được đánh giá chưa
            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.OrderId == dto.OrderId.Value && r.ProductId == dto.ProductId.Value);

            if (alreadyReviewed)
                return BadRequest(new { message = "Sản phẩm này trong đơn hàng đã được đánh giá rồi" });

            var user = await _context.Users.FindAsync(userId);

            var review = new Review
            {
                ProductId      = dto.ProductId.Value,
                UserId         = userId,
                OrderId        = dto.OrderId,
                Rating         = dto.Rating,
                Comment        = dto.Comment?.Trim() ?? "",
                CustomerName   = user?.Name ?? user?.UserName ?? "Khách hàng",
                CreatedAt      = DateTime.UtcNow,
                ReviewImageUrl = dto.ReviewImageUrl?.Trim()
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Đánh giá của bạn đã được gửi thành công!",
                review = new
                {
                    review.Id,
                    review.ProductId,
                    review.Rating,
                    review.Comment,
                    review.CustomerName,
                    review.CreatedAt,
                    review.ReviewImageUrl
                }
            });
        }

        // ── DELETE /api/reviews/{id} — Admin xóa review ──
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa đánh giá" });
        }
    }

    public class CreateReviewDto
    {
        public int? ProductId { get; set; }
        public int? OrderId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string? ReviewImageUrl { get; set; }
    }
}
