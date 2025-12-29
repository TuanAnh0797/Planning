using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;
using SMTScheduler.Models;

namespace SMTScheduler.Services
{
    /// <summary>
    /// Service lập lịch sản xuất SMT sử dụng OR-Tools CP-SAT
    /// Hỗ trợ:
    /// - Flexible Job Shop: Line có thể làm một số hoặc tất cả công đoạn
    /// - Manual Grouping: Nhóm sản phẩm do người dùng định nghĩa sẵn
    /// - Stage Naming: Model đổi tên qua từng công đoạn
    /// - Working Calendar: Lịch làm việc với ngày nghỉ
    /// - Smart Suggestions: Gợi ý OT, tuyển dụng khi thiếu năng lực
    /// - Transfer Time: Thời gian di chuyển giữa các Line
    /// - Lot Splitting: Chia lot lớn thành batch, cho phép pipeline giữa công đoạn
    /// - Priority Levels: Mức độ ưu tiên khi cùng due date
    /// - Custom Routing: Mỗi model đi qua các công đoạn riêng
    /// - Custom Leadtime: Mỗi model có leadtime riêng
    /// 
    /// Tính năng tùy chọn (có thể tắt):
    /// - Component Grouping: Tự động group theo linh kiện (EnableComponentGrouping)
    /// - Operator Management: Quản lý người vận hành (EnableOperatorManagement)
    /// </summary>
    public class SMTSchedulerService
    {
        private readonly List<Stage> _stages;
        private readonly List<Line> _lines;
        private readonly List<Product> _products;
        private readonly List<Component> _components;
        private readonly List<Operator> _operators;
        private readonly DateTime _referenceDate;
        private readonly WorkingCalendar _calendar;
        private readonly TransferTimeMatrix _transferMatrix;
        private readonly ComponentGroupingService _groupingService;
        private readonly SuggestionService _suggestionService;
        private readonly RoutingManager _routingManager;

        // Lưu trữ ProductScheduleInfo (bao gồm lot config và priority)
        private Dictionary<string, ProductScheduleInfo> _productScheduleInfos = new Dictionary<string, ProductScheduleInfo>();

        // Lưu trữ Manual Groups
        private List<ManualProductGroup> _manualGroups = new List<ManualProductGroup>();

        // ====== CẤU HÌNH CHÍNH ======

        /// <summary>
        /// Bật/tắt Component Grouping (tự động group theo linh kiện)
        /// Mặc định: false (tắt)
        /// </summary>
        public bool EnableComponentGrouping { get; set; } = false;

        /// <summary>
        /// Bật/tắt Operator Management (quản lý người vận hành)
        /// Mặc định: false (tắt)
        /// </summary>
        public bool EnableOperatorManagement { get; set; } = false;

        /// <summary>
        /// Sử dụng Manual Groups (nhóm do người dùng định nghĩa)
        /// </summary>
        public bool UseManualGrouping { get; set; } = false;

        /// <summary>
        /// Bật hiển thị tên model theo công đoạn trong kết quả
        /// </summary>
        public bool EnableStageNaming { get; set; } = true;

        /// <summary>
        /// Bật ràng buộc deadline cứng (nếu false, cho phép trễ deadline)
        /// </summary>
        public bool UseHardDeadlineConstraint { get; set; } = false;

        /// <summary>
        /// Bật gợi ý OT, tuyển dụng khi thiếu năng lực
        /// </summary>
        public bool EnableSuggestions { get; set; } = true;

        /// <summary>
        /// Bật tính thời gian di chuyển giữa các Line
        /// </summary>
        public bool EnableTransferTime { get; set; } = true;

        /// <summary>
        /// Bật chia lot lớn thành batch
        /// </summary>
        public bool EnableLotSplitting { get; set; } = true;

        /// <summary>
        /// Bật sắp xếp theo Priority
        /// </summary>
        public bool EnablePriorityScheduling { get; set; } = true;

        /// <summary>
        /// Bật Custom Routing (mỗi model đi qua công đoạn riêng)
        /// </summary>
        public bool EnableCustomRouting { get; set; } = true;

        // Backward compatibility
        public bool EnableGrouping 
        { 
            get => EnableComponentGrouping; 
            set => EnableComponentGrouping = value; 
        }

        public double MinSimilarityPercent { get; set; } = 30;
        public SuggestionConfig SuggestionConfig { get; set; } = new SuggestionConfig();
        public LotSplitConfig DefaultLotConfig { get; set; } = LotSplitConfig.NoSplitting;

        /// <summary>
        /// Lấy RoutingManager để cấu hình routing
        /// </summary>
        public RoutingManager RoutingManager => _routingManager;

        /// <summary>
        /// Danh sách Manual Groups
        /// </summary>
        public List<ManualProductGroup> ManualGroups => _manualGroups;

        /// <summary>
        /// Ma trận thời gian transfer giữa các công đoạn
        /// </summary>
        private StageTransferTimeMatrix _stageTransferMatrix;

        /// <summary>
        /// Lấy/Đặt ma trận thời gian transfer giữa các công đoạn
        /// </summary>
        public StageTransferTimeMatrix StageTransferMatrix
        {
            get => _stageTransferMatrix;
            set => _stageTransferMatrix = value;
        }

        /// <summary>
        /// Bật/tắt tính thời gian transfer giữa các công đoạn
        /// </summary>
        public bool EnableStageTransferTime { get; set; } = true;

        // ====== CONSTRUCTORS ======

        /// <summary>
        /// Constructor ĐƠN GIẢN - không cần Component và Operator
        /// </summary>
        public SMTSchedulerService(
            List<Stage> stages,
            List<Line> lines,
            List<Product> products,
            DateTime referenceDate,
            WorkingCalendar calendar = null,
            TransferTimeMatrix transferMatrix = null,
            StageTransferTimeMatrix stageTransferMatrix = null)
            : this(stages, lines, products, null, null, referenceDate, 
                   calendar ?? CreateDefaultCalendar(referenceDate), transferMatrix, stageTransferMatrix)
        {
            // Tắt các tính năng liên quan đến Component và Operator
            EnableComponentGrouping = false;
            EnableOperatorManagement = false;
        }

        /// <summary>
        /// Constructor đầy đủ với tất cả options
        /// </summary>
        public SMTSchedulerService(
            List<Stage> stages,
            List<Line> lines,
            List<Product> products,
            List<Component> components,
            List<Operator> operators,
            DateTime referenceDate,
            WorkingCalendar calendar,
            TransferTimeMatrix transferMatrix = null,
            StageTransferTimeMatrix stageTransferMatrix = null)
        {
            _stages = stages.OrderBy(s => s.Order).ToList();
            _lines = lines.Where(l => l.IsActive).ToList();
            _products = products;
            _components = components ?? new List<Component>();
            _operators = operators ?? new List<Operator>();
            _referenceDate = referenceDate;
            _calendar = calendar ?? CreateDefaultCalendar(referenceDate);
            _transferMatrix = transferMatrix ?? new TransferTimeMatrix(15); // Mặc định 15 phút
            _stageTransferMatrix = stageTransferMatrix ?? StageTransferTimeMatrix.CreateDefault(_stages, 5); // Mặc định 5 phút

            // Khởi tạo grouping service
            int maxFeederSlots = _lines.Any() ? _lines.Max(l => l.MaxFeederSlots) : 100;
            _groupingService = new ComponentGroupingService(_components, maxFeederSlots);

            // Khởi tạo suggestion service
            _suggestionService = new SuggestionService(_stages, _lines, _operators, _calendar);

            // Khởi tạo routing manager
            _routingManager = new RoutingManager(_stages);

            CalculateProductFeederSlots();
        }

