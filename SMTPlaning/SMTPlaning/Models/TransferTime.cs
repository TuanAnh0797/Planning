using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Thời gian di chuyển giữa 2 Line
    /// </summary>
    public class LineTransfer
    {
        /// <summary>
        /// Line nguồn (xuất phát)
        /// </summary>
        public string FromLineId { get; set; }

        /// <summary>
        /// Line đích (đến)
        /// </summary>
        public string ToLineId { get; set; }

        /// <summary>
        /// Thời gian di chuyển (phút)
        /// Bao gồm: lấy sản phẩm, di chuyển, đặt vào line mới, kiểm tra
        /// </summary>
        public int TransferTimeMinutes { get; set; }

        /// <summary>
        /// Có cần thiết bị hỗ trợ không (xe đẩy, băng tải...)
        /// </summary>
        public bool RequiresEquipment { get; set; } = false;

        /// <summary>
        /// Ghi chú
        /// </summary>
        public string Notes { get; set; }

        public LineTransfer() { }

        public LineTransfer(string fromLineId, string toLineId, int transferTimeMinutes)
        {
            FromLineId = fromLineId;
            ToLineId = toLineId;
            TransferTimeMinutes = transferTimeMinutes;
        }

        public override string ToString()
        {
            return $"{FromLineId} → {ToLineId}: {TransferTimeMinutes} phút";
        }
    }

    /// <summary>
    /// Ma trận thời gian di chuyển giữa tất cả các Line
    /// </summary>
    public class TransferTimeMatrix
    {
        /// <summary>
        /// Danh sách thời gian di chuyển
        /// Key = (FromLineId, ToLineId)
        /// </summary>
        private readonly Dictionary<(string, string), LineTransfer> _transfers 
            = new Dictionary<(string, string), LineTransfer>();

        /// <summary>
        /// Thời gian di chuyển mặc định (phút) khi không có cấu hình cụ thể
        /// </summary>
        public int DefaultTransferTimeMinutes { get; set; } = 15;

        /// <summary>
        /// Thời gian chuẩn bị khi ở cùng Line (phút)
        /// Thường = 0 vì không cần di chuyển
        /// </summary>
        public int SameLineTransferTimeMinutes { get; set; } = 0;

        public TransferTimeMatrix() { }

        public TransferTimeMatrix(int defaultTransferTimeMinutes)
        {
            DefaultTransferTimeMinutes = defaultTransferTimeMinutes;
        }

        /// <summary>
        /// Thêm thời gian di chuyển giữa 2 Line
        /// </summary>
        public void AddTransfer(string fromLineId, string toLineId, int transferTimeMinutes, 
                               bool requiresEquipment = false, string notes = null)
        {
            var transfer = new LineTransfer
            {
                FromLineId = fromLineId,
                ToLineId = toLineId,
                TransferTimeMinutes = transferTimeMinutes,
                RequiresEquipment = requiresEquipment,
                Notes = notes
            };

            _transfers[(fromLineId, toLineId)] = transfer;
        }

        /// <summary>
        /// Thêm thời gian di chuyển 2 chiều (A→B và B→A giống nhau)
        /// </summary>
        public void AddBidirectionalTransfer(string lineId1, string lineId2, int transferTimeMinutes,
                                            bool requiresEquipment = false, string notes = null)
        {
            AddTransfer(lineId1, lineId2, transferTimeMinutes, requiresEquipment, notes);
            AddTransfer(lineId2, lineId1, transferTimeMinutes, requiresEquipment, notes);
        }

        /// <summary>
        /// Lấy thời gian di chuyển giữa 2 Line
        /// </summary>
        public int GetTransferTime(string fromLineId, string toLineId)
        {
            // Nếu cùng Line, không cần thời gian di chuyển
            if (fromLineId == toLineId)
                return SameLineTransferTimeMinutes;

            // Tìm trong danh sách đã cấu hình
            if (_transfers.TryGetValue((fromLineId, toLineId), out var transfer))
                return transfer.TransferTimeMinutes;

            // Trả về giá trị mặc định
            return DefaultTransferTimeMinutes;
        }

        /// <summary>
        /// Lấy thông tin chi tiết transfer
        /// </summary>
        public LineTransfer GetTransferInfo(string fromLineId, string toLineId)
        {
            if (fromLineId == toLineId)
            {
                return new LineTransfer(fromLineId, toLineId, SameLineTransferTimeMinutes)
                {
                    Notes = "Cùng Line - không cần di chuyển"
                };
            }

            if (_transfers.TryGetValue((fromLineId, toLineId), out var transfer))
                return transfer;

            return new LineTransfer(fromLineId, toLineId, DefaultTransferTimeMinutes)
            {
                Notes = "Sử dụng thời gian mặc định"
            };
        }

        /// <summary>
        /// Kiểm tra có cần thiết bị hỗ trợ không
        /// </summary>
        public bool RequiresEquipment(string fromLineId, string toLineId)
        {
            if (fromLineId == toLineId)
                return false;

            if (_transfers.TryGetValue((fromLineId, toLineId), out var transfer))
                return transfer.RequiresEquipment;

            return false;
        }

        /// <summary>
        /// Lấy tất cả transfers đã cấu hình
        /// </summary>
        public List<LineTransfer> GetAllTransfers()
        {
            return _transfers.Values.ToList();
        }

        /// <summary>
        /// Tạo ma trận từ danh sách Lines với thời gian mặc định
        /// </summary>
        public static TransferTimeMatrix CreateDefault(List<Line> lines, int defaultMinutes = 15)
        {
            var matrix = new TransferTimeMatrix(defaultMinutes);
            return matrix;
        }

        /// <summary>
        /// Tạo ma trận dựa trên khoảng cách vật lý giữa các Line
        /// </summary>
        /// <param name="linePositions">Dictionary: LineId → Position (mét từ điểm gốc)</param>
        /// <param name="speedMetersPerMinute">Tốc độ di chuyển (mét/phút), mặc định 30m/phút</param>
        /// <param name="fixedSetupMinutes">Thời gian cố định để lấy/đặt sản phẩm</param>
        public static TransferTimeMatrix CreateFromPositions(
            Dictionary<string, double> linePositions,
            double speedMetersPerMinute = 30,
            int fixedSetupMinutes = 5)
        {
            var matrix = new TransferTimeMatrix();

            var lineIds = linePositions.Keys.ToList();
            for (int i = 0; i < lineIds.Count; i++)
            {
                for (int j = 0; j < lineIds.Count; j++)
                {
                    if (i == j) continue;

                    var fromLine = lineIds[i];
                    var toLine = lineIds[j];
                    var distance = Math.Abs(linePositions[fromLine] - linePositions[toLine]);
                    var travelTime = distance / speedMetersPerMinute;
                    var totalTime = (int)Math.Ceiling(travelTime + fixedSetupMinutes);

                    matrix.AddTransfer(fromLine, toLine, totalTime);
                }
            }

            return matrix;
        }

        /// <summary>
        /// In ma trận ra console
        /// </summary>
        public string PrintMatrix(List<string> lineIds)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ma trận thời gian di chuyển (phút):");
            sb.AppendLine();

            // Header
            sb.Append("        ");
            foreach (var toLine in lineIds)
            {
                sb.Append($"{toLine,8}");
            }
            sb.AppendLine();

            // Rows
            foreach (var fromLine in lineIds)
            {
                sb.Append($"{fromLine,8}");
                foreach (var toLine in lineIds)
                {
                    var time = GetTransferTime(fromLine, toLine);
                    sb.Append($"{time,8}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Thời gian transfer giữa 2 công đoạn
    /// </summary>
    public class StageTransfer
    {
        /// <summary>
        /// Công đoạn nguồn (xuất phát)
        /// </summary>
        public int FromStageId { get; set; }

        /// <summary>
        /// Công đoạn đích (đến)
        /// </summary>
        public int ToStageId { get; set; }

        /// <summary>
        /// Thời gian transfer (phút)
        /// Bao gồm: chờ nguội, kiểm tra, di chuyển sang công đoạn tiếp
        /// </summary>
        public int TransferTimeMinutes { get; set; }

        /// <summary>
        /// Ghi chú (VD: "Chờ nguội sau Reflow")
        /// </summary>
        public string Notes { get; set; }

        public StageTransfer() { }

        public StageTransfer(int fromStageId, int toStageId, int transferTimeMinutes)
        {
            FromStageId = fromStageId;
            ToStageId = toStageId;
            TransferTimeMinutes = transferTimeMinutes;
        }

        public override string ToString()
        {
            return $"CĐ{FromStageId} → CĐ{ToStageId}: {TransferTimeMinutes} phút";
        }
    }

    /// <summary>
    /// Ma trận thời gian transfer giữa các công đoạn
    /// </summary>
    public class StageTransferTimeMatrix
    {
        /// <summary>
        /// Danh sách thời gian transfer
        /// Key = (FromStageId, ToStageId)
        /// </summary>
        private readonly Dictionary<(int, int), StageTransfer> _transfers
            = new Dictionary<(int, int), StageTransfer>();

        /// <summary>
        /// Thời gian transfer mặc định giữa 2 công đoạn liên tiếp (phút)
        /// </summary>
        public int DefaultTransferTimeMinutes { get; set; } = 5;

        public StageTransferTimeMatrix() { }

        public StageTransferTimeMatrix(int defaultTransferTimeMinutes)
        {
            DefaultTransferTimeMinutes = defaultTransferTimeMinutes;
        }

        /// <summary>
        /// Thêm thời gian transfer giữa 2 công đoạn
        /// </summary>
        public void AddTransfer(int fromStageId, int toStageId, int transferTimeMinutes, string notes = null)
        {
            var transfer = new StageTransfer
            {
                FromStageId = fromStageId,
                ToStageId = toStageId,
                TransferTimeMinutes = transferTimeMinutes,
                Notes = notes
            };

            _transfers[(fromStageId, toStageId)] = transfer;
        }

        /// <summary>
        /// Đặt thời gian transfer cho tất cả công đoạn liên tiếp
        /// </summary>
        public void SetSequentialTransfers(List<Stage> stages, int transferTimeMinutes)
        {
            var orderedStages = stages.OrderBy(s => s.Order).ToList();
            for (int i = 0; i < orderedStages.Count - 1; i++)
            {
                AddTransfer(orderedStages[i].Id, orderedStages[i + 1].Id, transferTimeMinutes);
            }
        }

        /// <summary>
        /// Lấy thời gian transfer giữa 2 công đoạn
        /// </summary>
        public int GetTransferTime(int fromStageId, int toStageId)
        {
            // Nếu cùng công đoạn, không cần transfer
            if (fromStageId == toStageId)
                return 0;

            // Tìm trong danh sách đã cấu hình
            if (_transfers.TryGetValue((fromStageId, toStageId), out var transfer))
                return transfer.TransferTimeMinutes;

            // Trả về giá trị mặc định
            return DefaultTransferTimeMinutes;
        }

        /// <summary>
        /// Lấy thông tin chi tiết transfer
        /// </summary>
        public StageTransfer GetTransferInfo(int fromStageId, int toStageId)
        {
            if (fromStageId == toStageId)
            {
                return new StageTransfer(fromStageId, toStageId, 0)
                {
                    Notes = "Cùng công đoạn"
                };
            }

            if (_transfers.TryGetValue((fromStageId, toStageId), out var transfer))
                return transfer;

            return new StageTransfer(fromStageId, toStageId, DefaultTransferTimeMinutes)
            {
                Notes = "Sử dụng thời gian mặc định"
            };
        }

        /// <summary>
        /// Lấy tất cả transfers đã cấu hình
        /// </summary>
        public List<StageTransfer> GetAllTransfers()
        {
            return _transfers.Values.ToList();
        }

        /// <summary>
        /// Tạo ma trận mặc định từ danh sách Stages
        /// </summary>
        public static StageTransferTimeMatrix CreateDefault(List<Stage> stages, int defaultMinutes = 5)
        {
            var matrix = new StageTransferTimeMatrix(defaultMinutes);
            matrix.SetSequentialTransfers(stages, defaultMinutes);
            return matrix;
        }

        /// <summary>
        /// In ma trận ra string
        /// </summary>
        public string PrintMatrix(List<Stage> stages)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ma trận thời gian transfer giữa công đoạn (phút):");
            sb.AppendLine();

            var orderedStages = stages.OrderBy(s => s.Order).ToList();

            // Header
            sb.Append("          ");
            foreach (var toStage in orderedStages)
            {
                sb.Append($"CĐ{toStage.Id,6}");
            }
            sb.AppendLine();

            // Rows
            foreach (var fromStage in orderedStages)
            {
                sb.Append($"CĐ{fromStage.Id,6}   ");
                foreach (var toStage in orderedStages)
                {
                    var time = GetTransferTime(fromStage.Id, toStage.Id);
                    sb.Append($"{time,8}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
