using System;
using System.Collections.Generic;
using System.Linq;
using SMTScheduler.Models;

namespace SMTScheduler.Services
{
    /// <summary>
    /// Service để group các sản phẩm có linh kiện tương tự
    /// Giúp giảm thời gian changeover
    /// </summary>
    public class ComponentGroupingService
    {
        private readonly Dictionary<string, Component> _components;
        private readonly int _maxFeederSlots;

        public ComponentGroupingService(List<Component> components, int maxFeederSlots)
        {
            _components = components.ToDictionary(c => c.Id);
            _maxFeederSlots = maxFeederSlots;
        }

        /// <summary>
        /// Tính độ tương đồng giữa 2 sản phẩm (%)
        /// Jaccard Similarity = (A ∩ B) / (A ∪ B)
        /// </summary>
        public double CalculateSimilarity(Product p1, Product p2)
        {
            if (p1.ComponentIds.Count == 0 && p2.ComponentIds.Count == 0)
                return 100;

            var intersection = p1.ComponentIds.Intersect(p2.ComponentIds).Count();
            var union = p1.ComponentIds.Union(p2.ComponentIds).Count();

            if (union == 0) return 0;

            return (intersection * 100.0) / union;
        }

        /// <summary>
        /// Tính ma trận độ tương đồng giữa tất cả các sản phẩm
        /// </summary>
        public double[,] CalculateSimilarityMatrix(List<Product> products)
        {
            int n = products.Count;
            var matrix = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                        matrix[i, j] = 100;
                    else
                        matrix[i, j] = CalculateSimilarity(products[i], products[j]);
                }
            }