        #region Manual Grouping Methods

        /// <summary>
        /// Thêm một nhóm sản phẩm thủ công
        /// </summary>
        public void AddManualGroup(ManualProductGroup group)
        {
            if (group == null) return;
            
            // Xóa group cũ nếu đã tồn tại
            _manualGroups.RemoveAll(g => g.GroupId == group.GroupId);
            _manualGroups.Add(group);

            // Cập nhật GroupId cho các sản phẩm trong nhóm
            int sequence = 1;
            foreach (var productId in group.ProductIds)
            {
                var product = _products.FirstOrDefault(p => p.Id == productId);
                if (product != null)
                {
                    product.GroupId = group.GroupId;
                    product.SequenceInGroup = sequence++;
                }
            }
        }

        /// <summary>
        /// Tạo và thêm nhóm sản phẩm nhanh
        /// </summary>
        public ManualProductGroup CreateManualGroup(string groupId, string groupName, params string[] productIds)
        {
            var group = new ManualProductGroup(groupId, groupName);
            group.AddProducts(productIds);
            AddManualGroup(group);
            return group;
        }

        /// <summary>
        /// Xóa một nhóm sản phẩm
        /// </summary>
        public void RemoveManualGroup(string groupId)
        {
            _manualGroups.RemoveAll(g => g.GroupId == groupId);
            
            // Xóa GroupId khỏi các sản phẩm
            foreach (var product in _products.Where(p => p.GroupId == groupId))
            {
                product.GroupId = null;
                product.SequenceInGroup = 0;
            }
        }

        /// <summary>
        /// Lấy nhóm theo ID
        /// </summary>
        public ManualProductGroup GetManualGroup(string groupId)
        {
            return _manualGroups.FirstOrDefault(g => g.GroupId == groupId);
        }

        /// <summary>
        /// Lấy danh sách sản phẩm đã sắp xếp theo Manual Groups
        /// </summary>
        public List<Product> GetProductsSortedByManualGroups()
        {
            var result = new List<Product>();

            // 1. Thêm sản phẩm theo thứ tự nhóm
            foreach (var group in _manualGroups.OrderBy(g => g.GroupPriority))
            {
                var productsInGroup = group.ProductIds
                    .Select(id => _products.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .OrderBy(p => p.SequenceInGroup)
                    .ToList();

                result.AddRange(productsInGroup);
            }

            // 2. Thêm sản phẩm không thuộc nhóm nào (sắp xếp theo deadline)
            var ungroupedProducts = _products
                .Where(p => string.IsNullOrEmpty(p.GroupId))
                .OrderBy(p => p.DueDate)
                .ThenBy(p => p.Priority);

            result.AddRange(ungroupedProducts);

            return result;
        }

        /// <summary>
        /// Chuyển đổi Manual Groups sang ProductGroup (để tương thích với logic cũ)
        /// </summary>
        private List<ProductGroup> ConvertManualGroupsToProductGroups()
        {
            var result = new List<ProductGroup>();

            foreach (var manualGroup in _manualGroups.OrderBy(g => g.GroupPriority))
            {
                var products = manualGroup.ProductIds
                    .Select(id => _products.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .OrderBy(p => p.SequenceInGroup)
                    .ToList();

                if (!products.Any()) continue;

                var productGroup = new ProductGroup
                {
                    GroupId = manualGroup.GroupId,
                    GroupName = manualGroup.GroupName,
                    Products = products,
                    AllComponentIds = products.SelectMany(p => p.ComponentIds).Distinct().ToList(),
                    TotalChangeoverCount = products.Count - 1,
                    TotalChangeoverTimeMinutes = (products.Count - 1) * manualGroup.InternalChangeoverMinutes
                };

                // Tính feeder slots
                productGroup.TotalFeederSlots = _groupingService.CalculateTotalFeederSlots(products);

                result.Add(productGroup);
            }

            // Thêm sản phẩm không thuộc nhóm nào
            var ungroupedProducts = _products
                .Where(p => string.IsNullOrEmpty(p.GroupId) && p.RequiredQuantity > 0)
                .ToList();

            if (ungroupedProducts.Any())
            {
                // Nếu có EnableGrouping, tự động group các sản phẩm còn lại
                if (EnableGrouping)
                {
                    var autoGroups = _groupingService.GroupProductsByComponents(
                        ungroupedProducts, MinSimilarityPercent);
                    
                    foreach (var g in autoGroups)
                    {
                        g.GroupId = $"AUTO-{g.GroupId}";
                        g.GroupName = $"[Auto] {g.GroupName}";
                    }
                    result.AddRange(autoGroups);
                }
                else
                {
                    // Mỗi sản phẩm là 1 nhóm riêng
                    int idx = 1;
                    foreach (var p in ungroupedProducts.OrderBy(p => p.DueDate))
                    {
                        result.Add(new ProductGroup
                        {
                            GroupId = $"SINGLE-{idx}",
                            GroupName = p.Name,
                            Products = new List<Product> { p },
                            AllComponentIds = p.ComponentIds.ToList()
                        });
                        idx++;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Stage Naming Methods

        /// <summary>
        /// Đặt tên model cho một sản phẩm tại một công đoạn
        /// </summary>
        public void SetProductStageName(string productId, int stageId, string stageName)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product != null)
            {
                product.SetStageName(stageId, stageName);
            }
        }

        /// <summary>
        /// Đặt pattern đổi tên cho một sản phẩm
        /// Pattern hỗ trợ: {Name}, {Id}, {StageId}, {StageOrder}
        /// VD: "{Name}-S{StageOrder}" → "PCB-A-S1", "PCB-A-S2"
        /// </summary>
        public void SetProductStageNamePattern(string productId, string pattern)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product != null)
            {
                product.StageNamePattern = pattern;
            }
        }

        /// <summary>
        /// Đặt pattern đổi tên cho tất cả sản phẩm
        /// </summary>
        public void SetGlobalStageNamePattern(string pattern)
        {
            foreach (var product in _products)
            {
                product.StageNamePattern = pattern;
            }
        }

        /// <summary>
        /// Lấy tên model của sản phẩm tại một công đoạn
        /// </summary>
        public string GetProductNameAtStage(string productId, int stageId)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null) return productId;

            var stage = _stages.FirstOrDefault(s => s.Id == stageId);
            int stageOrder = stage?.Order ?? 0;

            return product.GetNameAtStage(stageId, stageOrder);
        }

        #endregion

        /// <summary>
        /// Constructor với Operators (không có transfer matrix)
        /// </summary>
        public SMTSchedulerService(
            List<Stage> stages,
            List<Line> lines,
            List<Product> products,
            List<Component> components,
            List<Operator> operators,
            DateTime referenceDate,
            WorkingCalendar calendar)
            : this(stages, lines, products, components, operators, referenceDate, calendar, null)
        {
        }

        /// <summary>
        /// Constructor với WorkingCalendar (không có operators)
        /// </summary>
        public SMTSchedulerService(
            List<Stage> stages,
            List<Line> lines,
            List<Product> products,
            List<Component> components,
            DateTime referenceDate,
            WorkingCalendar calendar)
            : this(stages, lines, products, components, new List<Operator>(), referenceDate, calendar, null)
        {
        }

