import { useState, useEffect, useCallback } from 'react';
import { warehouseService } from '../../services/warehouse/warehouseService';
import { productService } from '../../services/product/productService';

const QUARANTINE_LABELS = {
  Pending:  { label: 'Chờ kiểm dịch', color: '#f59e0b', bg: '#fff8e1' },
  InProgress: { label: 'Đang kiểm dịch', color: '#1976d2', bg: '#e3f2fd' },
  Passed:   { label: 'Đã qua kiểm dịch', color: '#388e3c', bg: '#e8f5e9' },
  Failed:   { label: 'Không đạt', color: '#e53935', bg: '#ffebee' },
};

function QuarantineBadge({ status }) {
  const q = QUARANTINE_LABELS[status] || { label: status, color: '#888', bg: '#f5f5f5' };
  return (
    <span style={{ padding: '2px 10px', borderRadius: 12, fontSize: '0.78rem', fontWeight: 600, color: q.color, background: q.bg, border: `1px solid ${q.color}33` }}>
      {q.label}
    </span>
  );
}

const emptyBatch = { productId: '', originFarm: '', importDate: '', initialQuantity: '', notes: '' };

export default function FishBatches() {
  const [batches, setBatches] = useState([]);
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState('');
  const [filterProduct, setFilterProduct] = useState('');

  const [showAddModal, setShowAddModal] = useState(false);
  const [addForm, setAddForm] = useState(emptyBatch);
  const [addError, setAddError] = useState('');
  const [addLoading, setAddLoading] = useState(false);

  const [quarantineModal, setQuarantineModal] = useState(null);
  const [quarantineStatus, setQuarantineStatus] = useState('');
  const [quarantineNote, setQuarantineNote] = useState('');
  const [quarantineLoading, setQuarantineLoading] = useState(false);

  const [lossModal, setLossModal] = useState(null);
  const [lossQty, setLossQty] = useState('');
  const [lossReason, setLossReason] = useState('');
  const [lossLoading, setLossLoading] = useState(false);
  const [lossError, setLossError] = useState('');

  const loadBatches = useCallback(async () => {
    setLoading(true);
    try {
      const params = {};
      if (filterStatus) params.quarantineStatus = filterStatus;
      if (filterProduct) params.productId = filterProduct;
      const data = await warehouseService.getBatches(params);
      setBatches(Array.isArray(data) ? data : data.items || []);
    } catch { setBatches([]); } finally { setLoading(false); }
  }, [filterStatus, filterProduct]);

  useEffect(() => { loadBatches(); }, [loadBatches]);

  useEffect(() => {
    productService.getAll('', null, 1, 200)
      .then(r => setProducts(r.items || []))
      .catch(() => {});
  }, []);

  const handleAddSubmit = async (e) => {
    e.preventDefault();
    setAddError('');
    if (!addForm.productId || !addForm.originFarm.trim() || !addForm.importDate || !addForm.initialQuantity) {
      setAddError('Vui lòng điền đầy đủ thông tin bắt buộc'); return;
    }
    setAddLoading(true);
    try {
      await warehouseService.createBatch({
        productId: parseInt(addForm.productId),
        originFarm: addForm.originFarm.trim(),
        importDate: addForm.importDate,
        initialQuantity: parseInt(addForm.initialQuantity),
        notes: addForm.notes.trim() || null,
      });
      setShowAddModal(false);
      setAddForm(emptyBatch);
      loadBatches();
    } catch (err) { setAddError(err.message || 'Tạo lô thất bại'); }
    finally { setAddLoading(false); }
  };

  const handleQuarantineSubmit = async () => {
    if (!quarantineStatus) return;
    setQuarantineLoading(true);
    try {
      await warehouseService.updateQuarantine(quarantineModal.id, { status: quarantineStatus, notes: quarantineNote.trim() || null });
      setQuarantineModal(null);
      loadBatches();
    } catch { } finally { setQuarantineLoading(false); }
  };

  const handleLossSubmit = async () => {
    setLossError('');
    const qty = parseInt(lossQty);
    if (!qty || qty <= 0) { setLossError('Số lượng phải lớn hơn 0'); return; }
    if (qty > lossModal.currentQuantity) { setLossError(`Không thể ghi nhận hơn ${lossModal.currentQuantity} con`); return; }
    setLossLoading(true);
    try {
      await warehouseService.recordBatchLoss(lossModal.id, { quantity: qty, reason: lossReason.trim() || null });
      setLossModal(null);
      setLossQty('');
      setLossReason('');
      loadBatches();
    } catch (err) { setLossError(err.message || 'Ghi nhận thất bại'); }
    finally { setLossLoading(false); }
  };

  return (
    <div className="container-fluid mt-4">
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h4 className="mb-0"><i className="fas fa-fish mr-2 text-info"></i>Quản lý lô nhập cá</h4>
        <button className="btn btn-primary btn-sm" onClick={() => { setAddForm(emptyBatch); setAddError(''); setShowAddModal(true); }}>
          <i className="fas fa-plus mr-1"></i> Thêm lô nhập
        </button>
      </div>

      {/* Filters */}
      <div className="card card-body bg-light mb-3 p-2">
        <div className="row g-2 align-items-end">
          <div className="col-auto">
            <label className="small mb-1">Trạng thái kiểm dịch</label>
            <select className="form-control form-control-sm" value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
              <option value="">Tất cả</option>
              {Object.entries(QUARANTINE_LABELS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
          </div>
          <div className="col-auto">
            <label className="small mb-1">Sản phẩm</label>
            <select className="form-control form-control-sm" value={filterProduct} onChange={e => setFilterProduct(e.target.value)} style={{ minWidth: 160 }}>
              <option value="">Tất cả</option>
              {products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>
          <div className="col-auto">
            <button className="btn btn-secondary btn-sm" onClick={() => { setFilterStatus(''); setFilterProduct(''); }}>Đặt lại</button>
          </div>
        </div>
      </div>

      {loading ? (
        <p>Đang tải...</p>
      ) : batches.length === 0 ? (
        <div className="alert alert-info">Không có lô nhập nào.</div>
      ) : (
        <div className="table-responsive">
          <table className="table table-sm table-hover table-bordered">
            <thead className="thead-light">
              <tr>
                <th>#</th>
                <th>Sản phẩm</th>
                <th>Trại giống</th>
                <th>Ngày nhập</th>
                <th>Kiểm dịch</th>
                <th style={{ textAlign: 'center' }}>Nhập ban đầu</th>
                <th style={{ textAlign: 'center' }}>Hiện tại</th>
                <th>Ghi chú</th>
                <th style={{ textAlign: 'center' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {batches.map(b => (
                <tr key={b.id}>
                  <td>{b.id}</td>
                  <td>{b.productName || b.product?.name || `#${b.productId}`}</td>
                  <td>{b.originFarm}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{new Date(b.importDate).toLocaleDateString('vi-VN')}</td>
                  <td><QuarantineBadge status={b.quarantineStatus} /></td>
                  <td style={{ textAlign: 'center' }}>{b.initialQuantity}</td>
                  <td style={{ textAlign: 'center' }}>
                    <strong style={{ color: b.currentQuantity < b.initialQuantity * 0.8 ? '#e53935' : '#222' }}>{b.currentQuantity}</strong>
                  </td>
                  <td style={{ fontSize: '0.82rem', color: '#666', maxWidth: 150 }}>{b.notes || '—'}</td>
                  <td style={{ textAlign: 'center', whiteSpace: 'nowrap' }}>
                    <button className="btn btn-xs btn-outline-info mr-1" style={{ padding: '2px 8px', fontSize: '0.78rem' }}
                      onClick={() => { setQuarantineModal(b); setQuarantineStatus(b.quarantineStatus); setQuarantineNote(''); }}>
                      🔬 Kiểm dịch
                    </button>
                    <button className="btn btn-xs btn-outline-warning" style={{ padding: '2px 8px', fontSize: '0.78rem' }}
                      onClick={() => { setLossModal(b); setLossQty(''); setLossReason(''); setLossError(''); }}>
                      📉 Hao hụt
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Add Batch Modal */}
      {showAddModal && (
        <div className="modal show d-block" style={{ background: 'rgba(0,0,0,0.45)' }} onClick={() => setShowAddModal(false)}>
          <div className="modal-dialog" onClick={e => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">Thêm lô nhập cá mới</h5>
                <button className="close" onClick={() => setShowAddModal(false)}><span>&times;</span></button>
              </div>
              <form onSubmit={handleAddSubmit}>
                <div className="modal-body">
                  {addError && <div className="alert alert-danger py-2">{addError}</div>}
                  <div className="form-group">
                    <label>Sản phẩm <span className="text-danger">*</span></label>
                    <select className="form-control" value={addForm.productId} onChange={e => setAddForm(f => ({ ...f, productId: e.target.value }))} required>
                      <option value="">-- Chọn sản phẩm --</option>
                      {products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Trại giống / Nguồn gốc <span className="text-danger">*</span></label>
                    <input className="form-control" value={addForm.originFarm} onChange={e => setAddForm(f => ({ ...f, originFarm: e.target.value }))} placeholder="VD: Trại cá Bình Dương" required />
                  </div>
                  <div className="form-group">
                    <label>Ngày nhập <span className="text-danger">*</span></label>
                    <input type="date" className="form-control" value={addForm.importDate} onChange={e => setAddForm(f => ({ ...f, importDate: e.target.value }))} required />
                  </div>
                  <div className="form-group">
                    <label>Số lượng nhập <span className="text-danger">*</span></label>
                    <input type="number" min="1" className="form-control" value={addForm.initialQuantity} onChange={e => setAddForm(f => ({ ...f, initialQuantity: e.target.value }))} placeholder="VD: 100" required />
                  </div>
                  <div className="form-group mb-0">
                    <label>Ghi chú</label>
                    <textarea className="form-control" rows="2" value={addForm.notes} onChange={e => setAddForm(f => ({ ...f, notes: e.target.value }))} placeholder="Tuỳ chọn..." />
                  </div>
                </div>
                <div className="modal-footer">
                  <button type="button" className="btn btn-secondary" onClick={() => setShowAddModal(false)}>Hủy</button>
                  <button type="submit" className="btn btn-primary" disabled={addLoading}>{addLoading ? 'Đang lưu...' : 'Tạo lô nhập'}</button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Quarantine Modal */}
      {quarantineModal && (
        <div className="modal show d-block" style={{ background: 'rgba(0,0,0,0.45)' }} onClick={() => setQuarantineModal(null)}>
          <div className="modal-dialog" onClick={e => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">🔬 Cập nhật kiểm dịch — Lô #{quarantineModal.id}</h5>
                <button className="close" onClick={() => setQuarantineModal(null)}><span>&times;</span></button>
              </div>
              <div className="modal-body">
                <p className="text-muted mb-3">Sản phẩm: <strong>{quarantineModal.productName || `#${quarantineModal.productId}`}</strong></p>
                <div className="form-group">
                  <label>Trạng thái kiểm dịch</label>
                  <select className="form-control" value={quarantineStatus} onChange={e => setQuarantineStatus(e.target.value)}>
                    {Object.entries(QUARANTINE_LABELS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
                  </select>
                </div>
                <div className="form-group mb-0">
                  <label>Ghi chú</label>
                  <textarea className="form-control" rows="2" value={quarantineNote} onChange={e => setQuarantineNote(e.target.value)} placeholder="Kết quả kiểm dịch, lưu ý..." />
                </div>
              </div>
              <div className="modal-footer">
                <button className="btn btn-secondary" onClick={() => setQuarantineModal(null)}>Hủy</button>
                <button className="btn btn-info" onClick={handleQuarantineSubmit} disabled={quarantineLoading}>{quarantineLoading ? 'Đang lưu...' : 'Cập nhật'}</button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Loss Modal */}
      {lossModal && (
        <div className="modal show d-block" style={{ background: 'rgba(0,0,0,0.45)' }} onClick={() => setLossModal(null)}>
          <div className="modal-dialog" onClick={e => e.stopPropagation()}>
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">📉 Ghi nhận hao hụt — Lô #{lossModal.id}</h5>
                <button className="close" onClick={() => setLossModal(null)}><span>&times;</span></button>
              </div>
              <div className="modal-body">
                <p className="text-muted mb-1">Sản phẩm: <strong>{lossModal.productName || `#${lossModal.productId}`}</strong></p>
                <p className="mb-3">Số lượng hiện tại: <strong>{lossModal.currentQuantity} con</strong></p>
                {lossError && <div className="alert alert-danger py-2">{lossError}</div>}
                <div className="form-group">
                  <label>Số lượng hao hụt <span className="text-danger">*</span></label>
                  <input type="number" min="1" max={lossModal.currentQuantity} className="form-control" value={lossQty} onChange={e => setLossQty(e.target.value)} placeholder="Số con bị chết/thất thoát" />
                </div>
                <div className="form-group mb-0">
                  <label>Lý do</label>
                  <textarea className="form-control" rows="2" value={lossReason} onChange={e => setLossReason(e.target.value)} placeholder="VD: Bệnh, sự cố kỹ thuật, vận chuyển..." />
                </div>
              </div>
              <div className="modal-footer">
                <button className="btn btn-secondary" onClick={() => setLossModal(null)}>Hủy</button>
                <button className="btn btn-warning" onClick={handleLossSubmit} disabled={lossLoading}>{lossLoading ? 'Đang lưu...' : 'Ghi nhận hao hụt'}</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
