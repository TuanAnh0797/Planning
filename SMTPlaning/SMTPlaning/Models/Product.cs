using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Sản phẩm PCB cần sản xuất
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Mã sản phẩm
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tên sản phẩm / Model
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Số lượng khách đặt
        /// </summary>
        public int OrderQuantity { get; set; }

        /// <summary>
        /// Tồn kho hiện tại
        /// </summary>
        public int StockQuantity { get; set; }

        /// <summary>
        /// Số lượng thực tế cần sản xuất = OrderQuantity - StockQuantity
        /// </summary>
        public int RequiredQuantity => Math.Max(0, OrderQuantity - StockQuantity);

        /// <summary>
        /// Ngày bắt đầu được phép sản xuất
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Ngày phải hoàn thành (Deadline)
        /// </summary>
        public DateTime DueDate { get; set; }

        /// <summary>
        /// Độ ưu tiên (1 = cao nhất)
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// BOM - Danh sách ID linh kiện cần cho sản phẩm này
        /// </summary>
        public List<string> ComponentIds { get; set; } = new List<string>();

        /// <summary>
        /// Tổng số khe Feeder cần (tính từ BOM)
        /// Sẽ được tính sau khi load Components
        /// </summary>
        public int TotalFeederSlotsRequired { get; set; }

        /// <summary>
        /// Cycle time riêng cho từng công đoạn (nếu khác với mặc định của Line)
        /// Key = StageId, Value = CycleTime (phút)
        /// Nếu null, sử dụng cycle time của Line
        /// </summary>
        public Dictionary<int, double> CustomCycleTimePerStage { get; set; }

        /// <summary>
        /// ID nhóm sản phẩm (do người dùng định nghĩa sẵn)
        /// Các sản phẩm cùng GroupId sẽ được sản xuất liên tiếp
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// Thứ tự trong nhóm (1, 2, 3...)
        /// </summary>
        public int SequenceInGroup { get; set; } = 0;

        /// <summary>
        /// Cấu hình Lot Splitting riêng cho sản phẩm này
        /// Nếu null, sử dụng DefaultLotConfig của Scheduler
        /// </summary>
        public LotSplitConfig LotConfig { get; set; }

        /// <summary>
        /// Cấu hình Lot Splitting riêng cho TỪNG CÔNG ĐOẠN
        /// Key = StageId, Value = LotSplitConfig cho công đoạn đó
        /// Nếu công đoạn không có trong dictionary, sử dụng LotConfig chung
        /// </summary>
        public Dictionary<int, LotSplitConfig> StageLotConfigs { get; set; } = new Dictionary<int, LotSplitConfig>();

        /// <summary>
        /// Lấy LotSplitConfig cho một công đoạn cụ thể
        /// </summary>
        public LotSplitConfig GetLotConfigForStage(int stageId)
        {
            // Ưu tiên 1: Config riêng cho công đoạn
            if (StageLotConfigs != null && StageLotConfigs.ContainsKey(stageId))
                return StageLotConfigs[stageId];

            // Ưu tiên 2: Config chung của Product
            if (LotConfig != null)
                return LotConfig;

            // Mặc định: Không chia
            return LotSplitConfig.NoSplitting;
        }

        /// <summary>
        /// Thiết lập Lot Splitting đơn giản cho sản phẩm
        /// </summary>
        /// <param name="batchSize">Số lượng mỗi batch</param>
        /// <param name="minGapMinutes">Thời gian chờ giữa các batch (phút)</param>
        public void SetLotSplitting(int batchSize, int minGapMinutes = 5)
        {
            LotConfig = new LotSplitConfig
            {
                EnableSplitting = true,
                Strategy = LotSplitStrategy.FixedQuantity,
                BatchSize = batchSize,
                MinGapBetweenBatches = minGapMinutes
            };
        }

        /// <summary>
        /// Thiết lập Lot Splitting theo số batch
        /// </summary>
        /// <param name="numberOfBatches">Số batch muốn chia</param>
        /// <param name="minGapMinutes">Thời gian chờ giữa các batch (phút)</param>
        public void SetLotSplittingByBatches(int numberOfBatches, int minGapMinutes = 5)
        {
            LotConfig = new LotSplitConfig
            {
                EnableSplitting = true,
                Strategy = LotSplitStrategy.FixedBatches,
                NumberOfBatches = numberOfBatches,
                MinGapBetweenBatches = minGapMinutes
            };
        }

        /// <summary>
        /// Thiết lập Lot Splitting cho MỘT CÔNG ĐOẠN CỤ THỂ
        /// </summary>
        /// <param name="stageId">ID công đoạn</param>
        /// <param name="batchSize">Số lượng mỗi batch</param>
        /// <param name="minGapMinutes">Thời gian chờ giữa các batch (phút)</param>
        public void SetStageLotSplitting(int stageId, int batchSize, int minGapMinutes = 5)
        {
            if (StageLotConfigs == null)
                StageLotConfigs = new Dictionary<int, LotSplitConfig>();

            StageLotConfigs[stageId] = new LotSplitConfig
            {
                EnableSplitting = true,
                Strategy = LotSplitStrategy.FixedQuantity,
                BatchSize = batchSize,
                MinGapBetweenBatches = minGapMinutes
            };
        }

        /// <summary>
        /// Thiết lập Lot Splitting cho NHIỀU CÔNG ĐOẠN
        /// </summary>
        /// <param name="stageBatchSizes">Dictionary: StageId → BatchSize</param>
        /// <param name="minGapMinutes">Thời gian chờ giữa các batch (phút)</param>
        public void SetStageLotSplitting(Dictionary<int, int> stageBatchSizes, int minGapMinutes = 5)
        {
            if (StageLotConfigs == null)
                StageLotConfigs = new Dictionary<int, LotSplitConfig>();

            foreach (var kvp in stageBatchSizes)
            {
                StageLotConfigs[kvp.Key] = new LotSplitConfig
                {
                    EnableSplitting = true,
                    Strategy = LotSplitStrategy.FixedQuantity,
                    BatchSize = kvp.Value,
                    MinGapBetweenBatches = minGapMinutes
                };
            }
        }

        /// <summary>
        /// Tắt Lot Splitting cho sản phẩm này
        /// </summary>
        public void DisableLotSplitting()
        {
            LotConfig = LotSplitConfig.NoSplitting;
            StageLotConfigs?.Clear();
        }

        /// <summary>
        /// Tắt Lot Splitting cho một công đoạn cụ thể
        /// </summary>
        public void DisableStageLotSplitting(int stageId)
        {
            if (StageLotConfigs == null)
                StageLotConfigs = new Dictionary<int, LotSplitConfig>();

            StageLotConfigs[stageId] = LotSplitConfig.NoSplitting;
        }

        /// <summary>
        /// Tên model tại mỗi công đoạn (Model có thể đổi tên qua từng công đoạn)
        /// Key = StageId, Value = Tên model tại công đoạn đó
        /// VD: Stage 1 = "PCB-A", Stage 2 = "PCB-A-S1", Stage 3 = "PCB-A-S2"
        /// </summary>
        public Dictionary<int, string> StageNames { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Pattern để tự động tạo tên theo công đoạn
        /// VD: "{Name}-S{StageOrder}" → "PCB-A-S1", "PCB-A-S2"...
        /// </summary>
        public string StageNamePattern { get; set; }

        public Product() { }

        public Product(string id, string name, int orderQty, int stockQty,
                      DateTime startDate, DateTime dueDate, int priority = 1)
        {
            Id = id;
            Name = name;
            OrderQuantity = orderQty;
            StockQuantity = stockQty;
            StartDate = startDate;
            DueDate = dueDate;
            Priority = priority;
        }

        /// <summary>
        /// Lấy tên model tại một công đoạn cụ thể
        /// </summary>
        /// <param name="stageId">ID công đoạn</param>
        /// <param name="stageOrder">Thứ tự công đoạn (1, 2, 3...)</param>
        /// <returns>Tên model tại công đoạn</returns>
        public string GetNameAtStage(int stageId, int stageOrder = 0)
        {
            // 1. Nếu có tên cụ thể cho stage này
            if (StageNames != null && StageNames.ContainsKey(stageId))
                return StageNames[stageId];

            // 2. Nếu có pattern, tự động tạo tên
            if (!string.IsNullOrEmpty(StageNamePattern))
            {
                return StageNamePattern
                    .Replace("{Name}", Name)
                    .Replace("{Id}", Id)
                    .Replace("{StageId}", stageId.ToString())
                    .Replace("{StageOrder}", stageOrder.ToString());
            }

            // 3. Mặc định trả về tên gốc
            return Name;
        }

        /// <summary>
        /// Đặt tên model cho một công đoạn
        /// </summary>
        public void SetStageName(int stageId, string name)
        {
            if (StageNames == null)
                StageNames = new Dictionary<int, string>();
            StageNames[stageId] = name;
        }

        /// <summary>
        /// Đặt tên model cho nhiều công đoạn
        /// </summary>
        public void SetStageNames(Dictionary<int, string> names)
        {
            StageNames = names ?? new Dictionary<int, string>();
        }

        /// <summary>
        /// Thêm linh kiện vào BOM
        /// </summary>
        public void AddComponent(string componentId)
        {
            if (!ComponentIds.Contains(componentId))
                ComponentIds.Add(componentId);
        }

        /// <summary>
        /// Thêm nhiều linh kiện vào BOM
        /// </summary>
        public void AddComponents(IEnumerable<string> componentIds)
        {
            foreach (var id in componentIds)
                AddComponent(id);
        }

        /// <summary>
        /// Tính số linh kiện chung với sản phẩm khác
        /// </summary>
        public int GetCommonComponentCount(Product other)
        {
            return ComponentIds.Intersect(other.ComponentIds).Count();
        }

        /// <summary>
        /// Tính số linh kiện cần thay đổi khi chuyển từ sản phẩm khác sang sản phẩm này
        /// </summary>
        public int GetChangeoverComponentCount(Product previousProduct)
        {
            if (previousProduct == null)
                return ComponentIds.Count;

            // Số linh kiện cần thêm mới = linh kiện của this mà previous không có
            return ComponentIds.Except(previousProduct.ComponentIds).Count();
        }

        /// <summary>
        /// Lấy danh sách linh kiện cần thay đổi
        /// </summary>
        public List<string> GetChangeoverComponents(Product previousProduct)
        {
            if (previousProduct == null)
                return ComponentIds.ToList();

            return ComponentIds.Except(previousProduct.ComponentIds).ToList();
        }

        public override string ToString()
        {
            string groupInfo = !string.IsNullOrEmpty(GroupId) ? $", Group: {GroupId}" : "";
            return $"{Name} (SL: {RequiredQuantity}, LK: {ComponentIds.Count}, Deadline: {DueDate:dd/MM/yyyy}{groupInfo})";
        }
    }

    /// <summary>
    /// Nhóm sản phẩm do người dùng định nghĩa
    /// </summary>
    public class ManualProductGroup
    {
        /// <summary>
        /// ID nhóm
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// Tên nhóm
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Mô tả nhóm
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Danh sách ID sản phẩm trong nhóm (đã sắp xếp theo thứ tự sản xuất)
        /// </summary>
        public List<string> ProductIds { get; set; } = new List<string>();

        /// <summary>
        /// Thứ tự ưu tiên của nhóm (1 = cao nhất)
        /// </summary>
        public int GroupPriority { get; set; } = 1;

        /// <summary>
        /// Line được chỉ định cho nhóm này (null = tự động chọn)
        /// </summary>
        public string AssignedLineId { get; set; }

        /// <summary>
        /// Thời gian changeover giữa các sản phẩm trong nhóm (phút)
        /// Mặc định = 0 vì đã được tối ưu linh kiện
        /// </summary>
        public int InternalChangeoverMinutes { get; set; } = 0;

        /// <summary>
        /// Thời gian changeover khi chuyển sang nhóm khác (phút)
        /// </summary>
        public int ExternalChangeoverMinutes { get; set; } = 30;

        /// <summary>
        /// Ghi chú
        /// </summary>
        public string Notes { get; set; }

        public ManualProductGroup() { }

        public ManualProductGroup(string groupId, string groupName)
        {
            GroupId = groupId;
            GroupName = groupName;
        }

        /// <summary>
        /// Thêm sản phẩm vào nhóm
        /// </summary>
        public void AddProduct(string productId)
        {
            if (!ProductIds.Contains(productId))
                ProductIds.Add(productId);
        }

        /// <summary>
        /// Thêm nhiều sản phẩm vào nhóm (theo thứ tự)
        /// </summary>
        public void AddProducts(params string[] productIds)
        {
            foreach (var id in productIds)
                AddProduct(id);
        }

        /// <summary>
        /// Số sản phẩm trong nhóm
        /// </summary>
        public int ProductCount => ProductIds.Count;

        public override string ToString()
        {
            return $"{GroupName} ({GroupId}): {ProductCount} sản phẩm";
        }
    }
}