        /// <summary>
        /// Constructor đơn giản
        /// </summary>
        public SMTSchedulerService(
            List<Stage> stages,
            List<Line> lines,
            List<Product> products,
            List<Component> components,
            DateTime referenceDate,
            int workingMinutesPerDay = 480)
            : this(stages, lines, products, components, new List<Operator>(), referenceDate, 
                   CreateDefaultCalendar(referenceDate, workingMinutesPerDay), null)
        {
        }

        private static WorkingCalendar CreateDefaultCalendar(DateTime referenceDate, int workingMinutesPerDay = 480)
        {
            var calendar = new WorkingCalendar();
            int hours = workingMinutesPerDay / 60;
            int minutes = workingMinutesPerDay % 60;
            
            calendar.DefaultShift = new WorkShift("Ca ngày",
                new TimeSpan(8, 0, 0),
                new TimeSpan(8 + hours, minutes, 0),
                60);

            calendar.AddWeekendsAsHolidays(referenceDate, referenceDate.AddMonths(3));
            return calendar;
        }

        public WorkingCalendar Calendar => _calendar;
        public List<Operator> Operators => _operators;
        public TransferTimeMatrix TransferMatrix => _transferMatrix;

        /// <summary>
        /// Cấu hình Lot Split và Priority cho một sản phẩm
        /// </summary>
        public void SetProductConfig(string productId, LotSplitConfig lotConfig, PriorityConfig priorityConfig = null)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null) return;

