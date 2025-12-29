using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Thông tin một công đoạn trong routing của sản phẩm
    /// </summary>
    public class RouteStep
    {
        /// <summary>
        /// ID công đoạn
        /// </summary>
        public int StageId { get; set; }

        /// <summary>
        /// Tên công đoạn
        /// </summary>
        public string StageName { get; set; }

        /// <summary>
        /// Thứ tự trong routing (1, 2, 3...)
        /// Khác với Stage.Order - đây là thứ tự riêng cho sản phẩm này
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// Bắt buộc phải qua công đoạn này không
        /// </summary>
        public bool IsMandatory { get; set; } = true;

        /// <summary>
        /// Có thể bỏ qua nếu điều kiện nào đó
        /// </summary>
        public bool CanSkip { get; set; } = false;

        /// <summary>
        /// Điều kiện để bỏ qua (VD: "Quantity < 100")
        /// </summary>
        public string SkipCondition { get; set; }

        /// <summary>
        /// Hệ số leadtime cho công đoạn này (nhân với base leadtime)
        /// VD: 1.5 = mất 150% thời gian so với chuẩn
        /// </summary>
        public double LeadtimeMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Thời gian xử lý cố định (phút) - cộng thêm vào
        /// VD: Setup đặc biệt cho sản phẩm này
        /// </summary>
        public int FixedProcessingTimeMinutes { get; set; } = 0;

        /// <summary>
        /// Ghi chú
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Danh sách Line ID được phép làm công đoạn này cho sản phẩm
        /// Null = tất cả Line hỗ trợ công đoạn này
        /// </summary>
        public List<string> AllowedLineIds { get; set; }

        /// <summary>
        /// Kiểm tra Line có được phép không
        /// </summary>
        public bool IsLineAllowed(string lineId)
        {
            if (AllowedLineIds == null || !AllowedLineIds.Any())
                return true;
            return AllowedLineIds.Contains(lineId);
        }

        public RouteStep() { }

        public RouteStep(int stageId, int sequence)
        {
            StageId = stageId;
            Sequence = sequence;
        }

        public override string ToString()
        {
            string skip = CanSkip ? " (có thể bỏ qua)" : "";
            string multiplier = LeadtimeMultiplier != 1.0 ? $" x{LeadtimeMultiplier:F1}" : "";
            return $"[{Sequence}] {StageName ?? $"Stage {StageId}"}{multiplier}{skip}";
        }
    }

    /// <summary>
    /// Routing cho một sản phẩm - định nghĩa các công đoạn cần đi qua
    /// </summary>
    public class ProductRouting
    {
        /// <summary>
        /// ID sản phẩm
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Danh sách các bước trong routing (đã sắp xếp theo Sequence)
        /// </summary>
        public List<RouteStep> Steps { get; set; } = new List<RouteStep>();

        /// <summary>
        /// Leadtime cơ bản (phút/sản phẩm) - áp dụng cho tất cả công đoạn nếu không có cấu hình riêng
        /// </summary>
        public double BaseLeadtimeMinutesPerUnit { get; set; } = 1.0;

        /// <summary>
        /// Leadtime theo công đoạn (phút/sản phẩm)
        /// Key = StageId, Value = LeadtimeMinutes
        /// Nếu có sẽ override BaseLeadtime cho công đoạn đó
        /// </summary>
        public Dictionary<int, double> StageLeadtimes { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// Hệ số độ phức tạp của sản phẩm (nhân với tất cả leadtime)
        /// VD: 1.2 = sản phẩm phức tạp hơn 20%
        /// </summary>
        public double ComplexityFactor { get; set; } = 1.0;

        /// <summary>
        /// Thời gian setup cố định khi bắt đầu sản xuất sản phẩm này (phút)
        /// </summary>
        public int InitialSetupTimeMinutes { get; set; } = 0;

        /// <summary>
        /// Ghi chú về routing
        /// </summary>
        public string Notes { get; set; }

        public ProductRouting() { }

        public ProductRouting(string productId)
        {
            ProductId = productId;
        }

        /// <summary>
        /// Thêm một bước vào routing
        /// </summary>
        public RouteStep AddStep(int stageId, string stageName = null)
        {
            int nextSeq = Steps.Any() ? Steps.Max(s => s.Sequence) + 1 : 1;
            var step = new RouteStep
            {
                StageId = stageId,
                StageName = stageName,
                Sequence = nextSeq
            };
            Steps.Add(step);
            return step;
        }

        /// <summary>
        /// Thêm bước với leadtime riêng
        /// </summary>
        public RouteStep AddStep(int stageId, double leadtimeMinutesPerUnit, string stageName = null)
        {
            var step = AddStep(stageId, stageName);
            StageLeadtimes[stageId] = leadtimeMinutesPerUnit;
            return step;
        }

        /// <summary>
        /// Đặt leadtime cho một công đoạn
        /// </summary>
        public void SetStageLeadtime(int stageId, double minutesPerUnit)
        {
            StageLeadtimes[stageId] = minutesPerUnit;
        }

        /// <summary>
        /// Lấy leadtime thực tế cho một công đoạn (đã tính complexity)
        /// </summary>
        public double GetEffectiveLeadtime(int stageId)
        {
            double baseTime = StageLeadtimes.ContainsKey(stageId) 
                ? StageLeadtimes[stageId] 
                : BaseLeadtimeMinutesPerUnit;

            var step = Steps.FirstOrDefault(s => s.StageId == stageId);
            double multiplier = step?.LeadtimeMultiplier ?? 1.0;
            int fixedTime = step?.FixedProcessingTimeMinutes ?? 0;

            return (baseTime * multiplier * ComplexityFactor) + fixedTime;
        }

        /// <summary>
        /// Tính tổng thời gian xử lý cho số lượng
        /// </summary>
        public double CalculateTotalProcessingTime(int stageId, int quantity, Line line = null)
        {
            double leadtime = GetEffectiveLeadtime(stageId);
            
            // Nếu có Line, tính thêm efficiency
            if (line != null && line.SupportsStage(stageId))
            {
                var cap = line.StageCapabilities[stageId];
                leadtime = leadtime / cap.Efficiency;
            }

            return leadtime * quantity;
        }

        /// <summary>
        /// Kiểm tra có công đoạn trong routing không
        /// </summary>
        public bool HasStage(int stageId)
        {
            return Steps.Any(s => s.StageId == stageId);
        }

        /// <summary>
        /// Lấy danh sách StageId theo thứ tự
        /// </summary>
        public List<int> GetStageSequence()
        {
            return Steps.OrderBy(s => s.Sequence).Select(s => s.StageId).ToList();
        }

        /// <summary>
        /// Lấy bước tiếp theo sau một công đoạn
        /// </summary>
        public RouteStep GetNextStep(int currentStageId)
        {
            var currentStep = Steps.FirstOrDefault(s => s.StageId == currentStageId);
            if (currentStep == null) return null;

            return Steps
                .Where(s => s.Sequence > currentStep.Sequence)
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();
        }

        /// <summary>
        /// Lấy bước trước đó
        /// </summary>
        public RouteStep GetPreviousStep(int currentStageId)
        {
            var currentStep = Steps.FirstOrDefault(s => s.StageId == currentStageId);
            if (currentStep == null) return null;

            return Steps
                .Where(s => s.Sequence < currentStep.Sequence)
                .OrderByDescending(s => s.Sequence)
                .FirstOrDefault();
        }

        /// <summary>
        /// Kiểm tra công đoạn có phải đầu tiên không
        /// </summary>
        public bool IsFirstStage(int stageId)
        {
            var firstStep = Steps.OrderBy(s => s.Sequence).FirstOrDefault();
            return firstStep != null && firstStep.StageId == stageId;
        }

        /// <summary>
        /// Kiểm tra công đoạn có phải cuối cùng không
        /// </summary>
        public bool IsLastStage(int stageId)
        {
            var lastStep = Steps.OrderByDescending(s => s.Sequence).FirstOrDefault();
            return lastStep != null && lastStep.StageId == stageId;
        }

        /// <summary>
        /// Số công đoạn trong routing
        /// </summary>
        public int StageCount => Steps.Count;

        /// <summary>
        /// Tạo routing mặc định (đi qua tất cả stages theo thứ tự)
        /// </summary>
        public static ProductRouting CreateDefault(string productId, List<Stage> allStages, 
            double baseLeadtime = 1.0)
        {
            var routing = new ProductRouting(productId)
            {
                BaseLeadtimeMinutesPerUnit = baseLeadtime
            };

            int seq = 1;
            foreach (var stage in allStages.OrderBy(s => s.Order))
            {
                routing.Steps.Add(new RouteStep
                {
                    StageId = stage.Id,
                    StageName = stage.Name,
                    Sequence = seq++
                });
            }

            return routing;
        }

        /// <summary>
        /// Tạo routing từ danh sách Stage IDs
        /// </summary>
        public static ProductRouting CreateFromStageIds(string productId, List<int> stageIds, 
            List<Stage> allStages, double baseLeadtime = 1.0)
        {
            var routing = new ProductRouting(productId)
            {
                BaseLeadtimeMinutesPerUnit = baseLeadtime
            };

            int seq = 1;
            foreach (var stageId in stageIds)
            {
                var stage = allStages.FirstOrDefault(s => s.Id == stageId);
                routing.Steps.Add(new RouteStep
                {
                    StageId = stageId,
                    StageName = stage?.Name ?? $"Stage {stageId}",
                    Sequence = seq++
                });
            }

            return routing;
        }

        public override string ToString()
        {
            var stageNames = Steps.OrderBy(s => s.Sequence).Select(s => s.StageName ?? $"S{s.StageId}");
            return $"{ProductId}: {string.Join(" → ", stageNames)} (Leadtime: {BaseLeadtimeMinutesPerUnit:F2}/unit)";
        }
    }

    /// <summary>
    /// Quản lý routing cho tất cả sản phẩm
    /// </summary>
    public class RoutingManager
    {
        /// <summary>
        /// Routing theo Product ID
        /// </summary>
        private Dictionary<string, ProductRouting> _routings = new Dictionary<string, ProductRouting>();

        /// <summary>
        /// Danh sách tất cả công đoạn (để tạo default routing)
        /// </summary>
        private List<Stage> _allStages;

        /// <summary>
        /// Leadtime mặc định khi không có cấu hình
        /// </summary>
        public double DefaultLeadtimeMinutesPerUnit { get; set; } = 1.0;

        public RoutingManager(List<Stage> stages)
        {
            _allStages = stages?.OrderBy(s => s.Order).ToList() ?? new List<Stage>();
        }

        /// <summary>
        /// Đặt routing cho một sản phẩm
        /// </summary>
        public void SetRouting(string productId, ProductRouting routing)
        {
            _routings[productId] = routing;
        }

        /// <summary>
        /// Tạo và đặt routing từ danh sách stage IDs
        /// </summary>
        public ProductRouting SetRouting(string productId, List<int> stageIds, double baseLeadtime = 0)
        {
            if (baseLeadtime <= 0) baseLeadtime = DefaultLeadtimeMinutesPerUnit;
            
            var routing = ProductRouting.CreateFromStageIds(productId, stageIds, _allStages, baseLeadtime);
            _routings[productId] = routing;
            return routing;
        }

        /// <summary>
        /// Tạo routing đơn giản với leadtime
        /// </summary>
        public ProductRouting SetSimpleRouting(string productId, double leadtimeMinutesPerUnit, 
            params int[] stageIds)
        {
            var routing = new ProductRouting(productId)
            {
                BaseLeadtimeMinutesPerUnit = leadtimeMinutesPerUnit
            };

            int seq = 1;
            foreach (var stageId in stageIds)
            {
                var stage = _allStages.FirstOrDefault(s => s.Id == stageId);
                routing.Steps.Add(new RouteStep
                {
                    StageId = stageId,
                    StageName = stage?.Name,
                    Sequence = seq++
                });
            }

            _routings[productId] = routing;
            return routing;
        }

        /// <summary>
        /// Lấy routing cho một sản phẩm (tạo default nếu chưa có)
        /// </summary>
        public ProductRouting GetRouting(string productId)
        {
            if (_routings.TryGetValue(productId, out var routing))
                return routing;

            // Tạo default routing (đi qua tất cả stages)
            routing = ProductRouting.CreateDefault(productId, _allStages, DefaultLeadtimeMinutesPerUnit);
            _routings[productId] = routing;
            return routing;
        }

        /// <summary>
        /// Kiểm tra sản phẩm có routing tùy chỉnh không
        /// </summary>
        public bool HasCustomRouting(string productId)
        {
            return _routings.ContainsKey(productId);
        }

        /// <summary>
        /// Lấy danh sách công đoạn cho một sản phẩm
        /// </summary>
        public List<int> GetStagesForProduct(string productId)
        {
            return GetRouting(productId).GetStageSequence();
        }

        /// <summary>
        /// Tính thời gian xử lý cho sản phẩm tại công đoạn
        /// </summary>
        public double CalculateProcessingTime(string productId, int stageId, int quantity, Line line = null)
        {
            var routing = GetRouting(productId);
            if (!routing.HasStage(stageId))
                return 0; // Sản phẩm không đi qua công đoạn này

            return routing.CalculateTotalProcessingTime(stageId, quantity, line);
        }

        /// <summary>
        /// Tạo routing với leadtime riêng cho từng công đoạn
        /// </summary>
        /// <param name="productId">ID sản phẩm</param>
        /// <param name="stageLeadtimes">Dictionary: Key = StageId, Value = Leadtime (phút/sp)</param>
        /// <returns>ProductRouting đã tạo</returns>
        public ProductRouting SetRoutingWithStageLeadtimes(string productId, Dictionary<int, double> stageLeadtimes)
        {
            var routing = new ProductRouting(productId);

            int seq = 1;
            foreach (var kvp in stageLeadtimes.OrderBy(x => x.Key))
            {
                int stageId = kvp.Key;
                double leadtime = kvp.Value;

                var stage = _allStages.FirstOrDefault(s => s.Id == stageId);
                routing.Steps.Add(new RouteStep
                {
                    StageId = stageId,
                    StageName = stage?.Name,
                    Sequence = seq++
                });

                // Đặt leadtime riêng cho công đoạn này
                routing.StageLeadtimes[stageId] = leadtime;
            }

            _routings[productId] = routing;
            return routing;
        }

        /// <summary>
        /// Đặt leadtime cho một công đoạn của sản phẩm
        /// </summary>
        public void SetStageLeadtime(string productId, int stageId, double minutesPerUnit)
        {
            var routing = GetRouting(productId);
            routing.SetStageLeadtime(stageId, minutesPerUnit);
        }

        /// <summary>
        /// Kiểm tra sản phẩm có cần đi qua công đoạn không
        /// </summary>
        public bool ProductRequiresStage(string productId, int stageId)
        {
            return GetRouting(productId).HasStage(stageId);
        }

        /// <summary>
        /// In báo cáo routing
        /// </summary>
        public string GenerateReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("              ROUTING VÀ LEADTIME THEO SẢN PHẨM            ");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var kvp in _routings.OrderBy(r => r.Key))
            {
                var routing = kvp.Value;
                sb.AppendLine($"► {routing.ProductId}");
                sb.AppendLine($"  Leadtime cơ bản: {routing.BaseLeadtimeMinutesPerUnit:F2} phút/sp");
                sb.AppendLine($"  Hệ số phức tạp: x{routing.ComplexityFactor:F2}");
                sb.AppendLine($"  Routing ({routing.StageCount} công đoạn):");

                foreach (var step in routing.Steps.OrderBy(s => s.Sequence))
                {
                    double effectiveLeadtime = routing.GetEffectiveLeadtime(step.StageId);
                    string customLeadtime = routing.StageLeadtimes.ContainsKey(step.StageId) 
                        ? $" (custom: {routing.StageLeadtimes[step.StageId]:F2})" 
                        : "";
                    
                    sb.AppendLine($"    {step.Sequence}. {step.StageName ?? $"Stage {step.StageId}"} " +
                                 $"- {effectiveLeadtime:F2} phút/sp{customLeadtime}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Mở rộng Product để lưu routing
    /// </summary>
    public static class ProductRoutingExtensions
    {
        private static Dictionary<string, ProductRouting> _productRoutings = new Dictionary<string, ProductRouting>();

        /// <summary>
        /// Gán routing cho product
        /// </summary>
        public static void SetRouting(this Product product, ProductRouting routing)
        {
            _productRoutings[product.Id] = routing;
        }

        /// <summary>
        /// Lấy routing của product
        /// </summary>
        public static ProductRouting GetRouting(this Product product)
        {
            _productRoutings.TryGetValue(product.Id, out var routing);
            return routing;
        }

        /// <summary>
        /// Kiểm tra có routing tùy chỉnh không
        /// </summary>
        public static bool HasCustomRouting(this Product product)
        {
            return _productRoutings.ContainsKey(product.Id);
        }
    }
}