            return matrix;
        }

        /// <summary>
        /// Tính thời gian changeover giữa 2 sản phẩm
        /// </summary>
        public double CalculateChangeoverTime(Product from, Product to)
        {
            if (from == null)
            {
                // Lần đầu setup, cần load tất cả linh kiện
                return to.ComponentIds.Sum(id => GetComponentChangeoverTime(id));
            }

            // Chỉ tính linh kiện cần thay mới
            var componentsToChange = to.GetChangeoverComponents(from);
            return componentsToChange.Sum(id => GetComponentChangeoverTime(id));
        }

        /// <summary>
        /// Lấy thời gian thay đổi của một linh kiện
        /// </summary>
        private double GetComponentChangeoverTime(string componentId)
        {
            if (_components.TryGetValue(componentId, out var comp))
                return comp.ChangeoverTimeMinutes;
            return 2; // Mặc định 2 phút
        }

        /// <summary>
        /// Tính tổng số khe Feeder cần cho một tập sản phẩm
        /// </summary>
        public int CalculateTotalFeederSlots(List<Product> products)
        {
            var allComponents = products.SelectMany(p => p.ComponentIds).Distinct();
            return allComponents.Sum(id =>
            {
                if (_components.TryGetValue(id, out var comp))
                    return comp.FeederSlots;
                return 1;
            });
        }

        /// <summary>
        /// Group sản phẩm theo linh kiện sử dụng Greedy Clustering
        /// </summary>
        public List<ProductGroup> GroupProductsByComponents(List<Product> products, 
            double minSimilarityPercent = 30, int? maxFeederSlotsOverride = null)
        {
            int maxSlots = maxFeederSlotsOverride ?? _maxFeederSlots;
            var groups = new List<ProductGroup>();
            var remaining = products.ToList();
            int groupIndex = 1;

            while (remaining.Any())
            {
                // Bắt đầu group mới với sản phẩm có deadline sớm nhất
                var seed = remaining.OrderBy(p => p.DueDate).ThenBy(p => p.Priority).First();
                remaining.Remove(seed);

                var group = new ProductGroup
                {
                    GroupId = $"G{groupIndex:D3}",
                    GroupName = $"Nhóm {groupIndex}",
                    Products = new List<Product> { seed },
                    AllComponentIds = seed.ComponentIds.ToList()
                };

                // Thêm các sản phẩm tương tự vào group
                bool added;
                do
                {
                    added = false;
                    var candidates = remaining
                        .Select(p => new
                        {
                            Product = p,
                            Similarity = CalculateSimilarity(seed, p),
                            NewComponents = p.ComponentIds.Except(group.AllComponentIds).ToList()
                        })
                        .Where(x => x.Similarity >= minSimilarityPercent)
                        .OrderByDescending(x => x.Similarity)
                        .ThenBy(x => x.Product.DueDate)
                        .ToList();

                    foreach (var candidate in candidates)
                    {
                        // Kiểm tra xem thêm sản phẩm này có vượt quá số khe Feeder không
                        var newTotalSlots = CalculateTotalFeederSlots(
                            group.Products.Concat(new[] { candidate.Product }).ToList());

                        if (newTotalSlots <= maxSlots)
                        {
                            group.Products.Add(candidate.Product);
                            group.AllComponentIds = group.AllComponentIds
                                .Union(candidate.Product.ComponentIds)
                                .ToList();
                            remaining.Remove(candidate.Product);
                            added = true;
                        }
                    }
                } while (added && remaining.Any());

                // Sắp xếp sản phẩm trong group để minimize changeover
                group.Products = OptimizeProductSequence(group.Products);

                // Tính thống kê group
                CalculateGroupStatistics(group);

                groups.Add(group);
                groupIndex++;
            }

            return groups;
        }

        /// <summary>
        /// Sắp xếp thứ tự sản phẩm trong group để minimize changeover
        /// Sử dụng thuật toán Nearest Neighbor (giống TSP)
        /// </summary>
        public List<Product> OptimizeProductSequence(List<Product> products)
        {
            if (products.Count <= 2)
                return products.OrderBy(p => p.DueDate).ToList();

            var result = new List<Product>();
            var remaining = products.ToList();

            // Bắt đầu với sản phẩm có deadline sớm nhất
            var current = remaining.OrderBy(p => p.DueDate).ThenBy(p => p.Priority).First();
            result.Add(current);
            remaining.Remove(current);

            while (remaining.Any())
            {
                // Tìm sản phẩm gần nhất (ít changeover nhất)
                var next = remaining
                    .OrderBy(p => CalculateChangeoverTime(current, p))
                    .ThenBy(p => p.DueDate)
                    .First();

                result.Add(next);
                remaining.Remove(next);
                current = next;
            }

            return result;
        }

        /// <summary>
        /// Tính thống kê cho một group
        /// </summary>
        private void CalculateGroupStatistics(ProductGroup group)
        {
            group.TotalFeederSlots = CalculateTotalFeederSlots(group.Products);

            // Tính changeover trong group
            group.TotalChangeoverCount = 0;
            group.TotalChangeoverTimeMinutes = 0;

            Product previous = null;
            foreach (var product in group.Products)
            {
                if (previous != null)
                {
                    var changeoverTime = CalculateChangeoverTime(previous, product);
                    if (changeoverTime > 0)
                    {
                        group.TotalChangeoverCount++;
                        group.TotalChangeoverTimeMinutes += changeoverTime;
                    }
                }
                previous = product;
            }

            // Tính độ tương đồng trung bình
            if (group.Products.Count > 1)
            {
                var similarities = new List<double>();
                for (int i = 0; i < group.Products.Count - 1; i++)
                {
                    similarities.Add(CalculateSimilarity(group.Products[i], group.Products[i + 1]));
                }
                group.AverageSimilarityPercent = similarities.Average();
            }
            else
            {
                group.AverageSimilarityPercent = 100;
            }
        }

        /// <summary>
        /// In báo cáo grouping
        /// </summary>
        public string GenerateGroupingReport(List<ProductGroup> groups)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("              BÁO CÁO GROUPING LINH KIỆN                   ");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var group in groups)
            {
                sb.AppendLine($"┌─ {group.GroupName} ({group.GroupId}) ─────────────────────────────────");
                sb.AppendLine($"│  Số sản phẩm: {group.Products.Count}");
                sb.AppendLine($"│  Tổng linh kiện: {group.AllComponentIds.Count}");
                sb.AppendLine($"│  Số khe Feeder: {group.TotalFeederSlots}");
                sb.AppendLine($"│  Độ tương đồng TB: {group.AverageSimilarityPercent:F1}%");
                sb.AppendLine($"│  Số lần changeover: {group.TotalChangeoverCount}");
                sb.AppendLine($"│  Thời gian changeover: {group.TotalChangeoverTimeMinutes:F0} phút");
                sb.AppendLine("│");
                sb.AppendLine("│  Thứ tự sản xuất:");

                Product prev = null;
                foreach (var product in group.Products)
                {
                    string changeInfo = "";
                    if (prev != null)
                    {
                        var changeCount = product.GetChangeoverComponentCount(prev);
                        var changeTime = CalculateChangeoverTime(prev, product);
                        if (changeCount > 0)
                            changeInfo = $" [Thay {changeCount} LK, {changeTime:F0}ph]";
                    }
                    sb.AppendLine($"│    → {product.Name} (SL: {product.RequiredQuantity}, DL: {product.DueDate:dd/MM}){changeInfo}");
                    prev = product;
                }

                sb.AppendLine("└───────────────────────────────────────────────────────────");
                sb.AppendLine();
            }

            // Tổng kết
            sb.AppendLine("─── TỔNG KẾT ───");
            sb.AppendLine($"Tổng số nhóm: {groups.Count}");
            sb.AppendLine($"Tổng số lần changeover: {groups.Sum(g => g.TotalChangeoverCount)}");
            sb.AppendLine($"Tổng thời gian changeover: {groups.Sum(g => g.TotalChangeoverTimeMinutes):F0} phút");

            return sb.ToString();
        }
    }
}
