using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Cấp độ kỹ năng của Operator
    /// </summary>
    public enum SkillLevel
    {
        /// <summary>Không có kỹ năng</summary>
        None = 0,
        
        /// <summary>Đang training - cần giám sát</summary>
        Training = 1,
        
        /// <summary>Cơ bản - làm được nhưng chậm hơn</summary>
        Basic = 2,
        
        /// <summary>Thành thạo - làm việc bình thường</summary>
        Proficient = 3,
        
        /// <summary>Chuyên gia - làm nhanh hơn, có thể training người khác</summary>
        Expert = 4
    }

    /// <summary>
    /// Kỹ năng của Operator cho một công đoạn
    /// </summary>
    public class OperatorSkill
    {
        public int StageId { get; set; }
        public SkillLevel Level { get; set; }
        
        /// <summary>
        /// Hệ số hiệu suất dựa trên skill level
        /// Training = 0.5, Basic = 0.7, Proficient = 1.0, Expert = 1.2
        /// </summary>
        public double EfficiencyFactor
        {
            get
            {
                switch (Level)
                {
                    case SkillLevel.Training: return 0.5;
                    case SkillLevel.Basic: return 0.7;
                    case SkillLevel.Proficient: return 1.0;
                    case SkillLevel.Expert: return 1.2;
                    default: return 0;
                }
            }
        }

        /// <summary>
        /// Có thể làm việc độc lập không (không cần giám sát)
        /// </summary>
        public bool CanWorkIndependently => Level >= SkillLevel.Basic;

        /// <summary>
        /// Có thể training người khác không
        /// </summary>
        public bool CanTrainOthers => Level >= SkillLevel.Expert;

        public OperatorSkill() { }

        public OperatorSkill(int stageId, SkillLevel level)
        {
            StageId = stageId;
            Level = level;
        }
    }

    /// <summary>
    /// Lịch làm việc của Operator
    /// </summary>
    public class OperatorSchedule
    {
        /// <summary>
        /// Ngày
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Có đi làm không
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Ca làm việc (null = ca mặc định)
        /// </summary>
        public WorkShift Shift { get; set; }

        /// <summary>
        /// Có thể OT không
        /// </summary>
        public bool CanOT { get; set; } = true;

        /// <summary>
        /// Số giờ OT tối đa
        /// </summary>
        public double MaxOTHours { get; set; } = 4;

        /// <summary>
        /// Lý do nghỉ (nếu không available)
        /// </summary>
        public string AbsenceReason { get; set; }

        /// <summary>
        /// Được assign cho Line nào (null = flexible)
        /// </summary>
        public string AssignedLineId { get; set; }
    }

    /// <summary>
    /// Người vận hành (Operator)
    /// </summary>
    public class Operator
    {
        /// <summary>
        /// Mã nhân viên
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tên nhân viên
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Bộ phận
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// Đang làm việc (không nghỉ việc)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Ngày bắt đầu làm việc
        /// </summary>
        public DateTime HireDate { get; set; }

        /// <summary>
        /// Kỹ năng cho từng công đoạn
        /// Key = StageId
        /// </summary>
        public Dictionary<int, OperatorSkill> Skills { get; set; } = new Dictionary<int, OperatorSkill>();

        /// <summary>
        /// Lịch làm việc cá nhân (ngày nghỉ phép, etc.)
        /// Key = Date
        /// </summary>
        public Dictionary<DateTime, OperatorSchedule> PersonalSchedule { get; set; } 
            = new Dictionary<DateTime, OperatorSchedule>();

        /// <summary>
        /// Line được assign cố định (null = có thể làm nhiều line)
        /// </summary>
        public string PrimaryLineId { get; set; }

        /// <summary>
        /// Có thể làm OT không
        /// </summary>
        public bool CanWorkOT { get; set; } = true;

        /// <summary>
        /// Số giờ OT tối đa mỗi tuần
        /// </summary>
        public double MaxOTHoursPerWeek { get; set; } = 20;

        /// <summary>
        /// Chi phí lương theo giờ (để tính toán tối ưu)
        /// </summary>
        public decimal HourlyRate { get; set; }

        /// <summary>
        /// Hệ số lương OT (VD: 1.5 = 150%)
        /// </summary>
        public decimal OTRateMultiplier { get; set; } = 1.5m;

        public Operator() { }

        public Operator(string id, string name, DateTime hireDate)
        {
            Id = id;
            Name = name;
            HireDate = hireDate;
        }

        /// <summary>
        /// Thêm kỹ năng cho một công đoạn
        /// </summary>
        public void AddSkill(int stageId, SkillLevel level)
        {
            Skills[stageId] = new OperatorSkill(stageId, level);
        }

        /// <summary>
        /// Thêm kỹ năng cho nhiều công đoạn
        /// </summary>
        public void AddSkills(IEnumerable<int> stageIds, SkillLevel level)
        {
            foreach (var stageId in stageIds)
            {
                AddSkill(stageId, level);
            }
        }

        /// <summary>
        /// Kiểm tra có kỹ năng cho công đoạn không
        /// </summary>
        public bool HasSkillFor(int stageId)
        {
            return Skills.ContainsKey(stageId) && Skills[stageId].Level > SkillLevel.None;
        }

        /// <summary>
        /// Kiểm tra có thể làm độc lập cho công đoạn không
        /// </summary>
        public bool CanWorkIndependentlyOn(int stageId)
        {
            return Skills.ContainsKey(stageId) && Skills[stageId].CanWorkIndependently;
        }

        /// <summary>
        /// Lấy cấp độ kỹ năng cho công đoạn
        /// </summary>
        public SkillLevel GetSkillLevel(int stageId)
        {
            return Skills.ContainsKey(stageId) ? Skills[stageId].Level : SkillLevel.None;
        }

        /// <summary>
        /// Lấy hệ số hiệu suất cho công đoạn
        /// </summary>
        public double GetEfficiencyFor(int stageId)
        {
            return Skills.ContainsKey(stageId) ? Skills[stageId].EfficiencyFactor : 0;
        }

        /// <summary>
        /// Lấy danh sách công đoạn có thể làm
        /// </summary>
        public List<int> GetCapableStages(bool independentOnly = false)
        {
            if (independentOnly)
            {
                return Skills.Where(s => s.Value.CanWorkIndependently)
                             .Select(s => s.Key)
                             .ToList();
            }
            return Skills.Where(s => s.Value.Level > SkillLevel.None)
                         .Select(s => s.Key)
                         .ToList();
        }

        /// <summary>
        /// Kiểm tra có available vào ngày cụ thể không
        /// </summary>
        public bool IsAvailableOn(DateTime date)
        {
            if (!IsActive) return false;
            if (date < HireDate) return false;

            if (PersonalSchedule.TryGetValue(date.Date, out var schedule))
            {
                return schedule.IsAvailable;
            }

            return true; // Mặc định là available
        }

        /// <summary>
        /// Thêm ngày nghỉ phép
        /// </summary>
        public void AddLeave(DateTime date, string reason = "Nghỉ phép")
        {
            PersonalSchedule[date.Date] = new OperatorSchedule
            {
                Date = date.Date,
                IsAvailable = false,
                AbsenceReason = reason
            };
        }

        /// <summary>
        /// Thêm nhiều ngày nghỉ phép
        /// </summary>
        public void AddLeaveRange(DateTime startDate, DateTime endDate, string reason = "Nghỉ phép")
        {
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                AddLeave(date, reason);
            }
        }

        /// <summary>
        /// Kiểm tra có thể OT vào ngày cụ thể không
        /// </summary>
        public bool CanOTOn(DateTime date)
        {
            if (!CanWorkOT) return false;
            if (!IsAvailableOn(date)) return false;

            if (PersonalSchedule.TryGetValue(date.Date, out var schedule))
            {
                return schedule.CanOT;
            }

            return true;
        }

        public override string ToString()
        {
            var skills = string.Join(", ", Skills.Select(s => $"CĐ{s.Key}:{s.Value.Level}"));
            return $"{Name} ({Id}) - Kỹ năng: [{skills}]";
        }
    }

    /// <summary>
    /// Yêu cầu tuyển dụng
    /// </summary>
    public class HiringRequirement
    {
        /// <summary>
        /// Công đoạn cần người
        /// </summary>
        public int StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Số người cần tuyển
        /// </summary>
        public int RequiredCount { get; set; }

        /// <summary>
        /// Ngày cần có người (đã trừ thời gian training)
        /// </summary>
        public DateTime NeededByDate { get; set; }

        /// <summary>
        /// Ngày muộn nhất phải tuyển (để kịp training)
        /// </summary>
        public DateTime LatestHireDate { get; set; }

        /// <summary>
        /// Thời gian training cần thiết (ngày)
        /// </summary>
        public int TrainingDaysRequired { get; set; }

        /// <summary>
        /// Skill level yêu cầu sau training
        /// </summary>
        public SkillLevel RequiredSkillLevel { get; set; } = SkillLevel.Basic;

        /// <summary>
        /// Lý do cần tuyển
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Mức độ ưu tiên (1 = cao nhất)
        /// </summary>
        public int Priority { get; set; } = 1;

        public override string ToString()
        {
            return $"Cần {RequiredCount} người cho {StageName}, " +
                   $"tuyển trước {LatestHireDate:dd/MM/yyyy} " +
                   $"(training {TrainingDaysRequired} ngày)";
        }
    }

    /// <summary>
    /// Gợi ý tăng ca (OT)
    /// </summary>
    public class OTSuggestion
    {
        /// <summary>
        /// Ngày cần OT
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Loại ngày (Weekend, Holiday, Weekday evening)
        /// </summary>
        public string DayType { get; set; }

        /// <summary>
        /// Công đoạn cần OT
        /// </summary>
        public int StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Line cần OT
        /// </summary>
        public string LineId { get; set; }
        public string LineName { get; set; }

        /// <summary>
        /// Số giờ OT cần
        /// </summary>
        public double OTHoursNeeded { get; set; }

        /// <summary>
        /// Số operator cần cho OT
        /// </summary>
        public int OperatorsNeeded { get; set; }

        /// <summary>
        /// Danh sách operator có thể làm OT
        /// </summary>
        public List<string> AvailableOperatorIds { get; set; } = new List<string>();

        /// <summary>
        /// Sản phẩm liên quan
        /// </summary>
        public string ProductId { get; set; }
        public string ProductName { get; set; }

        /// <summary>
        /// Số lượng có thể sản xuất thêm
        /// </summary>
        public int AdditionalQuantity { get; set; }

        /// <summary>
        /// Chi phí OT ước tính
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Mức độ cần thiết (1-10, 10 = rất cần)
        /// </summary>
        public int Urgency { get; set; }

        /// <summary>
        /// Lý do cần OT
        /// </summary>
        public string Reason { get; set; }

        public override string ToString()
        {
            return $"{Date:dd/MM/yyyy} ({DayType}): {StageName} - {LineName}, " +
                   $"cần {OTHoursNeeded:F1}h OT với {OperatorsNeeded} người";
        }
    }

    /// <summary>
    /// Kết quả phân tích năng lực operator
    /// </summary>
    public class OperatorCapacityAnalysis
    {
        public int StageId { get; set; }
        public string StageName { get; set; }

        /// <summary>
        /// Số operator có thể làm công đoạn này
        /// </summary>
        public int TotalOperators { get; set; }

        /// <summary>
        /// Số operator làm độc lập được
        /// </summary>
        public int IndependentOperators { get; set; }

        /// <summary>
        /// Số operator đang training
        /// </summary>
        public int TrainingOperators { get; set; }

        /// <summary>
        /// Tổng giờ công khả dụng (trong khoảng thời gian)
        /// </summary>
        public double TotalAvailableHours { get; set; }

        /// <summary>
        /// Tổng giờ công cần thiết
        /// </summary>
        public double TotalRequiredHours { get; set; }

        /// <summary>
        /// Thiếu hụt giờ công
        /// </summary>
        public double ShortfallHours => Math.Max(0, TotalRequiredHours - TotalAvailableHours);

        /// <summary>
        /// Có thiếu người không
        /// </summary>
        public bool HasShortfall => ShortfallHours > 0;

        /// <summary>
        /// Tỷ lệ sử dụng (%)
        /// </summary>
        public double UtilizationPercent => TotalAvailableHours > 0 
            ? (TotalRequiredHours / TotalAvailableHours) * 100 
            : 0;

        /// <summary>
        /// Số người cần tuyển thêm (ước tính)
        /// </summary>
        public int EstimatedHiringNeed { get; set; }
    }
}
