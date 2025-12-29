using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Loại gợi ý
    /// </summary>
    public enum SuggestionType
    {
        /// <summary>Tăng ca ngày thường (sau giờ làm)</summary>
        WeekdayOT,
        
        /// <summary>OT ngày thường</summary>
        OvertimeWeekday,
        
        /// <summary>OT cuối tuần</summary>
        OvertimeWeekend,
        
        /// <summary>OT ngày lễ</summary>
        OvertimeHoliday,
        
        /// <summary>Làm thứ 7</summary>
        SaturdayWork,
        
        /// <summary>Làm Chủ nhật</summary>
        SundayWork,
        
        /// <summary>Làm ngày lễ</summary>
        HolidayWork,
        
        /// <summary>Tuyển thêm người</summary>
        Hiring,
        
        /// <summary>Training nâng cao kỹ năng</summary>
        Training,
        
        /// <summary>Điều chuyển người từ bộ phận khác</summary>
        Transfer,
        
        /// <summary>Thuê ngoài (Outsource)</summary>
        Outsource,
        
        /// <summary>Thương lượng lại deadline</summary>
        NegotiateDeadline,
        
        /// <summary>Chia nhỏ đơn hàng</summary>
        SplitOrder,
        
        /// <summary>Thêm máy/Line</summary>
        AddEquipment
    }

    /// <summary>
    /// Mức độ khả thi của gợi ý
    /// </summary>
    public enum FeasibilityLevel
    {
        /// <summary>Rất dễ thực hiện</summary>
        VeryEasy = 5,
        
        /// <summary>Dễ thực hiện</summary>
        Easy = 4,
        
        /// <summary>Trung bình</summary>
        Moderate = 3,
        
        /// <summary>Khó</summary>
        Difficult = 2,
        
        /// <summary>Rất khó</summary>
        VeryDifficult = 1
    }

    /// <summary>
    /// Một gợi ý để giải quyết vấn đề năng lực
    /// </summary>
    public class Suggestion
    {
        /// <summary>
        /// ID gợi ý
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Loại gợi ý
        /// </summary>
        public SuggestionType Type { get; set; }

        /// <summary>
        /// Tiêu đề ngắn gọn
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Mô tả chi tiết
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Mức độ ưu tiên (1 = cao nhất)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Mức độ khả thi
        /// </summary>
        public FeasibilityLevel Feasibility { get; set; }

        /// <summary>
        /// Chi phí ước tính (VND)
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Số giờ công có thể bù đắp
        /// </summary>
        public double HoursGained { get; set; }

        /// <summary>
        /// Số sản phẩm có thể sản xuất thêm
        /// </summary>
        public int AdditionalCapacity { get; set; }

        /// <summary>
        /// Công đoạn liên quan
        /// </summary>
        public int? StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Line liên quan
        /// </summary>
        public string LineId { get; set; }
        public string LineName { get; set; }

        /// <summary>
        /// Sản phẩm hưởng lợi
        /// </summary>
        public List<string> AffectedProductIds { get; set; } = new List<string>();

        /// <summary>
        /// Ngày bắt đầu áp dụng
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Ngày kết thúc
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Điều kiện tiên quyết
        /// </summary>
        public List<string> Prerequisites { get; set; } = new List<string>();

        /// <summary>
        /// Rủi ro
        /// </summary>
        public List<string> Risks { get; set; } = new List<string>();

        /// <summary>
        /// Đã được chấp nhận chưa
        /// </summary>
        public bool IsAccepted { get; set; } = false;

        /// <summary>
        /// Ghi chú thêm
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Thời gian thu được (phút)
        /// </summary>
        public int TimeGainMinutes { get; set; }

        /// <summary>
        /// Line bị ảnh hưởng (dạng string)
        /// </summary>
        public string AffectedLineId { get; set; }

        /// <summary>
        /// Công đoạn bị ảnh hưởng (dạng string)
        /// </summary>
        public string AffectedStageId { get; set; }

        /// <summary>
        /// Operator bị ảnh hưởng
        /// </summary>
        public string AffectedOperatorId { get; set; }

        public override string ToString()
        {
            return $"[{Type}] {Title} - Ưu tiên: {Priority}, Khả thi: {Feasibility}";
        }
    }

    /// <summary>
    /// Gợi ý OT chi tiết
    /// </summary>
    public class OTSuggestionDetail : Suggestion
    {
        /// <summary>
        /// Danh sách ngày cần OT
        /// </summary>
        public List<OTDayDetail> OTDays { get; set; } = new List<OTDayDetail>();

        /// <summary>
        /// Tổng số giờ OT
        /// </summary>
        public double TotalOTHours => OTDays.Sum(d => d.OTHours);

        /// <summary>
        /// Tổng số người-ngày OT
        /// </summary>
        public int TotalManDays => OTDays.Sum(d => d.OperatorCount);

        public OTSuggestionDetail()
        {
            Type = SuggestionType.WeekdayOT;
        }
    }

    /// <summary>
    /// Chi tiết một ngày OT
    /// </summary>
    public class OTDayDetail
    {
        public DateTime Date { get; set; }
        public DayOfWeek DayOfWeek => Date.DayOfWeek;
        public bool IsHoliday { get; set; }
        public string HolidayName { get; set; }
        
        /// <summary>
        /// Số giờ OT cần
        /// </summary>
        public double OTHours { get; set; }

        /// <summary>
        /// Số operator cần
        /// </summary>
        public int OperatorCount { get; set; }

        /// <summary>
        /// Danh sách operator có thể làm
        /// </summary>
        public List<string> AvailableOperatorIds { get; set; } = new List<string>();
        public List<string> AvailableOperatorNames { get; set; } = new List<string>();

        /// <summary>
        /// Công đoạn
        /// </summary>
        public int StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Line
        /// </summary>
        public string LineId { get; set; }
        public string LineName { get; set; }

        /// <summary>
        /// Hệ số lương OT
        /// </summary>
        public decimal OTMultiplier { get; set; } = 1.5m;

        /// <summary>
        /// Chi phí ước tính
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Sản lượng có thể đạt được
        /// </summary>
        public int ExpectedOutput { get; set; }

        public string GetDayTypeDescription()
        {
            if (IsHoliday) return $"Ngày lễ ({HolidayName})";
            switch (DayOfWeek)
            {
                case DayOfWeek.Saturday: return "Thứ Bảy";
                case DayOfWeek.Sunday: return "Chủ Nhật";
                default: return "Ngày thường (sau giờ)";
            }
        }

        public override string ToString()
        {
            return $"{Date:dd/MM/yyyy} ({GetDayTypeDescription()}): " +
                   $"{OTHours:F1}h x {OperatorCount} người = {ExpectedOutput} sp";
        }
    }

    /// <summary>
    /// Gợi ý tuyển dụng chi tiết
    /// </summary>
    public class HiringSuggestionDetail : Suggestion
    {
        /// <summary>
        /// Danh sách vị trí cần tuyển
        /// </summary>
        public List<HiringPosition> Positions { get; set; } = new List<HiringPosition>();

        /// <summary>
        /// Tổng số người cần tuyển
        /// </summary>
        public int TotalHeadcount => Positions.Sum(p => p.Headcount);

        /// <summary>
        /// Chi phí tuyển dụng ước tính
        /// </summary>
        public decimal RecruitmentCost { get; set; }

        /// <summary>
        /// Chi phí training ước tính
        /// </summary>
        public decimal TrainingCost { get; set; }

        /// <summary>
        /// Chi phí lương tháng đầu
        /// </summary>
        public decimal FirstMonthSalaryCost { get; set; }

        public HiringSuggestionDetail()
        {
            Type = SuggestionType.Hiring;
        }
    }

    /// <summary>
    /// Một vị trí cần tuyển
    /// </summary>
    public class HiringPosition
    {
        /// <summary>
        /// Tên vị trí
        /// </summary>
        public string PositionTitle { get; set; }

        /// <summary>
        /// Số lượng cần tuyển
        /// </summary>
        public int Headcount { get; set; }

        /// <summary>
        /// Công đoạn sẽ làm
        /// </summary>
        public int StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Line sẽ làm
        /// </summary>
        public string LineId { get; set; }
        public string LineName { get; set; }

        /// <summary>
        /// Skill level yêu cầu ban đầu
        /// </summary>
        public SkillLevel RequiredInitialSkill { get; set; } = SkillLevel.None;

        /// <summary>
        /// Skill level sau training
        /// </summary>
        public SkillLevel TargetSkillAfterTraining { get; set; } = SkillLevel.Basic;

        /// <summary>
        /// Thời gian training (ngày làm việc)
        /// </summary>
        public int TrainingDays { get; set; }

        /// <summary>
        /// Ngày muộn nhất phải tuyển
        /// </summary>
        public DateTime LatestHireDate { get; set; }

        /// <summary>
        /// Ngày bắt đầu làm việc hiệu quả (sau training)
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Lương đề xuất
        /// </summary>
        public decimal ProposedSalary { get; set; }

        /// <summary>
        /// Yêu cầu kinh nghiệm
        /// </summary>
        public string ExperienceRequirement { get; set; }

        /// <summary>
        /// Người sẽ training (nếu có)
        /// </summary>
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }

        /// <summary>
        /// Lý do cần tuyển
        /// </summary>
        public string Reason { get; set; }

        public override string ToString()
        {
            return $"{PositionTitle} x{Headcount} - {StageName}, " +
                   $"tuyển trước {LatestHireDate:dd/MM/yyyy}, " +
                   $"training {TrainingDays} ngày";
        }
    }

    /// <summary>
    /// Gợi ý training nâng cao
    /// </summary>
    public class TrainingSuggestionDetail : Suggestion
    {
        /// <summary>
        /// Danh sách khóa training
        /// </summary>
        public List<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();

        public TrainingSuggestionDetail()
        {
            Type = SuggestionType.Training;
        }
    }

    /// <summary>
    /// Kế hoạch training
    /// </summary>
    public class TrainingPlan
    {
        /// <summary>
        /// Operator được training
        /// </summary>
        public string OperatorId { get; set; }
        public string OperatorName { get; set; }

        /// <summary>
        /// Công đoạn sẽ học
        /// </summary>
        public int StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Skill hiện tại
        /// </summary>
        public SkillLevel CurrentSkill { get; set; }

        /// <summary>
        /// Skill mục tiêu
        /// </summary>
        public SkillLevel TargetSkill { get; set; }

        /// <summary>
        /// Thời gian training (ngày)
        /// </summary>
        public int TrainingDays { get; set; }

        /// <summary>
        /// Ngày bắt đầu training
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Ngày hoàn thành
        /// </summary>
        public DateTime CompletionDate { get; set; }

        /// <summary>
        /// Người training
        /// </summary>
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }

        /// <summary>
        /// Chi phí training
        /// </summary>
        public decimal Cost { get; set; }
    }

    /// <summary>
    /// Báo cáo tổng hợp các gợi ý
    /// </summary>
    public class SuggestionReport
    {
        /// <summary>
        /// Tình trạng tổng quan
        /// </summary>
        public string OverallStatus { get; set; }

        /// <summary>
        /// Tóm tắt ngắn gọn
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Có khả thi hay không với các gợi ý
        /// </summary>
        public bool IsFeasibleWithSuggestions { get; set; }

        /// <summary>
        /// Tổng thiếu hụt giờ công
        /// </summary>
        public double TotalShortfallHours { get; set; }

        /// <summary>
        /// Tổng thiếu hụt (phút)
        /// </summary>
        public double TotalShortfallMinutes { get; set; }

        /// <summary>
        /// Số sản phẩm có nguy cơ trễ deadline
        /// </summary>
        public int ProductsAtRisk { get; set; }

        /// <summary>
        /// Danh sách tất cả gợi ý (dạng chung)
        /// </summary>
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();

        /// <summary>
        /// Danh sách gợi ý OT
        /// </summary>
        public List<OTSuggestionDetail> OTSuggestions { get; set; } = new List<OTSuggestionDetail>();

        /// <summary>
        /// Danh sách gợi ý tuyển dụng
        /// </summary>
        public List<HiringSuggestionDetail> HiringSuggestions { get; set; } = new List<HiringSuggestionDetail>();

        /// <summary>
        /// Danh sách gợi ý training
        /// </summary>
        public List<TrainingSuggestionDetail> TrainingSuggestions { get; set; } = new List<TrainingSuggestionDetail>();

        /// <summary>
        /// Các gợi ý khác
        /// </summary>
        public List<Suggestion> OtherSuggestions { get; set; } = new List<Suggestion>();

        /// <summary>
        /// Tổng chi phí nếu thực hiện tất cả gợi ý
        /// </summary>
        public decimal TotalEstimatedCost { get; set; }

        /// <summary>
        /// Phân tích năng lực operator theo công đoạn
        /// </summary>
        public List<OperatorCapacityAnalysis> CapacityAnalyses { get; set; } = new List<OperatorCapacityAnalysis>();

        /// <summary>
        /// In báo cáo
        /// </summary>
        public string GenerateReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    BÁO CÁO GỢI Ý GIẢI QUYẾT NĂNG LỰC                        ║");
            sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Tình trạng tổng quan
            sb.AppendLine($"TÌNH TRẠNG: {OverallStatus}");
            sb.AppendLine($"Khả thi với gợi ý: {(IsFeasibleWithSuggestions ? "CÓ ✓" : "KHÔNG ✗")}");
            sb.AppendLine($"Thiếu hụt: {TotalShortfallHours:F1} giờ công");
            sb.AppendLine($"Sản phẩm có nguy cơ trễ: {ProductsAtRisk}");
            sb.AppendLine($"Tổng chi phí ước tính: {TotalEstimatedCost:N0} VND");
            sb.AppendLine();

            // Phân tích năng lực
            if (CapacityAnalyses.Any())
            {
                sb.AppendLine("─── PHÂN TÍCH NĂNG LỰC THEO CÔNG ĐOẠN ───");
                foreach (var cap in CapacityAnalyses)
                {
                    string status = cap.HasShortfall ? "⚠️ THIẾU" : "✓ ĐỦ";
                    sb.AppendLine($"  {cap.StageName}: {cap.TotalOperators} người " +
                                 $"({cap.IndependentOperators} độc lập, {cap.TrainingOperators} đang học) - {status}");
                    if (cap.HasShortfall)
                    {
                        sb.AppendLine($"      Thiếu: {cap.ShortfallHours:F1} giờ, cần thêm ~{cap.EstimatedHiringNeed} người");
                    }
                }
                sb.AppendLine();
            }

            // Gợi ý OT
            if (OTSuggestions.Any())
            {
                sb.AppendLine("─── GỢI Ý TĂNG CA (OT) ───");
                int idx = 1;
                foreach (var ot in OTSuggestions.OrderBy(o => o.Priority))
                {
                    sb.AppendLine($"\n  [{idx}] {ot.Title}");
                    sb.AppendLine($"      Ưu tiên: {ot.Priority}, Khả thi: {ot.Feasibility}");
                    sb.AppendLine($"      Tổng: {ot.TotalOTHours:F1} giờ OT, {ot.TotalManDays} người-ngày");
                    sb.AppendLine($"      Chi phí: {ot.EstimatedCost:N0} VND");
                    sb.AppendLine($"      Năng lực thêm: +{ot.AdditionalCapacity} sản phẩm");
                    
                    if (ot.OTDays.Any())
                    {
                        sb.AppendLine("      Chi tiết:");
                        foreach (var day in ot.OTDays.Take(5))
                        {
                            sb.AppendLine($"        - {day}");
                        }
                        if (ot.OTDays.Count > 5)
                        {
                            sb.AppendLine($"        ... và {ot.OTDays.Count - 5} ngày khác");
                        }
                    }
                    idx++;
                }
                sb.AppendLine();
            }

            // Gợi ý tuyển dụng
            if (HiringSuggestions.Any())
            {
                sb.AppendLine("─── GỢI Ý TUYỂN DỤNG ───");
                int idx = 1;
                foreach (var hire in HiringSuggestions.OrderBy(h => h.Priority))
                {
                    sb.AppendLine($"\n  [{idx}] {hire.Title}");
                    sb.AppendLine($"      Tổng cần tuyển: {hire.TotalHeadcount} người");
                    sb.AppendLine($"      Chi phí tuyển dụng: {hire.RecruitmentCost:N0} VND");
                    sb.AppendLine($"      Chi phí training: {hire.TrainingCost:N0} VND");
                    
                    foreach (var pos in hire.Positions)
                    {
                        sb.AppendLine($"      • {pos}");
                    }
                    idx++;
                }
                sb.AppendLine();
            }

            // Gợi ý training
            if (TrainingSuggestions.Any())
            {
                sb.AppendLine("─── GỢI Ý TRAINING NÂNG CAO ───");
                foreach (var training in TrainingSuggestions)
                {
                    sb.AppendLine($"\n  {training.Title}");
                    foreach (var plan in training.TrainingPlans)
                    {
                        sb.AppendLine($"    • {plan.OperatorName}: {plan.CurrentSkill} → {plan.TargetSkill} " +
                                     $"({plan.TrainingDays} ngày, bắt đầu {plan.StartDate:dd/MM})");
                    }
                }
                sb.AppendLine();
            }

            // Gợi ý khác
            if (OtherSuggestions.Any())
            {
                sb.AppendLine("─── GỢI Ý KHÁC ───");
                foreach (var sug in OtherSuggestions.OrderBy(s => s.Priority))
                {
                    sb.AppendLine($"  • [{sug.Type}] {sug.Title}");
                    sb.AppendLine($"    {sug.Description}");
                }
            }

            return sb.ToString();
        }
    }
}
