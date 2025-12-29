using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Một task trong lịch sản xuất
    /// </summary>
    public class ScheduleTask
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }

        /// <summary>
        /// Tên model tại công đoạn này (có thể khác ProductName nếu model đổi tên qua công đoạn)
        /// </summary>
        public string ModelNameAtStage { get; set; }

        public int StageId { get; set; }
        public int StageOrder { get; set; }
        public string StageName { get; set; }
        public string LineId { get; set; }
        public string LineName { get; set; }
        public int Quantity { get; set; }

        /// <summary>
        /// ID nhóm sản phẩm (nếu có)
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// Tên nhóm sản phẩm
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Thời điểm bắt đầu (phút từ mốc 0)
        /// </summary>
        public long StartTimeMinutes { get; set; }

        /// <summary>
        /// Thời điểm kết thúc (phút từ mốc 0)
        /// </summary>
        public long EndTimeMinutes { get; set; }

        /// <summary>
        /// Thời gian xử lý (phút) - không bao gồm setup time
        /// </summary>
        public long ProcessingTimeMinutes { get; set; }

        /// <summary>
        /// Thời gian setup/changeover (phút)
        /// </summary>
        public long SetupTimeMinutes { get; set; }

        /// <summary>
        /// Thời gian di chuyển từ Line trước (phút)
        /// </summary>
        public int TransferTimeMinutes { get; set; }

        /// <summary>
        /// Thời gian transfer giữa công đoạn (phút)
        /// </summary>
        public int StageTransferTimeMinutes { get; set; }

        /// <summary>
        /// Line của công đoạn trước (để tính transfer time)
        /// </summary>
        public string PreviousLineId { get; set; }

        /// <summary>
        /// Công đoạn trước (để tính stage transfer time)
        /// </summary>
        public int PreviousStageId { get; set; }

        /// <summary>
        /// ID Batch (nếu có lot splitting)
        /// </summary>
        public string BatchId { get; set; }

        /// <summary>
        /// Số thứ tự batch (1, 2, 3...)
        /// </summary>
        public int BatchNumber { get; set; } = 1;

        /// <summary>
        /// Tổng số batch của sản phẩm này
        /// </summary>
        public int TotalBatches { get; set; } = 1;

        /// <summary>
        /// Có phải task của batch không (lot được chia)
        /// </summary>
        public bool IsBatchTask => TotalBatches > 1;

        /// <summary>
        /// Tổng thời gian = Processing + Setup + Transfer
        /// </summary>
        public long DurationMinutes => EndTimeMinutes - StartTimeMinutes;

        /// <summary>
        /// Tên hiển thị - ưu tiên ModelNameAtStage nếu có
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(ModelNameAtStage) ? ModelNameAtStage : ProductName;

        /// <summary>
        /// Ngày giờ bắt đầu thực tế
        /// </summary>
        public DateTime ActualStartDate { get; set; }

        /// <summary>
        /// Ngày giờ kết thúc thực tế
        /// </summary>
        public DateTime ActualEndDate { get; set; }

        /// <summary>
        /// Danh sách linh kiện cần thay đổi (nếu có changeover)
        /// </summary>
        public List<string> ChangeoverComponents { get; set; } = new List<string>();

        /// <summary>
        /// Sản phẩm trước đó trên cùng Line (để tính changeover)
        /// </summary>
        public string PreviousProductId { get; set; }

        /// <summary>
        /// Ghi chú tổng hợp (batch info, transfer info, etc.)
        /// </summary>
        public string Notes
        {
            get
            {
                var notes = new List<string>();
                
                // Batch info
                if (IsBatchTask)
                    notes.Add($"Batch {BatchNumber}/{TotalBatches}");
                
                // Stage Transfer
                if (StageTransferTimeMinutes > 0)
                    notes.Add($"CĐ Transfer: {StageTransferTimeMinutes}ph");
                
                // Line Transfer
                if (TransferTimeMinutes > 0)
                    notes.Add($"Line Transfer: {TransferTimeMinutes}ph (từ {PreviousLineId})");
                
                // Setup/Changeover
                if (SetupTimeMinutes > 0)
                    notes.Add($"Setup: {SetupTimeMinutes}ph");
                
                // Changeover components
                if (ChangeoverComponents != null && ChangeoverComponents.Any())
                    notes.Add($"Đổi {ChangeoverComponents.Count} LK");
                
                return notes.Any() ? string.Join(" | ", notes) : "";
            }
        }

        public override string ToString()
        {
            string setup = SetupTimeMinutes > 0 ? $" [Setup: {SetupTimeMinutes}ph]" : "";
            return $"{ProductName} | CĐ{StageOrder}:{StageName} | {LineName} | " +
                   $"{ActualStartDate:dd/MM HH:mm} → {ActualEndDate:dd/MM HH:mm}{setup}";
        }
    }

    /// <summary>
    /// Nhóm sản phẩm có linh kiện tương tự
    /// </summary>
    public class ProductGroup
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }

        /// <summary>
        /// Danh sách sản phẩm trong nhóm (đã sắp xếp tối ưu)
        /// </summary>
        public List<Product> Products { get; set; } = new List<Product>();

        /// <summary>
        /// Danh sách tất cả linh kiện cần cho nhóm này
        /// </summary>
        public List<string> AllComponentIds { get; set; } = new List<string>();

        /// <summary>
        /// Tổng số khe Feeder cần
        /// </summary>
        public int TotalFeederSlots { get; set; }

        /// <summary>
        /// Tổng số lần changeover trong nhóm
        /// </summary>
        public int TotalChangeoverCount { get; set; }

        /// <summary>
        /// Tổng thời gian changeover (phút)
        /// </summary>
        public double TotalChangeoverTimeMinutes { get; set; }

        /// <summary>
        /// Độ tương đồng trung bình trong nhóm (%)
        /// </summary>
        public double AverageSimilarityPercent { get; set; }
    }

    /// <summary>
    /// Kết quả lập lịch
    /// </summary>
    public class ScheduleResult
    {
        public bool IsSuccess { get; set; }
        public string Status { get; set; }

        /// <summary>
        /// Thời gian hoàn thành tổng (phút)
        /// </summary>
        public long MakespanMinutes { get; set; }

        /// <summary>
        /// Ngày bắt đầu kế hoạch
        /// </summary>
        public DateTime PlanStartDate { get; set; }

        /// <summary>
        /// Ngày hoàn thành dự kiến
        /// </summary>
        public DateTime ExpectedCompletionDate { get; set; }

        /// <summary>
        /// Thời gian solver chạy (ms)
        /// </summary>
        public long SolveTimeMs { get; set; }

        /// <summary>
        /// Danh sách task đã lập lịch
        /// </summary>
        public List<ScheduleTask> Tasks { get; set; } = new List<ScheduleTask>();

        /// <summary>
        /// Nhóm sản phẩm (sau khi đã group theo linh kiện)
        /// </summary>
        public List<ProductGroup> ProductGroups { get; set; } = new List<ProductGroup>();

        /// <summary>
        /// Danh sách sản phẩm trễ deadline
        /// </summary>
        public List<DeadlineMiss> MissedDeadlines { get; set; } = new List<DeadlineMiss>();

        /// <summary>
        /// Nguyên nhân thất bại (nếu có)
        /// </summary>
        public List<string> FailureReasons { get; set; } = new List<string>();

        /// <summary>
        /// Cảnh báo (không gây fail nhưng cần lưu ý)
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Phân tích năng lực sản xuất theo công đoạn
        /// </summary>
        public List<CapacityAnalysis> CapacityAnalyses { get; set; } = new List<CapacityAnalysis>();

        /// <summary>
        /// Thống kê sử dụng Line
        /// </summary>
        public List<LineUtilization> LineUtilizations { get; set; } = new List<LineUtilization>();

        /// <summary>
        /// Thống kê changeover
        /// </summary>
        public ChangeoverStatistics ChangeoverStats { get; set; } = new ChangeoverStatistics();

        /// <summary>
        /// Báo cáo gợi ý (OT, tuyển dụng, training)
        /// </summary>
        public SuggestionReport SuggestionReport { get; set; }

        /// <summary>
        /// In báo cáo tóm tắt
        /// </summary>
        public string GetSummaryReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("                    KẾT QUẢ LẬP LỊCH SMT                   ");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine($"Trạng thái: {(IsSuccess ? "✓ THÀNH CÔNG" : "✗ THẤT BẠI")} ({Status})");
            sb.AppendLine($"Thời gian tính toán: {SolveTimeMs} ms");
            sb.AppendLine();

            if (IsSuccess)
            {
                sb.AppendLine($"Ngày bắt đầu: {PlanStartDate:dd/MM/yyyy}");
                sb.AppendLine($"Ngày hoàn thành dự kiến: {ExpectedCompletionDate:dd/MM/yyyy HH:mm}");
                sb.AppendLine($"Tổng thời gian: {MakespanMinutes} phút ({MakespanMinutes / 60.0:F1} giờ)");
                sb.AppendLine();

                // Thống kê changeover
                if (ChangeoverStats != null)
                {
                    sb.AppendLine("--- THỐNG KÊ CHANGEOVER ---");
                    sb.AppendLine($"Tổng số lần changeover: {ChangeoverStats.TotalChangeoverCount}");
                    sb.AppendLine($"Tổng thời gian changeover: {ChangeoverStats.TotalChangeoverTimeMinutes:F0} phút");
                    sb.AppendLine($"Thời gian changeover trung bình: {ChangeoverStats.AverageChangeoverTimeMinutes:F1} phút");
                    sb.AppendLine();
                }

                // Cảnh báo deadline
                if (MissedDeadlines.Any())
                {
                    sb.AppendLine("⚠️  CẢNH BÁO: CÓ SẢN PHẨM TRỄ DEADLINE!");
                    foreach (var miss in MissedDeadlines)
                    {
                        sb.AppendLine($"   - {miss.ProductName}: Trễ {miss.DelayDays} ngày ({miss.Reason})");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("--- NGUYÊN NHÂN THẤT BẠI ---");
                foreach (var reason in FailureReasons)
                {
                    sb.AppendLine($"   ✗ {reason}");
                }
                sb.AppendLine();
            }

            // Cảnh báo
            if (Warnings.Any())
            {
                sb.AppendLine("--- CẢNH BÁO ---");
                foreach (var warning in Warnings)
                {
                    sb.AppendLine($"   ⚠ {warning}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// In lịch trình chi tiết
        /// </summary>
        public string GetDetailedSchedule()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("                                   LỊCH TRÌNH CHI TIẾT                                 ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════════════");

            // Group theo Line
            var tasksByLine = Tasks.GroupBy(t => t.LineId).OrderBy(g => g.Key);

            foreach (var lineGroup in tasksByLine)
            {
                var lineTasks = lineGroup.OrderBy(t => t.StartTimeMinutes).ToList();
                var lineName = lineTasks.First().LineName;

                sb.AppendLine();
                sb.AppendLine($"┌─ {lineName} ─────────────────────────────────────────────────────────────────────────");
                sb.AppendLine("│  Thời gian             │ Model (tại CĐ)           │ Công đoạn        │ SL    │ Ghi chú");
                sb.AppendLine("├────────────────────────┼──────────────────────────┼──────────────────┼───────┼─────────────");

                foreach (var task in lineTasks)
                {
                    // Tên model tại công đoạn (ưu tiên ModelNameAtStage, nếu không có thì dùng ProductName)
                    string displayName = !string.IsNullOrEmpty(task.ModelNameAtStage) 
                        ? task.ModelNameAtStage 
                        : task.ProductName;
                    
                    // Truncate nếu quá dài
                    if (displayName.Length > 24)
                        displayName = displayName.Substring(0, 21) + "...";

                    sb.AppendLine($"│  {task.ActualStartDate:dd/MM HH:mm} → {task.ActualEndDate:HH:mm} │ {displayName,-24} │ CĐ{task.StageOrder} {task.StageName,-11} │ {task.Quantity,5} │ {task.Notes}");
                }

                sb.AppendLine("└─────────────────────────────────────────────────────────────────────────────────────────");
            }

            return sb.ToString();
        }

        /// <summary>
        /// In lịch trình đơn giản
        /// </summary>
        public string GetSimpleSchedule()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                    LỊCH TRÌNH ĐƠN GIẢN                        ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            var sortedTasks = Tasks.OrderBy(t => t.StartTimeMinutes).ThenBy(t => t.LineId);

            foreach (var task in sortedTasks)
            {
                string modelDisplay = !string.IsNullOrEmpty(task.ModelNameAtStage) 
                    ? task.ModelNameAtStage 
                    : task.ProductName;

                // Hiển thị notes nếu có
                string notesDisplay = !string.IsNullOrEmpty(task.Notes) ? $" [{task.Notes}]" : "";

                sb.AppendLine($"  {task.ActualStartDate:dd/MM HH:mm} | {modelDisplay,-20} | {task.StageName,-12} | {task.LineName} | {task.Quantity} sp{notesDisplay}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Thông tin sản phẩm trễ deadline
    /// </summary>
    public class DeadlineMiss
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime ExpectedCompletion { get; set; }
        public int DelayDays { get; set; }
        public double DelayHours { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Phân tích năng lực từng công đoạn
    /// </summary>
    public class CapacityAnalysis
    {
        public int StageId { get; set; }
        public string StageName { get; set; }
        public double RequiredTimeMinutes { get; set; }
        public double AvailableTimeMinutes { get; set; }
        public double UtilizationPercent => AvailableTimeMinutes > 0
            ? (RequiredTimeMinutes / AvailableTimeMinutes) * 100
            : 0;
        public bool IsBottleneck => RequiredTimeMinutes > AvailableTimeMinutes;
        public int AvailableLineCount { get; set; }
    }

    /// <summary>
    /// Thống kê sử dụng Line
    /// </summary>
    public class LineUtilization
    {
        public string LineId { get; set; }
        public string LineName { get; set; }
        public long TotalWorkMinutes { get; set; }
        public long TotalSetupMinutes { get; set; }
        public long TotalTransferMinutes { get; set; }
        public long AvailableMinutes { get; set; }
        public double UtilizationPercent => AvailableMinutes > 0
            ? ((TotalWorkMinutes + TotalSetupMinutes + TotalTransferMinutes) * 100.0 / AvailableMinutes)
            : 0;
        public int TaskCount { get; set; }
        public int ChangeoverCount { get; set; }
        public int TransferCount { get; set; }
    }

    /// <summary>
    /// Thống kê changeover
    /// </summary>
    public class ChangeoverStatistics
    {
        public int TotalChangeoverCount { get; set; }
        public double TotalChangeoverTimeMinutes { get; set; }
        public double AverageChangeoverTimeMinutes => TotalChangeoverCount > 0
            ? TotalChangeoverTimeMinutes / TotalChangeoverCount
            : 0;
        public int TotalComponentChanges { get; set; }
        public double TimesSavedByGroupingMinutes { get; set; }
        
        /// <summary>
        /// Số lần chuyển Line giữa các công đoạn
        /// </summary>
        public int TotalTransferCount { get; set; }
        
        /// <summary>
        /// Tổng thời gian chuyển Line (phút)
        /// </summary>
        public double TotalTransferTimeMinutes { get; set; }
    }
}
