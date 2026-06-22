import { buildUrl, getHeaders, handleResponse } from '../utils/apiClient';

export const reviewService = {
  // Lấy reviews của sản phẩm (public)
  getByProduct: async (productId) => {
    const res = await fetch(buildUrl(`/api/reviews/product/${productId}`), {
      headers: getHeaders(),
    });
    return handleResponse(res, 'Failed to fetch reviews');
  },

  // Lấy danh sách {orderId, productId} mà user đã đánh giá
  getMyReviewedProducts: async () => {
    const res = await fetch(buildUrl('/api/reviews/my-reviewed-products'), {
      headers: getHeaders(true),
    });
    return handleResponse(res, 'Failed to fetch reviewed products');
  },

  // Tạo đánh giá cho 1 sản phẩm trong đơn hàng
  create: async ({ orderId, productId, rating, comment, reviewImageUrl }) => {
    const res = await fetch(buildUrl('/api/reviews'), {
      method: 'POST',
      headers: getHeaders(true),
      body: JSON.stringify({ orderId, productId, rating, comment, reviewImageUrl }),
    });
    return handleResponse(res, 'Failed to submit review');
  },
};
