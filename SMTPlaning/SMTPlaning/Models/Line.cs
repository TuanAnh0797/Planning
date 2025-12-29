using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Khả năng của Line cho một công đoạn
    /// KHÔNG chứa CycleTime vì đã có Leadtime trong ProductRouting
    /// Chỉ chứa Efficiency để điều chỉnh thời gian theo năng lực Line
    /// </summary>
    public class StageCapability
    {
        /// <summary>
        /// ID công đoạn
        /// </summary>
        public int StageId { get; set; }

        /// <summary>
        /// Hiệu suất Line cho công đoạn này (0.0 - 1.0)
        /// VD: 0.85 = Line chạy ở 85% hiệu suất
        /// Dùng để điều chỉnh: ActualTime = ProductLeadtime / Efficiency
        /// </summary>
        public double Efficiency { get; set; } = 1.0;

        /// <summary>
        /// Có đang hoạt động không
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Ghi chú
        /// </summary>
        public string Notes { get; set; }

        public StageCapability() { }

        public StageCapability(int stageId, double efficiency = 1.0)
        {
            StageId = stageId;
            Efficiency = Math.Max(0.1, Math.Min(1.5, efficiency)); // 10% - 150%
        }

        public override string ToString()
        {
            return $"Stage {StageId}: Efficiency={Efficiency:P0}";
        }
    }

    /// <summary>
    /// Dây chuyền sản xuất SMT
    /// - Hỗ trợ một số hoặc tất cả công đoạn (Flexible Job Shop)
    /// - Không chứa CycleTime vì đã có Leadtime trong ProductRouting
    /// - Chứa Efficiency để điều chỉnh thời gian thực tế
    /// </summary>
    public class Line
    {
        /// <summary>
        /// Mã Line
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tên Line
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Số khe Feeder tối đa
        /// </summary>
        public int MaxFeederSlots { get; set; } = 100;

        /// <summary>
        /// Số giờ làm việc mỗi ngày
        /// </summary>
        public double WorkingHoursPerDay { get; set; } = 8;

        /// <summary>
        /// Line có đang hoạt động không
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Khả năng của Line theo từng công đoạn
        /// Key = StageId
        /// Nếu StageId không có trong dictionary = Line KHÔNG hỗ trợ công đoạn đó
        /// </summary>
        public Dictionary<int, StageCapability> StageCapabilities { get; set; } 
            = new Dictionary<int, StageCapability>();

        /// <summary>
        /// Thời gian chuẩn bị mặc định khi đổi sản phẩm (phút)
        /// Không tính thời gian thay linh kiện
        /// </summary>
        public double DefaultSetupTimeMinutes { get; set; } = 10;

        /// <summary>
        /// Vị trí vật lý của Line (mét từ điểm gốc)
        /// Dùng để tính Transfer Time tự động
        /// </summary>
        public double PhysicalPosition { get; set; } = 0;

        /// <summary>
        /// Khu vực/Zone của Line
        /// </summary>
        public string Zone { get; set; }

        /// <summary>
        /// Ghi chú
        /// </summary>
        public string Notes { get; set; }

        public Line() { }

        public Line(string id, string name, int maxFeederSlots = 100, 
                   double workingHoursPerDay = 8)
        {
            Id = id;
            Name = name;
            MaxFeederSlots = maxFeederSlots;
            WorkingHoursPerDay = workingHoursPerDay;
        }

        /// <summary>
        /// Thêm khả năng hỗ trợ một công đoạn
        /// </summary>
        /// <param name="stageId">ID công đoạn</param>
        /// <param name="efficiency">Hiệu suất (0.1 - 1.5), mặc định 1.0</param>
        public void AddStageCapability(int stageId, double efficiency = 1.0)
        {
            StageCapabilities[stageId] = new StageCapability(stageId, efficiency);
        }

        /// <summary>
        /// Thêm khả năng hỗ trợ nhiều công đoạn với cùng efficiency
        /// </summary>
        public void AddStageCapabilities(IEnumerable<int> stageIds, double efficiency = 1.0)
        {
            foreach (var stageId in stageIds)
            {
                AddStageCapability(stageId, efficiency);
            }
        }

        /// <summary>
        /// Thêm khả năng hỗ trợ tất cả công đoạn từ 1 đến maxStageId
        /// </summary>
        public void AddAllStages(int maxStageId, double efficiency = 1.0)
        {
            for (int i = 1; i <= maxStageId; i++)
            {
                AddStageCapability(i, efficiency);
            }
        }

        /// <summary>
        /// BACKWARD COMPATIBILITY: Thêm capability với cycleTime (sẽ bị bỏ qua)
        /// CycleTime bây giờ được lấy từ ProductRouting.Leadtime
        /// </summary>
        [Obsolete("CycleTime should be defined in ProductRouting. This parameter is ignored.")]
        public void AddStageCapability(int stageId, double cycleTimeMinutes, double efficiency)
        {
            // Bỏ qua cycleTimeMinutes, chỉ lưu efficiency
            AddStageCapability(stageId, efficiency);
        }

        /// <summary>
        /// Kiểm tra Line có hỗ trợ công đoạn này không
        /// </summary>
        public bool SupportsStage(int stageId)
        {
            return StageCapabilities.ContainsKey(stageId) && 
                   StageCapabilities[stageId].IsEnabled &&
                   StageCapabilities[stageId].Efficiency > 0;
        }

        /// <summary>
        /// Lấy efficiency cho một công đoạn
        /// </summary>
        public double GetEfficiency(int stageId)
        {
            if (StageCapabilities.TryGetValue(stageId, out var cap))
                return cap.Efficiency;
            return 1.0;
        }

        /// <summary>
        /// BACKWARD COMPATIBILITY: GetActualCycleTime
        /// Trả về 1.0 / Efficiency (giả sử base leadtime = 1.0)
        /// Thực tế nên dùng ProductRouting.CalculateTotalProcessingTime()
        /// </summary>
        [Obsolete("Use ProductRouting.CalculateTotalProcessingTime() instead")]
        public double GetActualCycleTime(int stageId)
        {
            if (!SupportsStage(stageId))
                return double.MaxValue;

            // Giả sử leadtime = 1.0, trả về 1.0 / efficiency
            return 1.0 / GetEfficiency(stageId);
        }

        /// <summary>
        /// Tính thời gian xử lý dựa trên Leadtime của Product
        /// ProcessingTime = ProductLeadtime × Quantity / LineEfficiency
        /// </summary>
        public double CalculateProcessingTime(int stageId, int quantity, double productLeadtimePerUnit)
        {
            if (!SupportsStage(stageId))
                return double.MaxValue;

            double efficiency = GetEfficiency(stageId);
            return (productLeadtimePerUnit * quantity) / efficiency;
        }

        /// <summary>
        /// BACKWARD COMPATIBILITY: CalculateProcessingTime với default leadtime = 1.0
        /// Thực tế nên dùng overload với productLeadtimePerUnit
        /// </summary>
        public double CalculateProcessingTime(int stageId, int quantity)
        {
            // Sử dụng leadtime mặc định = 1.0 phút/sản phẩm
            return CalculateProcessingTime(stageId, quantity, 1.0);
        }

        /// <summary>
        /// Kiểm tra Line có đủ khe Feeder cho sản phẩm không
        /// </summary>
        public bool HasEnoughFeederSlots(int requiredSlots)
        {
            return MaxFeederSlots >= requiredSlots;
        }

        /// <summary>
        /// Lấy danh sách các công đoạn mà Line hỗ trợ
        /// </summary>
        public List<int> GetSupportedStages()
        {
            return StageCapabilities
                .Where(kv => kv.Value.IsEnabled && kv.Value.Efficiency > 0)
                .Select(kv => kv.Key)
                .OrderBy(s => s)
                .ToList();
        }

        /// <summary>
        /// Danh sách công đoạn Line hỗ trợ (property)
        /// </summary>
        public List<int> SupportedStages => GetSupportedStages();

        public override string ToString()
        {
            string stages = string.Join(", ", GetSupportedStages());
            return $"{Name} ({MaxFeederSlots} slots, CĐ: [{stages}])";
        }
    }
}
