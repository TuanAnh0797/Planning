using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Chiến lược chia Lot
    /// </summary>
    public enum LotSplitStrategy
    {
        /// <summary>Không chia - Phải hoàn thành toàn bộ mới chuyển công đoạn</summary>
        NoSplit,

        /// <summary>Chia đều theo số lượng cố định</summary>
        FixedQuantity,

        /// <summary>Chia theo phần trăm</summary>
        Percentage,

        /// <summary>Chia theo số batch cố định</summary>
        FixedBatches,

        /// <summary>Tự động tối ưu (hệ thống quyết định)</summary>
        Auto
    }

    /// <summary>
    /// Mức độ ưu tiên sản xuất
    /// </summary>
    public enum ProductionPriority
    {
        /// <summary>Khẩn cấp - Làm ngay, có thể ngắt công việc khác</summary>
        Critical = 1,

        /// <summary>Rất cao - Ưu tiên hàng đầu</summary>
        VeryHigh = 2,

        /// <summary>Cao - Ưu tiên</summary>
        High = 3,

        /// <summary>Bình thường</summary>
        Normal = 4,

        /// <summary>Thấp - Có thể delay nếu cần</summary>
        Low = 5,

        /// <summary>Rất thấp - Làm khi rảnh</summary>
        VeryLow = 6
    }

    /// <summary>
    /// Cấu hình chia Lot cho một sản phẩm
    /// </summary>
    public class LotSplitConfig
    {
        /// <summary>
        /// Cho phép chia Lot không
        /// </summary>
        public bool EnableSplitting { get; set; } = false;

        /// <summary>
        /// Chiến lược chia
        /// </summary>
        public LotSplitStrategy Strategy { get; set; } = LotSplitStrategy.NoSplit;

        /// <summary>
        /// Số lượng mỗi batch (khi Strategy = FixedQuantity)
        /// VD: 100 = mỗi batch 100 sản phẩm
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Phần trăm mỗi batch (khi Strategy = Percentage)
        /// VD: 25 = chia làm 4 batch, mỗi batch 25%
        /// </summary>
        public double BatchPercentage { get; set; } = 25;

        /// <summary>
        /// Số batch cố định (khi Strategy = FixedBatches)
        /// </summary>
        public int NumberOfBatches { get; set; } = 4;

        /// <summary>
        /// Số lượng tối thiểu để được chia
        /// Lot nhỏ hơn giá trị này sẽ không chia
        /// </summary>
        public int MinQuantityToSplit { get; set; } = 200;

        /// <summary>
        /// Số lượng tối thiểu của một batch
        /// </summary>
        public int MinBatchSize { get; set; } = 50;

        /// <summary>
        /// Cho phép batch cuối cùng nhỏ hơn MinBatchSize không
        /// </summary>
        public bool AllowSmallLastBatch { get; set; } = true;

        /// <summary>
        /// Thời gian chờ tối thiểu giữa các batch (phút)
        /// Để đảm bảo batch trước đã sẵn sàng
        /// </summary>
        public int MinGapBetweenBatches { get; set; } = 5;

        /// <summary>
        /// Tính số batch từ tổng số lượng
        /// </summary>
        public List<int> CalculateBatches(int totalQuantity)
        {
            if (!EnableSplitting || totalQuantity < MinQuantityToSplit)
            {
                return new List<int> { totalQuantity };
            }

            var batches = new List<int>();

            switch (Strategy)
            {
                case LotSplitStrategy.FixedQuantity:
                    int remaining = totalQuantity;
                    while (remaining > 0)
                    {
                        int batchQty = Math.Min(remaining, BatchSize);
                        if (batchQty < MinBatchSize && !AllowSmallLastBatch && batches.Any())
                        {
                            // Gộp vào batch trước
                            batches[batches.Count - 1] += batchQty;
                        }
                        else
                        {
                            batches.Add(batchQty);
                        }
                        remaining -= BatchSize;
                    }
                    break;

                case LotSplitStrategy.Percentage:
                    int batchCount = (int)Math.Ceiling(100.0 / BatchPercentage);
                    int baseSize = totalQuantity / batchCount;
                    int remainder = totalQuantity % batchCount;

                    for (int i = 0; i < batchCount; i++)
                    {
                        int qty = baseSize + (i < remainder ? 1 : 0);
                        if (qty >= MinBatchSize || (AllowSmallLastBatch && i == batchCount - 1))
                        {
                            batches.Add(qty);
                        }
                        else if (batches.Any())
                        {
                            batches[batches.Count - 1] += qty;
                        }
                        else
                        {
                            batches.Add(qty);
                        }
                    }
                    break;

                case LotSplitStrategy.FixedBatches:
                    int batchSize = totalQuantity / NumberOfBatches;
                    int rem = totalQuantity % NumberOfBatches;

                    for (int i = 0; i < NumberOfBatches; i++)
                    {
                        batches.Add(batchSize + (i < rem ? 1 : 0));
                    }
                    break;

                case LotSplitStrategy.Auto:
                    // Tự động chọn batch size tối ưu
                    int optimalBatchSize = Math.Max(MinBatchSize, totalQuantity / 4);
                    optimalBatchSize = Math.Min(optimalBatchSize, 500); // Max 500/batch
                    
                    int autoRemaining = totalQuantity;
                    while (autoRemaining > 0)
                    {
                        int qty = Math.Min(autoRemaining, optimalBatchSize);
                        batches.Add(qty);
                        autoRemaining -= optimalBatchSize;
                    }
                    break;

                default:
                    batches.Add(totalQuantity);
                    break;
            }

            return batches;
        }

        /// <summary>
        /// Config mặc định - không chia
        /// </summary>
        public static LotSplitConfig NoSplitting => new LotSplitConfig
        {
            EnableSplitting = false,
            Strategy = LotSplitStrategy.NoSplit
        };

        /// <summary>
        /// Config chia theo số lượng cố định
        /// </summary>
        public static LotSplitConfig FixedSize(int batchSize, int minToSplit = 200)
        {
            return new LotSplitConfig
            {
                EnableSplitting = true,
                Strategy = LotSplitStrategy.FixedQuantity,
                BatchSize = batchSize,
                MinQuantityToSplit = minToSplit,
                MinBatchSize = Math.Min(50, batchSize / 2)
            };
        }

        /// <summary>
        /// Config chia tự động
        /// </summary>
        public static LotSplitConfig AutoSplit(int minToSplit = 200)
        {
            return new LotSplitConfig
            {
                EnableSplitting = true,
                Strategy = LotSplitStrategy.Auto,
                MinQuantityToSplit = minToSplit
            };
        }

        public override string ToString()
        {
            if (!EnableSplitting) return "Không chia Lot";
            return $"{Strategy}: BatchSize={BatchSize}, MinToSplit={MinQuantityToSplit}";
        }
    }

    /// <summary>
    /// Một Batch trong Lot (sau khi chia)
    /// </summary>
    public class ProductionBatch
    {
        /// <summary>
        /// ID batch (VD: PCB-A_B1, PCB-A_B2)
        /// </summary>
        public string BatchId { get; set; }

        /// <summary>
        /// ID sản phẩm gốc
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Tên sản phẩm
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Số thứ tự batch (1, 2, 3...)
        /// </summary>
        public int BatchNumber { get; set; }

        /// <summary>
        /// Tổng số batch của sản phẩm này
        /// </summary>
        public int TotalBatches { get; set; }

        /// <summary>
        /// Số lượng trong batch này
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Ngày bắt đầu được phép (inherit từ product)
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Deadline (inherit từ product)
        /// </summary>
        public DateTime DueDate { get; set; }

        /// <summary>
        /// Priority (inherit từ product)
        /// </summary>
        public ProductionPriority Priority { get; set; }

        /// <summary>
        /// Batch trước đó (null nếu là batch đầu)
        /// </summary>
        public string PreviousBatchId { get; set; }

        /// <summary>
        /// Batch tiếp theo (null nếu là batch cuối)
        /// </summary>
        public string NextBatchId { get; set; }

        /// <summary>
        /// Có phải batch đầu tiên không
        /// </summary>
        public bool IsFirstBatch => BatchNumber == 1;

        /// <summary>
        /// Có phải batch cuối cùng không
        /// </summary>
        public bool IsLastBatch => BatchNumber == TotalBatches;

        /// <summary>
        /// Thời gian chờ tối thiểu sau batch trước (phút)
        /// Áp dụng cho cùng công đoạn trên cùng Line
        /// </summary>
        public int MinGapAfterPreviousBatch { get; set; } = 5;

        public override string ToString()
        {
            return $"{BatchId}: {Quantity} sp (Batch {BatchNumber}/{TotalBatches})";
        }
    }

    /// <summary>
    /// Cấu hình ưu tiên sản xuất cho một sản phẩm
    /// </summary>
    public class PriorityConfig
    {
        /// <summary>
        /// Mức độ ưu tiên
        /// </summary>
        public ProductionPriority Priority { get; set; } = ProductionPriority.Normal;

        /// <summary>
        /// Điểm ưu tiên phụ (để phân biệt khi cùng Priority)
        /// Số càng nhỏ càng ưu tiên cao
        /// </summary>
        public int SubPriority { get; set; } = 100;

        /// <summary>
        /// Cho phép ngắt (preempt) công việc khác không
        /// Chỉ áp dụng cho Critical priority
        /// </summary>
        public bool AllowPreemption { get; set; } = false;

        /// <summary>
        /// Có phải đơn hàng quan trọng (khách VIP, hợp đồng lớn...)
        /// </summary>
        public bool IsVIPOrder { get; set; } = false;

        /// <summary>
        /// Penalty cost nếu trễ deadline (VND/ngày)
        /// Dùng để solver quyết định ưu tiên
        /// </summary>
        public decimal PenaltyCostPerDay { get; set; } = 0;

        /// <summary>
        /// Ghi chú lý do ưu tiên
        /// </summary>
        public string PriorityReason { get; set; }

        /// <summary>
        /// Tính điểm ưu tiên tổng hợp (để sort)
        /// Số càng nhỏ càng ưu tiên cao
        /// </summary>
        public int CalculatePriorityScore(DateTime dueDate, DateTime referenceDate)
        {
            // Base score từ Priority level
            int baseScore = (int)Priority * 1000;

            // Adjust by sub-priority
            baseScore += SubPriority;

            // Adjust by urgency (days until due)
            int daysUntilDue = (int)(dueDate - referenceDate).TotalDays;
            if (daysUntilDue <= 1) baseScore -= 500;      // Very urgent
            else if (daysUntilDue <= 3) baseScore -= 200; // Urgent
            else if (daysUntilDue <= 7) baseScore -= 50;  // Soon

            // Adjust for VIP
            if (IsVIPOrder) baseScore -= 300;

            // Adjust for penalty
            if (PenaltyCostPerDay > 0)
            {
                baseScore -= (int)(PenaltyCostPerDay / 10000); // Mỗi 10k penalty = -1 điểm
            }

            return baseScore;
        }

        public static PriorityConfig Normal => new PriorityConfig
        {
            Priority = ProductionPriority.Normal,
            SubPriority = 100
        };

        public static PriorityConfig High => new PriorityConfig
        {
            Priority = ProductionPriority.High,
            SubPriority = 50
        };

        public static PriorityConfig Critical => new PriorityConfig
        {
            Priority = ProductionPriority.Critical,
            SubPriority = 10,
            AllowPreemption = true
        };

        public static PriorityConfig VIP(ProductionPriority level = ProductionPriority.High)
        {
            return new PriorityConfig
            {
                Priority = level,
                SubPriority = 30,
                IsVIPOrder = true,
                PriorityReason = "Khách hàng VIP"
            };
        }

        public override string ToString()
        {
            string vip = IsVIPOrder ? " [VIP]" : "";
            return $"{Priority} (Sub:{SubPriority}){vip}";
        }
    }

    /// <summary>
    /// Extended Product với Lot Split và Priority
    /// </summary>
    public class ProductScheduleInfo
    {
        /// <summary>
        /// Sản phẩm gốc
        /// </summary>
        public Product Product { get; set; }

        /// <summary>
        /// Cấu hình chia Lot
        /// </summary>
        public LotSplitConfig LotConfig { get; set; } = LotSplitConfig.NoSplitting;

        /// <summary>
        /// Cấu hình ưu tiên
        /// </summary>
        public PriorityConfig PriorityConfig { get; set; } = PriorityConfig.Normal;

        /// <summary>
        /// Danh sách batch sau khi chia
        /// </summary>
        public List<ProductionBatch> Batches { get; private set; } = new List<ProductionBatch>();

        /// <summary>
        /// Đã được chia thành batch chưa
        /// </summary>
        public bool IsSplit => Batches.Count > 1;

        /// <summary>
        /// Thực hiện chia Lot thành các Batch
        /// </summary>
        public void SplitIntoBatches()
        {
            Batches.Clear();

            // Ưu tiên sử dụng LotConfig của Product nếu có
            var configToUse = Product.LotConfig ?? LotConfig;
            var quantities = configToUse.CalculateBatches(Product.RequiredQuantity);

            for (int i = 0; i < quantities.Count; i++)
            {
                var batch = new ProductionBatch
                {
                    BatchId = quantities.Count > 1 
                        ? $"{Product.Id}_B{i + 1}" 
                        : Product.Id,
                    ProductId = Product.Id,
                    ProductName = Product.Name,
                    BatchNumber = i + 1,
                    TotalBatches = quantities.Count,
                    Quantity = quantities[i],
                    StartDate = Product.StartDate,
                    DueDate = Product.DueDate,
                    Priority = PriorityConfig.Priority,
                    PreviousBatchId = i > 0 ? $"{Product.Id}_B{i}" : null,
                    NextBatchId = i < quantities.Count - 1 ? $"{Product.Id}_B{i + 2}" : null,
                    MinGapAfterPreviousBatch = configToUse.MinGapBetweenBatches
                };

                Batches.Add(batch);
            }
        }

        /// <summary>
        /// Lấy danh sách batch cho một công đoạn cụ thể
        /// Mỗi công đoạn có thể có cấu hình chia batch khác nhau
        /// </summary>
        public List<ProductionBatch> GetBatchesForStage(int stageId)
        {
            // Lấy LotConfig cho công đoạn này
            var stageConfig = Product.GetLotConfigForStage(stageId);
            var quantities = stageConfig.CalculateBatches(Product.RequiredQuantity);

            var stageBatches = new List<ProductionBatch>();
            for (int i = 0; i < quantities.Count; i++)
            {
                var batch = new ProductionBatch
                {
                    BatchId = quantities.Count > 1 
                        ? $"{Product.Id}_S{stageId}_B{i + 1}" 
                        : $"{Product.Id}_S{stageId}",
                    ProductId = Product.Id,
                    ProductName = Product.Name,
                    BatchNumber = i + 1,
                    TotalBatches = quantities.Count,
                    Quantity = quantities[i],
                    StartDate = Product.StartDate,
                    DueDate = Product.DueDate,
                    Priority = PriorityConfig.Priority,
                    PreviousBatchId = i > 0 ? $"{Product.Id}_S{stageId}_B{i}" : null,
                    NextBatchId = i < quantities.Count - 1 ? $"{Product.Id}_S{stageId}_B{i + 2}" : null,
                    MinGapAfterPreviousBatch = stageConfig.MinGapBetweenBatches
                };

                stageBatches.Add(batch);
            }

            return stageBatches;
        }

        /// <summary>
        /// Kiểm tra xem có Stage Lot Splitting không
        /// </summary>
        public bool HasStageLotSplitting => Product.StageLotConfigs != null && Product.StageLotConfigs.Any();

        /// <summary>
        /// Tính điểm ưu tiên
        /// </summary>
        public int GetPriorityScore(DateTime referenceDate)
        {
            return PriorityConfig.CalculatePriorityScore(Product.DueDate, referenceDate);
        }

        public ProductScheduleInfo() { }

        public ProductScheduleInfo(Product product)
        {
            Product = product;
            LotConfig = LotSplitConfig.NoSplitting;
            PriorityConfig = PriorityConfig.Normal;
        }

        public ProductScheduleInfo(Product product, LotSplitConfig lotConfig, PriorityConfig priorityConfig)
        {
            Product = product;
            LotConfig = lotConfig ?? LotSplitConfig.NoSplitting;
            PriorityConfig = priorityConfig ?? PriorityConfig.Normal;
        }

        public override string ToString()
        {
            string batches = IsSplit ? $", {Batches.Count} batches" : "";
            return $"{Product.Name}: {PriorityConfig}{batches}";
        }
    }

    /// <summary>
    /// Helper class để sắp xếp sản phẩm theo priority
    /// </summary>
    public static class ProductPriorityComparer
    {
        /// <summary>
        /// Sắp xếp danh sách sản phẩm theo priority và due date
        /// </summary>
        public static List<ProductScheduleInfo> SortByPriority(
            List<ProductScheduleInfo> products, 
            DateTime referenceDate)
        {
            return products
                .OrderBy(p => p.GetPriorityScore(referenceDate))
                .ThenBy(p => p.Product.DueDate)
                .ThenBy(p => p.Product.Name)
                .ToList();
        }

        /// <summary>
        /// Sắp xếp batch theo priority
        /// </summary>
        public static List<ProductionBatch> SortBatchesByPriority(
            List<ProductionBatch> batches,
            Dictionary<string, ProductScheduleInfo> productInfos,
            DateTime referenceDate)
        {
            return batches
                .OrderBy(b => 
                {
                    if (productInfos.TryGetValue(b.ProductId, out var info))
                        return info.GetPriorityScore(referenceDate);
                    return 10000; // Default low priority
                })
                .ThenBy(b => b.DueDate)
                .ThenBy(b => b.BatchNumber) // Batch đầu trước
                .ToList();
        }
    }
}