            _productScheduleInfos[productId] = new ProductScheduleInfo(
                product,
                lotConfig ?? DefaultLotConfig,
                priorityConfig ?? PriorityConfig.Normal
            );
        }

        /// <summary>
        /// Cấu hình Lot Split cho một sản phẩm
        /// </summary>
        public void SetLotConfig(string productId, LotSplitConfig config)
        {
            SetProductConfig(productId, config, null);
        }

        /// <summary>
        /// Cấu hình Priority cho một sản phẩm
        /// </summary>
        public void SetPriority(string productId, ProductionPriority priority, int subPriority = 100)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null) return;

            if (!_productScheduleInfos.TryGetValue(productId, out var info))
            {
                info = new ProductScheduleInfo(product);
                _productScheduleInfos[productId] = info;
            }

            info.PriorityConfig = new PriorityConfig
            {
                Priority = priority,
                SubPriority = subPriority
            };
        }

        /// <summary>
        /// Đặt sản phẩm là VIP
        /// </summary>
        public void SetVIPProduct(string productId, ProductionPriority priority = ProductionPriority.High)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null) return;

            if (!_productScheduleInfos.TryGetValue(productId, out var info))
            {
                info = new ProductScheduleInfo(product);
                _productScheduleInfos[productId] = info;
            }

            info.PriorityConfig = PriorityConfig.VIP(priority);
        }

        /// <summary>
        /// Lấy ProductScheduleInfo cho một sản phẩm
        /// </summary>
        public ProductScheduleInfo GetProductInfo(string productId)
        {
            if (_productScheduleInfos.TryGetValue(productId, out var info))
                return info;

            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product != null)
            {
                info = new ProductScheduleInfo(product, DefaultLotConfig, PriorityConfig.Normal);
                _productScheduleInfos[productId] = info;
                return info;
            }

            return null;
        }

        private void CalculateProductFeederSlots()
        {
            var componentDict = _components.ToDictionary(c => c.Id);
            foreach (var product in _products)
            {
                product.TotalFeederSlotsRequired = product.ComponentIds.Sum(id =>
                {
                    if (componentDict.TryGetValue(id, out var comp))
                        return comp.FeederSlots;
                    return 1;
                });
            }
        }

        /// <summary>
        /// Thực hiện lập lịch sản xuất
        /// </summary>
        public ScheduleResult Solve(int timeLimitSeconds = 60)
        {
            var result = new ScheduleResult();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Bước 1: Kiểm tra dữ liệu
                var validationErrors = ValidateInput();
                if (validationErrors.Any())
                {
                    result.IsSuccess = false;
                    result.Status = "INVALID_INPUT";
                    result.FailureReasons = validationErrors;
                    return result;
                }

                // Bước 2: Lọc sản phẩm cần sản xuất
                var productsToSchedule = _products.Where(p => p.RequiredQuantity > 0).ToList();
                if (!productsToSchedule.Any())
                {
                    result.IsSuccess = true;
                    result.Status = "NO_PRODUCTION_NEEDED";
                    result.Warnings.Add("Tất cả sản phẩm đã đủ tồn kho.");
                    return result;
                }

                // Bước 2.5: Khởi tạo ProductScheduleInfo cho tất cả sản phẩm
                foreach (var product in productsToSchedule)
                {
                    if (!_productScheduleInfos.ContainsKey(product.Id))
                    {
                        _productScheduleInfos[product.Id] = new ProductScheduleInfo(
                            product, DefaultLotConfig, PriorityConfig.Normal);
                    }
                }

                // Bước 2.6: Sắp xếp theo Priority nếu được bật
                if (EnablePriorityScheduling)
                {
                    var sortedInfos = ProductPriorityComparer.SortByPriority(
                        _productScheduleInfos.Values.Where(p => p.Product.RequiredQuantity > 0).ToList(),
                        _referenceDate);
                    productsToSchedule = sortedInfos.Select(s => s.Product).ToList();

                    // Log priority info
                    result.Warnings.Add($"Đã sắp xếp {productsToSchedule.Count} sản phẩm theo mức độ ưu tiên");
                }

                // Bước 2.7: Chia Lot thành Batches nếu được bật
                List<ProductionBatch> allBatches = null;
                if (EnableLotSplitting)
                {
                    allBatches = new List<ProductionBatch>();
                    foreach (var product in productsToSchedule)
                    {
                        var info = _productScheduleInfos[product.Id];
                        info.SplitIntoBatches();
                        allBatches.AddRange(info.Batches);

                        if (info.IsSplit)
                        {
                            result.Warnings.Add($"Sản phẩm '{product.Name}' được chia thành {info.Batches.Count} batch");
                        }
                    }
                }

                // Bước 3: Phân tích năng lực
                result.CapacityAnalyses = AnalyzeCapacity(productsToSchedule);
                foreach (var bn in result.CapacityAnalyses.Where(c => c.IsBottleneck))
                {
                    result.Warnings.Add($"Công đoạn '{bn.StageName}' đang quá tải ({bn.UtilizationPercent:F1}%)");
                }

                // Bước 4: Group sản phẩm 
                // - UseManualGrouping = true: Sử dụng nhóm do người dùng định nghĩa
                // - EnableComponentGrouping = true: Tự động group theo linh kiện (cần có Component data)
                // - Nếu cả 2 đều false: Không grouping
                List<ProductGroup> groups = null;
                
                if (UseManualGrouping && _manualGroups.Any())
                {
                    // Sử dụng Manual Groups
                    groups = ConvertManualGroupsToProductGroups();
                    result.ProductGroups = groups;
                    productsToSchedule = groups.SelectMany(g => g.Products).ToList();
                    result.Warnings.Add($"Sử dụng {_manualGroups.Count} nhóm sản phẩm do người dùng định nghĩa");
                }
                else if (EnableComponentGrouping && _components.Any() && productsToSchedule.Any(p => p.ComponentIds.Any()))
                {
                    // Tự động group theo linh kiện (chỉ khi có Component data)
                    groups = _groupingService.GroupProductsByComponents(productsToSchedule, MinSimilarityPercent);
                    result.ProductGroups = groups;
                    productsToSchedule = groups.SelectMany(g => g.Products).ToList();
                    result.Warnings.Add($"Đã tự động group {productsToSchedule.Count} sản phẩm thành {groups.Count} nhóm theo linh kiện");
                }
                // Nếu không có grouping, giữ nguyên productsToSchedule

                // Bước 5: Tính horizon
                int horizon = CalculateHorizon(productsToSchedule);
                result.PlanStartDate = _referenceDate;

                // Bước 6: Giải với CP-SAT (có hỗ trợ lot splitting)
                var solveResult = SolveWithCpSat(productsToSchedule, groups, allBatches, horizon, timeLimitSeconds);
                result.IsSuccess = solveResult.IsSuccess;
                result.Status = solveResult.Status;
                result.Tasks = solveResult.Tasks;
                result.MakespanMinutes = solveResult.MakespanMinutes;
                result.FailureReasons.AddRange(solveResult.FailureReasons);

                if (result.IsSuccess && result.Tasks.Any())
                {
                    ConvertToActualDates(result);
                    result.ExpectedCompletionDate = result.Tasks.Max(t => t.ActualEndDate);
                    CheckDeadlines(result, productsToSchedule);
                    CalculateStatistics(result, horizon);
                }

                if (!result.IsSuccess)
                {
                    AnalyzeFailure(result, productsToSchedule);
                }

                // Bước 7: Tạo gợi ý nếu cần
                if (EnableSuggestions && 
                    (!result.IsSuccess || result.MissedDeadlines.Any() || 
                     result.CapacityAnalyses.Any(c => c.IsBottleneck)))
                {
                    result.SuggestionReport = _suggestionService.AnalyzeAndSuggest(
                        productsToSchedule, result, _referenceDate);
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Status = "ERROR";
                result.FailureReasons.Add($"Lỗi hệ thống: {ex.Message}");
            }
            finally
            {
                watch.Stop();
                result.SolveTimeMs = watch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Giải bài toán với CP-SAT Solver - CÓ TRANSFER TIME và LOT SPLITTING
        /// </summary>
        private ScheduleResult SolveWithCpSat(List<Product> products, List<ProductGroup> groups,
            List<ProductionBatch> batches, int horizon, int timeLimitSeconds)
        {
            var result = new ScheduleResult();
            CpModel model = new CpModel();

            // Xác định items để schedule (batches nếu có, ngược lại dùng products)
            bool useBatches = EnableLotSplitting && batches != null && batches.Any(b => b.TotalBatches > 1);
            
            // Tạo danh sách schedule items
            var scheduleItems = new List<ScheduleItem>();
            
            // Kiểm tra có Stage Lot Splitting không
            bool hasStageLoSplitting = products.Any(p => p.StageLotConfigs != null && p.StageLotConfigs.Any());
            
            if (hasStageLoSplitting)
            {
                // === STAGE LOT SPLITTING MODE ===
                // Mỗi công đoạn có thể có batch size khác nhau
                foreach (var product in products)
                {
                    // Lấy routing để biết product đi qua những stage nào
                    var routing = EnableCustomRouting ? _routingManager.GetRouting(product.Id) : null;
                    var productStages = (EnableCustomRouting && routing != null && routing.Steps.Any())
                        ? routing.GetStageSequence()
                        : _stages.Select(s => s.Id).ToList();

                    foreach (var stageId in productStages)
                    {
                        // Lấy LotConfig cho stage này
                        var stageConfig = product.GetLotConfigForStage(stageId);
                        var stageBatches = stageConfig.CalculateBatches(product.RequiredQuantity);

                        for (int i = 0; i < stageBatches.Count; i++)
                        {
                            string itemId = stageBatches.Count > 1
                                ? $"{product.Id}_S{stageId}_B{i + 1}"
                                : $"{product.Id}_S{stageId}";

                            scheduleItems.Add(new ScheduleItem
                            {
                                Id = itemId,
                                ProductId = product.Id,
                                ProductName = product.Name,
                                Quantity = stageBatches[i],
                                StartDate = product.StartDate,
                                DueDate = product.DueDate,
                                StageId = stageId,
                                StageBatchNumber = i + 1,
                                StageTotalBatches = stageBatches.Count,
                                BatchNumber = i + 1,
                                TotalBatches = stageBatches.Count,
                                PreviousBatchId = i > 0 ? $"{product.Id}_S{stageId}_B{i}" : null,
                                NextBatchId = i < stageBatches.Count - 1 ? $"{product.Id}_S{stageId}_B{i + 2}" : null,
                                MinGapAfterPreviousBatch = stageConfig.MinGapBetweenBatches,
                                ComponentIds = product.ComponentIds
                            });
                        }
                    }
                }
            }
            else if (useBatches)
            {
                // === PRODUCT LOT SPLITTING MODE (legacy) ===
                // Batch chung cho cả product
                foreach (var batch in batches)
                {
                    var product = products.First(p => p.Id == batch.ProductId);
                    scheduleItems.Add(new ScheduleItem
                    {
                        Id = batch.BatchId,
                        ProductId = batch.ProductId,
                        ProductName = batch.ProductName,
                        Quantity = batch.Quantity,
                        StartDate = batch.StartDate,
                        DueDate = batch.DueDate,
                        BatchNumber = batch.BatchNumber,
                        TotalBatches = batch.TotalBatches,
                        PreviousBatchId = batch.PreviousBatchId,
                        NextBatchId = batch.NextBatchId,
                        MinGapAfterPreviousBatch = batch.MinGapAfterPreviousBatch,
                        ComponentIds = product.ComponentIds
                    });
                }
            }
            else
            {
                // === NO SPLITTING MODE ===
                foreach (var product in products)
                {
                    scheduleItems.Add(new ScheduleItem
                    {
                        Id = product.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = product.RequiredQuantity,
                        StartDate = product.StartDate,
                        DueDate = product.DueDate,
                        BatchNumber = 1,
                        TotalBatches = 1,
                        ComponentIds = product.ComponentIds
                    });
                }
            }

            // Cấu trúc lưu biến
            var taskVars = new Dictionary<(string itemId, int stageId, string lineId), TaskVars>();
            var stageEndTimes = new Dictionary<(string itemId, int stageId), IntVar>();
            var stageLineAssignment = new Dictionary<(string itemId, int stageId), Dictionary<string, BoolVar>>();
            var lineIntervals = new Dictionary<string, List<IntervalVar>>();

            foreach (var line in _lines)
            {
                lineIntervals[line.Id] = new List<IntervalVar>();
            }

            // Dictionary để track batch order (cho Stage Lot Splitting)
            var batchStageEndTimes = new Dictionary<(string productId, int batchNum, int stageId), IntVar>();
            
            // Track previous stage batch end times cho Stage Lot Splitting
            var stageBatchEndTimes = new Dictionary<(string productId, int stageId, int batchNum), IntVar>();

            // ===== TẠO BIẾN CHO MỖI SCHEDULE ITEM =====
            foreach (var item in scheduleItems)
            {
                int startTimeMin = ConvertDateToMinutes(item.StartDate);

                // Lấy routing cho sản phẩm này
                var routing = EnableCustomRouting 
                    ? _routingManager.GetRouting(item.ProductId) 
                    : null;
                
                // Xác định stages cần xử lý
                List<RouteStep> itemStages;
                if (item.StageId.HasValue)
                {
                    // Stage Lot Splitting: Item chỉ cho 1 stage cụ thể
                    var stage = _stages.FirstOrDefault(s => s.Id == item.StageId.Value);
                    if (stage == null) continue;
                    itemStages = new List<RouteStep> 
                    { 
                        new RouteStep { StageId = stage.Id, StageName = stage.Name, Sequence = stage.Order } 
                    };
                }
                else
                {
                    // Legacy mode: Item đi qua tất cả stages trong routing
                    itemStages = (EnableCustomRouting && routing != null && routing.Steps.Any())
                        ? routing.Steps.OrderBy(s => s.Sequence).ToList()
                        : _stages.Select(s => new RouteStep { StageId = s.Id, StageName = s.Name, Sequence = s.Order }).ToList();
                }

                RouteStep previousStep = null;

                foreach (var routeStep in itemStages)
                {
                    var stage = _stages.FirstOrDefault(s => s.Id == routeStep.StageId);
                    if (stage == null) continue;

                    var stageEnd = model.NewIntVar(0, horizon, $"stageEnd_{item.Id}_{stage.Id}");
                    stageEndTimes[(item.Id, stage.Id)] = stageEnd;
                    
                    // Track cho batch ordering
                    batchStageEndTimes[(item.ProductId, item.BatchNumber, stage.Id)] = stageEnd;
                    
                    // Track cho Stage Lot Splitting
                    if (item.StageId.HasValue)
                    {
                        stageBatchEndTimes[(item.ProductId, stage.Id, item.StageBatchNumber)] = stageEnd;
                    }

                    var assignedVars = new List<BoolVar>();
                    var lineEndVars = new List<(BoolVar, IntVar)>();
                    var lineAssignmentDict = new Dictionary<string, BoolVar>();

                    foreach (var line in _lines)
                    {
                        if (!line.SupportsStage(stage.Id))
                            continue;

                        // Kiểm tra Line có được phép cho route step này không
                        if (!routeStep.IsLineAllowed(line.Id))
                            continue;

                        // Tính processing time - sử dụng leadtime từ routing nếu có
                        int processingTime;
                        if (EnableCustomRouting && routing != null)
                        {
                            processingTime = (int)Math.Ceiling(
                                routing.CalculateTotalProcessingTime(stage.Id, item.Quantity, line));
                        }
                        else
                        {
                            processingTime = (int)Math.Ceiling(
                                line.CalculateProcessingTime(stage.Id, item.Quantity));
                        }

                        var isAssigned = model.NewBoolVar($"assigned_{item.Id}_{stage.Id}_{line.Id}");
                        assignedVars.Add(isAssigned);
                        lineAssignmentDict[line.Id] = isAssigned;

                        var start = model.NewIntVar(0, horizon, $"start_{item.Id}_{stage.Id}_{line.Id}");
                        var end = model.NewIntVar(0, horizon, $"end_{item.Id}_{stage.Id}_{line.Id}");

                        var interval = model.NewOptionalIntervalVar(
                            start, processingTime, end, isAssigned,
                            $"interval_{item.Id}_{stage.Id}_{line.Id}");

                        taskVars[(item.Id, stage.Id, line.Id)] = new TaskVars
                        {
                            Start = start,
                            End = end,
                            Interval = interval,
                            IsAssigned = isAssigned,
                            ProcessingTime = processingTime
                        };

                        lineIntervals[line.Id].Add(interval);
                        lineEndVars.Add((isAssigned, end));

                        // Ràng buộc: bắt đầu sau StartDate
                        model.Add(start >= startTimeMin).OnlyEnforceIf(isAssigned);

                        // ===== RÀNG BUỘC TRANSFER TIME (giữa công đoạn và giữa Line) =====
                        if (previousStep != null)
                        {
                            var prevStageId = previousStep.StageId;
                            var prevEnd = stageEndTimes[(item.Id, prevStageId)];

                            // Tính Stage Transfer Time (thời gian chuyển giữa công đoạn)
                            int stageTransferTime = 0;
                            if (EnableStageTransferTime && _stageTransferMatrix != null)
                            {
                                stageTransferTime = _stageTransferMatrix.GetTransferTime(prevStageId, stage.Id);
                            }

                            if (EnableTransferTime && stageLineAssignment.TryGetValue((item.Id, prevStageId), out var prevAssignments))
                            {
                                foreach (var prevLine in _lines.Where(l => l.SupportsStage(prevStageId)))
                                {
                                    if (!prevAssignments.ContainsKey(prevLine.Id))
                                        continue;

                                    var prevAssigned = prevAssignments[prevLine.Id];
                                    
                                    // Line Transfer Time (thời gian di chuyển giữa Line)
                                    int lineTransferTime = _transferMatrix.GetTransferTime(prevLine.Id, line.Id);
                                    
                                    // Tổng thời gian transfer = Stage Transfer + Line Transfer
                                    int totalTransferTime = stageTransferTime + lineTransferTime;

                                    var bothAssigned = model.NewBoolVar(
                                        $"both_{item.Id}_{prevStageId}_{prevLine.Id}_{stage.Id}_{line.Id}");
                                    
                                    model.AddBoolAnd(new[] { prevAssigned, isAssigned }).OnlyEnforceIf(bothAssigned);
                                    model.AddBoolOr(new[] { prevAssigned.Not(), isAssigned.Not() }).OnlyEnforceIf(bothAssigned.Not());

                                    model.Add(start >= prevEnd + totalTransferTime).OnlyEnforceIf(bothAssigned);
                                }
                            }
                            else
                            {
                                // Chỉ có Stage Transfer Time
                                model.Add(start >= prevEnd + stageTransferTime).OnlyEnforceIf(isAssigned);
                            }
                        }

                        // ===== RÀNG BUỘC LOT SPLITTING - Batch order =====
                        if (item.StageId.HasValue)
                        {
                            // === STAGE LOT SPLITTING MODE ===
                            var product = products.First(p => p.Id == item.ProductId);
                            
                            // Lấy routing để biết stage trước là gì
                            var productRouting = EnableCustomRouting ? _routingManager.GetRouting(item.ProductId) : null;
                            var productStages = (EnableCustomRouting && productRouting != null && productRouting.Steps.Any())
                                ? productRouting.GetStageSequence()
                                : _stages.Select(s => s.Id).ToList();
                            
                            int currentStageIndex = productStages.IndexOf(stage.Id);
                            int? prevStageId = currentStageIndex > 0 ? productStages[currentStageIndex - 1] : (int?)null;
                            
                            // 1. Batch sau của cùng stage phải đợi batch trước
                            if (item.StageBatchNumber > 1)
                            {
                                var prevBatchKey = (item.ProductId, stage.Id, item.StageBatchNumber - 1);
                                if (stageBatchEndTimes.TryGetValue(prevBatchKey, out var prevBatchEnd))
                                {
                                    model.Add(start >= prevBatchEnd + item.MinGapAfterPreviousBatch).OnlyEnforceIf(isAssigned);
                                }
                            }
                            
                            // 2. PIPELINE: Batch N của stage hiện tại đợi Batch N của stage trước
                            if (prevStageId.HasValue)
                            {
                                // Lấy số batch của stage trước
                                var prevStageConfig = product.GetLotConfigForStage(prevStageId.Value);
                                var prevStageBatches = prevStageConfig.CalculateBatches(product.RequiredQuantity);
                                int prevStageTotalBatches = prevStageBatches.Count;
                                
                                // Batch tương ứng ở stage trước (hoặc batch cuối nếu stage trước có ít batch hơn)
                                int correspondingBatchNum = Math.Min(item.StageBatchNumber, prevStageTotalBatches);
                                
                                // Tìm item ID của batch tương ứng ở stage trước
                                string prevBatchItemId = prevStageTotalBatches > 1
                                    ? $"{item.ProductId}_S{prevStageId.Value}_B{correspondingBatchNum}"
                                    : $"{item.ProductId}_S{prevStageId.Value}";
                                
                                // Tính Stage Transfer Time
                                int stageTransfer = 0;
                                if (EnableStageTransferTime && _stageTransferMatrix != null)
                                {
                                    stageTransfer = _stageTransferMatrix.GetTransferTime(prevStageId.Value, stage.Id);
                                }
                                
                                // Tính Line Transfer Time (cần biết batch trước được assign vào line nào)
                                if (EnableTransferTime && stageLineAssignment.TryGetValue((prevBatchItemId, prevStageId.Value), out var prevAssignments))
                                {
                                    foreach (var prevLine in _lines.Where(l => l.SupportsStage(prevStageId.Value)))
                                    {
                                        if (!prevAssignments.ContainsKey(prevLine.Id))
                                            continue;

                                        var prevAssigned = prevAssignments[prevLine.Id];
                                        
                                        // Line Transfer Time
                                        int lineTransferTime = _transferMatrix.GetTransferTime(prevLine.Id, line.Id);
                                        
                                        // Tổng thời gian transfer = Stage Transfer + Line Transfer
                                        int totalTransferTime = stageTransfer + lineTransferTime;

                                        // Lấy end time của batch trước
                                        if (stageEndTimes.TryGetValue((prevBatchItemId, prevStageId.Value), out var prevBatchEnd))
                                        {
                                            var bothAssigned = model.NewBoolVar(
                                                $"pipeline_{item.Id}_{prevBatchItemId}_{prevLine.Id}_{line.Id}");
                                            
                                            model.AddBoolAnd(new[] { prevAssigned, isAssigned }).OnlyEnforceIf(bothAssigned);
                                            model.AddBoolOr(new[] { prevAssigned.Not(), isAssigned.Not() }).OnlyEnforceIf(bothAssigned.Not());

                                            model.Add(start >= prevBatchEnd + totalTransferTime).OnlyEnforceIf(bothAssigned);
                                        }
                                    }
                                }
                                else
                                {
                                    // Không có Line Transfer, chỉ Stage Transfer
                                    var correspondingBatchKey = (item.ProductId, prevStageId.Value, correspondingBatchNum);
                                    if (stageBatchEndTimes.TryGetValue(correspondingBatchKey, out var correspondingBatchEnd))
                                    {
                                        model.Add(start >= correspondingBatchEnd + stageTransfer).OnlyEnforceIf(isAssigned);
                                    }
                                }
                            }
                        }
                        else if (useBatches && item.BatchNumber > 1 && item.PreviousBatchId != null)
                        {
                            // === LEGACY LOT SPLITTING MODE ===
                            // Batch N của công đoạn S+1 phải đợi batch N của công đoạn S hoàn thành
                            var prevBatchKey = (item.ProductId, item.BatchNumber - 1, stage.Id);
                            if (batchStageEndTimes.TryGetValue(prevBatchKey, out var prevBatchEnd))
                            {
                                model.Add(start >= prevBatchEnd + item.MinGapAfterPreviousBatch).OnlyEnforceIf(isAssigned);
                            }
                        }
                    }

                    stageLineAssignment[(item.Id, stage.Id)] = lineAssignmentDict;

                    if (!assignedVars.Any())
                    {
                        result.IsSuccess = false;
                        result.Status = "INFEASIBLE";
                        result.FailureReasons.Add(
                            $"Không có Line nào hỗ trợ công đoạn '{stage.Name}' cho '{item.ProductName}'");
                        return result;
                    }

                    model.Add(LinearExpr.Sum(assignedVars) == 1);

                    foreach (var (assigned, end) in lineEndVars)
                    {
                        model.Add(stageEnd >= end).OnlyEnforceIf(assigned);
                        model.Add(stageEnd <= end).OnlyEnforceIf(assigned);
                    }

                    // Lưu step hiện tại làm previousStep cho bước tiếp theo
                    previousStep = routeStep;
                }

                // Hard deadline constraint - sử dụng stage cuối cùng trong routing
                if (UseHardDeadlineConstraint && !item.StageId.HasValue)
                {
                    // Chỉ áp dụng cho legacy mode (không có Stage Lot Splitting)
                    var lastStageId = (EnableCustomRouting && routing != null && routing.Steps.Any())
                        ? routing.Steps.OrderByDescending(s => s.Sequence).First().StageId
                        : _stages.Last().Id;
                    
                    var deadline = ConvertDateToMinutes(item.DueDate);
                    if (stageEndTimes.ContainsKey((item.Id, lastStageId)))
                    {
                        model.Add(stageEndTimes[(item.Id, lastStageId)] <= deadline);
                    }
                }
            }

            // ===== RÀNG BUỘC NO OVERLAP =====
            foreach (var kvp in lineIntervals)
            {
                if (kvp.Value.Count > 1)
                {
                    model.AddNoOverlap(kvp.Value);
                }
            }

            // ===== MỤC TIÊU: MINIMIZE MAKESPAN với PRIORITY WEIGHTING =====
            var makespan = model.NewIntVar(0, horizon, "makespan");
            var allEndTimes = new List<IntVar>();

            foreach (var item in scheduleItems)
            {
                // Chỉ tính batch cuối của mỗi product
                if (item.BatchNumber == item.TotalBatches)
                {
                    // Lấy stage cuối cùng từ routing của product
                    var routing = EnableCustomRouting ? _routingManager.GetRouting(item.ProductId) : null;
                    var lastStageId = (EnableCustomRouting && routing != null && routing.Steps.Any())
                        ? routing.Steps.OrderByDescending(s => s.Sequence).First().StageId
                        : _stages.Last().Id;

                    if (stageEndTimes.ContainsKey((item.Id, lastStageId)))
                    {
                        allEndTimes.Add(stageEndTimes[(item.Id, lastStageId)]);
                    }
                }
            }

            if (!allEndTimes.Any())
            {
                result.IsSuccess = false;
                result.Status = "INFEASIBLE";
                result.FailureReasons.Add("Không có task nào được tạo");
                return result;
            }

            model.AddMaxEquality(makespan, allEndTimes);
            model.Minimize(makespan);

            // ===== GIẢI =====
            CpSolver solver = new CpSolver();
            solver.StringParameters = $"max_time_in_seconds:{timeLimitSeconds}";

            CpSolverStatus status = solver.Solve(model);

            // ===== XỬ LÝ KẾT QUẢ =====
            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                result.IsSuccess = true;
                result.Status = status == CpSolverStatus.Optimal ? "OPTIMAL" : "FEASIBLE";
                result.MakespanMinutes = solver.Value(makespan);

                var previousProductOnLine = new Dictionary<string, string>();
                var tasks = new List<ScheduleTask>();

                // Kiểm tra có Stage Lot Splitting không
                bool isStageLoSplitting = products.Any(p => p.StageLotConfigs != null && p.StageLotConfigs.Any());

                foreach (var item in scheduleItems)
                {
                    string previousLineId = null;
                    int previousStageId = 0;

                    // Xác định stages cần xử lý
                    List<int> itemStageIds;
                    if (item.StageId.HasValue)
                    {
                        // Stage Lot Splitting: Chỉ 1 stage
                        itemStageIds = new List<int> { item.StageId.Value };
                    }
                    else
                    {
                        // Legacy mode
                        var routing = EnableCustomRouting ? _routingManager.GetRouting(item.ProductId) : null;
                        itemStageIds = (EnableCustomRouting && routing != null && routing.Steps.Any())
                            ? routing.GetStageSequence()
                            : _stages.Select(s => s.Id).ToList();
                    }

                    foreach (var stageId in itemStageIds)
                    {
                        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
                        if (stage == null) continue;

                        foreach (var line in _lines)
                        {
                            var key = (item.Id, stage.Id, line.Id);
                            if (!taskVars.ContainsKey(key)) continue;

                            var vars = taskVars[key];
                            if (solver.Value(vars.IsAssigned) != 1) continue;

                            // Line Transfer Time
                            int lineTransferTime = 0;
                            if (EnableTransferTime && previousLineId != null && previousLineId != line.Id)
                            {
                                lineTransferTime = _transferMatrix.GetTransferTime(previousLineId, line.Id);
                            }

                            // Stage Transfer Time
                            int stageTransferTime = 0;
                            int prevStageId = 0;
                            if (EnableStageTransferTime && previousStageId > 0 && previousStageId != stage.Id)
                            {
                                stageTransferTime = _stageTransferMatrix.GetTransferTime(previousStageId, stage.Id);
                                prevStageId = previousStageId;
                            }

                            string lineStageKey = $"{line.Id}_{stage.Id}";
                            string prevProductId = null;
                            previousProductOnLine.TryGetValue(lineStageKey, out prevProductId);

                            var origProduct = products.First(p => p.Id == item.ProductId);
                            var prevProduct = products.FirstOrDefault(p => p.Id == prevProductId);
                            
                            // Tính changeover - chỉ khi bật Component Grouping
                            List<string> changeoverComponents = new List<string>();
                            double changeoverTime = 0;
                            if (EnableComponentGrouping && _components.Any())
                            {
                                changeoverComponents = origProduct.GetChangeoverComponents(prevProduct);
                                changeoverTime = _groupingService.CalculateChangeoverTime(prevProduct, origProduct);
                            }

                            // Lấy tên model tại công đoạn này
                            string modelNameAtStage = EnableStageNaming 
                                ? origProduct.GetNameAtStage(stage.Id, stage.Order)
                                : origProduct.Name;

                            // Lấy thông tin Group
                            string groupId = origProduct.GroupId;
                            string groupName = null;
                            if (!string.IsNullOrEmpty(groupId))
                            {
                                var manualGroup = _manualGroups.FirstOrDefault(g => g.GroupId == groupId);
                                groupName = manualGroup?.GroupName ?? groupId;
                            }

                            // Xác định batch info
                            int batchNumber = item.StageId.HasValue ? item.StageBatchNumber : item.BatchNumber;
                            int totalBatches = item.StageId.HasValue ? item.StageTotalBatches : item.TotalBatches;
                            string batchId = item.StageId.HasValue || isStageLoSplitting ? item.Id : (useBatches ? item.Id : null);

                            var task = new ScheduleTask
                            {
                                ProductId = item.ProductId,
                                ProductName = item.ProductName,
                                ModelNameAtStage = modelNameAtStage,
                                GroupId = groupId,
                                GroupName = groupName,
                                StageId = stage.Id,
                                StageOrder = stage.Order,
                                StageName = stage.Name,
                                LineId = line.Id,
                                LineName = line.Name,
                                Quantity = item.Quantity,
                                StartTimeMinutes = solver.Value(vars.Start),
                                EndTimeMinutes = solver.Value(vars.End),
                                ProcessingTimeMinutes = vars.ProcessingTime,
                                SetupTimeMinutes = (long)changeoverTime,
                                TransferTimeMinutes = lineTransferTime,
                                StageTransferTimeMinutes = stageTransferTime,
                                PreviousLineId = previousLineId,
                                PreviousStageId = prevStageId,
                                ChangeoverComponents = changeoverComponents,
                                PreviousProductId = prevProductId,
                                BatchId = batchId,
                                BatchNumber = batchNumber,
                                TotalBatches = totalBatches
                            };

                            tasks.Add(task);
                            previousProductOnLine[lineStageKey] = item.ProductId;
                            previousLineId = line.Id;
                            previousStageId = stage.Id;
                        }
                    }
                }

                result.Tasks = tasks.OrderBy(t => t.StartTimeMinutes).ThenBy(t => t.LineId).ToList();
            }
            else
            {
                result.IsSuccess = false;
                result.Status = status.ToString();

                if (status == CpSolverStatus.Infeasible)
                {
                    result.FailureReasons.Add(
                        "Không tìm được lịch trình khả thi. Deadline quá chặt hoặc năng lực không đủ.");
                }
            }

            return result;
        }

        /// <summary>
        /// Internal class để represent item cần schedule (có thể là Product hoặc Batch)
        /// </summary>
        private class ScheduleItem
        {
            public string Id { get; set; }
            public string ProductId { get; set; }
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime DueDate { get; set; }
            public int BatchNumber { get; set; }
            public int TotalBatches { get; set; }
            public string PreviousBatchId { get; set; }
            public string NextBatchId { get; set; }
            public int MinGapAfterPreviousBatch { get; set; }
            public List<string> ComponentIds { get; set; } = new List<string>();
            
            // Hỗ trợ Stage Lot Splitting
            public int? StageId { get; set; }  // null = áp dụng cho tất cả stages
            public int StageBatchNumber { get; set; } = 1;
            public int StageTotalBatches { get; set; } = 1;
        }

        #region Helper Methods

        private List<string> ValidateInput()
        {
            var errors = new List<string>();

            if (!_stages.Any())
                errors.Add("Chưa có công đoạn nào được định nghĩa.");

            if (!_lines.Any())
                errors.Add("Chưa có Line nào được định nghĩa hoặc tất cả đều inactive.");

            if (!_products.Any())
                errors.Add("Chưa có sản phẩm nào cần sản xuất.");

            foreach (var stage in _stages)
            {
                if (!_lines.Any(l => l.SupportsStage(stage.Id)))
                    errors.Add($"Công đoạn '{stage.Name}' không có Line nào hỗ trợ.");
            }

            foreach (var product in _products.Where(p => p.RequiredQuantity > 0))
            {
                if (product.DueDate <= product.StartDate)
                    errors.Add($"Sản phẩm '{product.Name}': Deadline phải sau ngày bắt đầu.");

                int workingDays = _calendar.GetWorkingDayCount(product.StartDate, product.DueDate);
                if (workingDays == 0)
                    errors.Add($"Sản phẩm '{product.Name}': Không có ngày làm việc từ {product.StartDate:dd/MM} đến {product.DueDate:dd/MM}.");
            }

            return errors;
        }

        private List<CapacityAnalysis> AnalyzeCapacity(List<Product> products)
        {
            var analyses = new List<CapacityAnalysis>();
            var maxDeadline = products.Max(p => p.DueDate);
            var minStart = products.Min(p => p.StartDate);

            foreach (var stage in _stages)
            {
                var supportingLines = _lines.Where(l => l.SupportsStage(stage.Id)).ToList();

                double totalRequired = 0;
                foreach (var product in products)
                {
                    var fastestLine = supportingLines.OrderBy(l => l.GetActualCycleTime(stage.Id)).FirstOrDefault();
                    if (fastestLine != null)
                        totalRequired += fastestLine.CalculateProcessingTime(stage.Id, product.RequiredQuantity);
                }

                double totalAvailable = supportingLines.Sum(l => _calendar.GetTotalWorkingMinutes(minStart, maxDeadline, l.Id));

                analyses.Add(new CapacityAnalysis
                {
                    StageId = stage.Id,
                    StageName = stage.Name,
                    RequiredTimeMinutes = totalRequired,
                    AvailableTimeMinutes = totalAvailable,
                    AvailableLineCount = supportingLines.Count
                });
            }

            return analyses;
        }

        private int CalculateHorizon(List<Product> products)
        {
            double totalProcessingTime = 0;
            foreach (var product in products)
            {
                foreach (var stage in _stages)
                {
                    var fastestLine = _lines.Where(l => l.SupportsStage(stage.Id))
                        .OrderBy(l => l.GetActualCycleTime(stage.Id)).FirstOrDefault();
                    if (fastestLine != null)
                        totalProcessingTime += fastestLine.CalculateProcessingTime(stage.Id, product.RequiredQuantity);
                }
            }

            int transferBuffer = EnableTransferTime 
                ? products.Count * _stages.Count * _transferMatrix.DefaultTransferTimeMinutes : 0;

            var maxDeadline = products.Max(p => p.DueDate);
            var minStart = products.Min(p => p.StartDate);
            int calendarMinutes = _calendar.GetTotalWorkingMinutes(minStart, maxDeadline.AddDays(30));

            int horizon = Math.Max((int)(totalProcessingTime * 2) + transferBuffer, calendarMinutes);
            int minHorizon = _calendar.GetTotalWorkingMinutes(minStart, minStart.AddDays(7));
            
            return Math.Max(horizon, minHorizon);
        }

        private int ConvertDateToMinutes(DateTime date, string lineId = null) =>
            _calendar.ConvertDateToWorkingMinutes(date, _referenceDate, lineId);

        private DateTime ConvertMinutesToDate(long minutes, string lineId = null) =>
            _calendar.ConvertWorkingMinutesToDate((int)minutes, _referenceDate, lineId);

        private void ConvertToActualDates(ScheduleResult result)
        {
            foreach (var task in result.Tasks)
            {
                task.ActualStartDate = ConvertMinutesToDate(task.StartTimeMinutes, task.LineId);
                task.ActualEndDate = ConvertMinutesToDate(task.EndTimeMinutes, task.LineId);
            }
        }

        private void CheckDeadlines(ScheduleResult result, List<Product> products)
        {
            var lastStageId = _stages.Last().Id;
            foreach (var product in products)
            {
                var lastTask = result.Tasks.FirstOrDefault(t => t.ProductId == product.Id && t.StageId == lastStageId);
                if (lastTask != null && lastTask.ActualEndDate > product.DueDate)
                {
                    var delay = lastTask.ActualEndDate - product.DueDate;
                    int workingDaysLate = 0;
                    for (var d = product.DueDate.Date; d < lastTask.ActualEndDate.Date; d = d.AddDays(1))
                        if (_calendar.IsWorkingDay(d)) workingDaysLate++;

                    result.MissedDeadlines.Add(new DeadlineMiss
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        DueDate = product.DueDate,
                        ExpectedCompletion = lastTask.ActualEndDate,
                        DelayDays = workingDaysLate,
                        DelayHours = delay.TotalHours,
                        Reason = $"Hoàn thành lúc {lastTask.ActualEndDate:dd/MM/yyyy HH:mm}, trễ {workingDaysLate} ngày làm việc"
                    });
                }
            }
        }

        private void CalculateStatistics(ScheduleResult result, int horizon)
        {
            foreach (var line in _lines)
            {
                var lineTasks = result.Tasks.Where(t => t.LineId == line.Id).ToList();
                int availableMinutes = _calendar.GetTotalWorkingMinutes(_referenceDate, ConvertMinutesToDate(result.MakespanMinutes), line.Id);

                result.LineUtilizations.Add(new LineUtilization
                {
                    LineId = line.Id,
                    LineName = line.Name,
                    TotalWorkMinutes = lineTasks.Sum(t => t.ProcessingTimeMinutes),
                    TotalSetupMinutes = lineTasks.Sum(t => t.SetupTimeMinutes),
                    TotalTransferMinutes = lineTasks.Sum(t => t.TransferTimeMinutes),
                    AvailableMinutes = availableMinutes,
                    TaskCount = lineTasks.Count,
                    ChangeoverCount = lineTasks.Count(t => t.SetupTimeMinutes > 0),
                    TransferCount = lineTasks.Count(t => t.TransferTimeMinutes > 0)
                });
            }

            result.ChangeoverStats = new ChangeoverStatistics
            {
                TotalChangeoverCount = result.Tasks.Count(t => t.SetupTimeMinutes > 0),
                TotalChangeoverTimeMinutes = result.Tasks.Sum(t => t.SetupTimeMinutes),
                TotalComponentChanges = result.Tasks.Sum(t => t.ChangeoverComponents.Count),
                TotalTransferCount = result.Tasks.Count(t => t.TransferTimeMinutes > 0),
                TotalTransferTimeMinutes = result.Tasks.Sum(t => t.TransferTimeMinutes)
            };
        }

        private void AnalyzeFailure(ScheduleResult result, List<Product> products)
        {
            foreach (var product in products.OrderBy(p => p.DueDate))
            {
                double minTime = 0;
                foreach (var stage in _stages)
                {
                    var fastestLine = _lines.Where(l => l.SupportsStage(stage.Id))
                        .OrderBy(l => l.GetActualCycleTime(stage.Id)).FirstOrDefault();
                    if (fastestLine != null)
                        minTime += fastestLine.CalculateProcessingTime(stage.Id, product.RequiredQuantity);
                }

                if (EnableTransferTime)
                    minTime += (_stages.Count - 1) * _transferMatrix.DefaultTransferTimeMinutes;

                int availableMinutes = _calendar.GetTotalWorkingMinutes(product.StartDate, product.DueDate);
                if (minTime > availableMinutes)
                {
                    int workingDays = _calendar.GetWorkingDayCount(product.StartDate, product.DueDate);
                    result.FailureReasons.Add(
                        $"Sản phẩm '{product.Name}': Cần {minTime:F0} phút (gồm transfer), " +
                        $"có {availableMinutes:F0} phút ({workingDays} ngày).");
                }
            }

            foreach (var product in products)
            {
                var maxSlots = _lines.Max(l => l.MaxFeederSlots);
                if (product.TotalFeederSlotsRequired > maxSlots)
                    result.FailureReasons.Add($"Sản phẩm '{product.Name}': Cần {product.TotalFeederSlotsRequired} khe Feeder, max {maxSlots}.");
            }
        }

        #endregion

        public string GetGroupingReport(List<Product> products)
        {
            var groups = _groupingService.GroupProductsByComponents(products, MinSimilarityPercent);
            return _groupingService.GenerateGroupingReport(groups);
        }
    }

    internal class TaskVars
    {
        public IntVar Start { get; set; }
        public IntVar End { get; set; }
        public IntervalVar Interval { get; set; }
        public BoolVar IsAssigned { get; set; }
        public int ProcessingTime { get; set; }
    }
}
