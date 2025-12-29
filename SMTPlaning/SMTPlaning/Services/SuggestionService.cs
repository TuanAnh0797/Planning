using System;
using System.Collections.Generic;
using System.Linq;
using SMTScheduler.Models;

namespace SMTScheduler.Services
{
    /// <summary>
    /// Cấu hình cho SuggestionService
    /// </summary>
    public class SuggestionConfig
    {
        /// <summary>
        /// Thời gian training mặc định cho người mới (ngày làm việc)
        /// </summary>
        public int DefaultTrainingDays { get; set; } = 14;

        /// <summary>
        /// Hệ số lương OT ngày thường
        /// </summary>
        public decimal WeekdayOTMultiplier { get; set; } = 1.5m;

        /// <summary>
        /// Hệ số lương OT thứ 7
        /// </summary>
        public decimal SaturdayOTMultiplier { get; set; } = 2.0m;

        /// <summary>
        /// Hệ số lương OT Chủ nhật
        /// </summary>
        public decimal SundayOTMultiplier { get; set; } = 3.0m;

        /// <summary>
        /// Hệ số lương OT ngày lễ
        /// </summary>
        public decimal HolidayOTMultiplier { get; set; } = 4.0m;

        /// <summary>
        /// Số giờ OT tối đa mỗi ngày
        /// </summary>
        public double MaxOTHoursPerDay { get; set; } = 4;

        /// <summary>
        /// Số giờ làm việc ngày OT weekend
        /// </summary>
        public double WeekendWorkHours { get; set; } = 8;

        /// <summary>
        /// Lương giờ trung bình (VND)
        /// </summary>
        public decimal AverageHourlyRate { get; set; } = 50000;

        /// <summary>
        /// Chi phí tuyển dụng mỗi người (VND)
        /// </summary>
        public decimal RecruitmentCostPerPerson { get; set; } = 2000000;

        /// <summary>
        /// Chi phí training mỗi ngày (VND)
        /// </summary>
        public decimal TrainingCostPerDay { get; set; } = 200000;

        /// <summary>
        /// Số ngày trước deadline bắt đầu xem xét OT
        /// </summary>
        public int OTConsiderationDaysBeforeDeadline { get; set; } = 7;
    }

    /// <summary>
    /// Service đưa ra gợi ý khi không đủ năng lực sản xuất
    /// </summary>
    public class SuggestionService
    {
        private readonly List<Stage> _stages;
        private readonly List<Line> _lines;
        private readonly List<Operator> _operators;
        private readonly WorkingCalendar _calendar;
        private readonly SuggestionConfig _config;

        public SuggestionService(
            List<Stage> stages,
            List<Line> lines,
            List<Operator> operators,
            WorkingCalendar calendar,
            SuggestionConfig config = null)
        {
            _stages = stages;
            _lines = lines;
            _operators = operators ?? new List<Operator>();
            _calendar = calendar;
            _config = config ?? new SuggestionConfig();
        }

        /// <summary>
        /// Phân tích năng lực và đưa ra gợi ý
        /// </summary>
        public SuggestionReport AnalyzeAndSuggest(
            List<Product> products,
            ScheduleResult scheduleResult,
            DateTime referenceDate)
        {
            var report = new SuggestionReport();
            report.Suggestions = new List<Suggestion>();

            // Lọc sản phẩm cần sản xuất
            var productsToSchedule = products.Where(p => p.RequiredQuantity > 0).ToList();
            if (!productsToSchedule.Any())
            {
                report.OverallStatus = "Không có sản phẩm cần sản xuất";
                report.IsFeasibleWithSuggestions = true;
                return report;
            }

            // Phân tích năng lực operator theo công đoạn
            report.CapacityAnalyses = AnalyzeOperatorCapacity(productsToSchedule, referenceDate);

            // Xác định sản phẩm có nguy cơ trễ
            var productsAtRisk = new List<Product>();
            if (scheduleResult != null && scheduleResult.MissedDeadlines.Any())
            {
                productsAtRisk = productsToSchedule
                    .Where(p => scheduleResult.MissedDeadlines.Any(m => m.ProductId == p.Id))
                    .ToList();
            }
            else
            {
                // Ước tính sản phẩm có thể trễ dựa trên năng lực
                productsAtRisk = EstimateProductsAtRisk(productsToSchedule, referenceDate);
            }

            report.ProductsAtRisk = productsAtRisk.Count;
            report.TotalShortfallHours = report.CapacityAnalyses.Sum(c => c.ShortfallHours);
            report.TotalShortfallMinutes = report.TotalShortfallHours * 60;

            // Đưa ra gợi ý
            if (report.TotalShortfallHours > 0 || productsAtRisk.Any())
            {
                // 1. Gợi ý OT
                report.OTSuggestions = GenerateOTSuggestions(
                    productsAtRisk, report.CapacityAnalyses, referenceDate);
                
                // Chuyển OT suggestions thành Suggestion chung
                foreach (var otSuggestion in report.OTSuggestions)
                {
                    // Xác định loại OT dựa trên Type đã có
                    SuggestionType otType = SuggestionType.OvertimeWeekday;
                    if (otSuggestion.Type == SuggestionType.SaturdayWork || 
                        otSuggestion.Type == SuggestionType.SundayWork)
                    {
                        otType = SuggestionType.OvertimeWeekend;
                    }
                    else if (otSuggestion.Type == SuggestionType.HolidayWork)
                    {
                        otType = SuggestionType.OvertimeHoliday;
                    }

                    report.Suggestions.Add(new Suggestion
                    {
                        Id = otSuggestion.Id,
                        Type = otType,
                        Title = otSuggestion.Title,
                        Description = otSuggestion.Description,
                        Priority = otSuggestion.Priority,
                        Feasibility = otSuggestion.Feasibility,
                        EstimatedCost = otSuggestion.EstimatedCost,
                        TimeGainMinutes = (int)(otSuggestion.HoursGained * 60),
                        AffectedLineId = otSuggestion.LineId,
                        AffectedStageId = otSuggestion.StageId?.ToString()
                    });
                }

                // 2. Gợi ý tuyển dụng
                report.HiringSuggestions = GenerateHiringSuggestions(
                    report.CapacityAnalyses, referenceDate);
                
                foreach (var hiringSuggestion in report.HiringSuggestions)
                {
                    report.Suggestions.Add(new Suggestion
                    {
                        Id = hiringSuggestion.Id,
                        Type = SuggestionType.Hiring,
                        Title = hiringSuggestion.Title,
                        Description = hiringSuggestion.Description,
                        Priority = hiringSuggestion.Priority,
                        Feasibility = hiringSuggestion.Feasibility,
                        EstimatedCost = hiringSuggestion.EstimatedCost,
                        TimeGainMinutes = (int)(hiringSuggestion.HoursGained * 60)
                    });
                }

                // 3. Gợi ý training
                report.TrainingSuggestions = GenerateTrainingSuggestions(
                    report.CapacityAnalyses, referenceDate);
                
                foreach (var trainingSuggestion in report.TrainingSuggestions)
                {
                    report.Suggestions.Add(new Suggestion
                    {
                        Id = trainingSuggestion.Id,
                        Type = SuggestionType.Training,
                        Title = trainingSuggestion.Title,
                        Description = trainingSuggestion.Description,
                        Priority = trainingSuggestion.Priority,
                        Feasibility = trainingSuggestion.Feasibility,
                        EstimatedCost = trainingSuggestion.EstimatedCost,
                        TimeGainMinutes = (int)(trainingSuggestion.HoursGained * 60)
                    });
                }

                // 4. Gợi ý khác
                report.OtherSuggestions = GenerateOtherSuggestions(
                    productsAtRisk, report.CapacityAnalyses);
                report.Suggestions.AddRange(report.OtherSuggestions);

                // Tính tổng chi phí
                report.TotalEstimatedCost = 
                    report.OTSuggestions.Sum(o => o.EstimatedCost) +
                    report.HiringSuggestions.Sum(h => h.EstimatedCost) +
                    report.TrainingSuggestions.Sum(t => t.EstimatedCost);

                // Đánh giá khả thi
                double hoursCanRecover = 
                    report.OTSuggestions.Sum(o => o.HoursGained) +
                    report.HiringSuggestions.Sum(h => h.HoursGained);

                report.IsFeasibleWithSuggestions = hoursCanRecover >= report.TotalShortfallHours;
                
                report.OverallStatus = report.IsFeasibleWithSuggestions
                    ? $"Thiếu {report.TotalShortfallHours:F0}h - CÓ THỂ GIẢI QUYẾT với OT/Tuyển dụng"
                    : $"Thiếu {report.TotalShortfallHours:F0}h - CẦN XEM XÉT THÊM GIẢI PHÁP";
                
                report.Summary = report.OverallStatus;
            }
            else
            {
                report.OverallStatus = "Năng lực đủ đáp ứng";
                report.Summary = "Năng lực đủ đáp ứng";
                report.IsFeasibleWithSuggestions = true;
            }

            return report;
        }

        /// <summary>
        /// Phân tích năng lực operator theo công đoạn
        /// </summary>
        public List<OperatorCapacityAnalysis> AnalyzeOperatorCapacity(
            List<Product> products,
            DateTime referenceDate)
        {
            var analyses = new List<OperatorCapacityAnalysis>();
            var maxDeadline = products.Max(p => p.DueDate);
            var minStart = products.Min(p => p.StartDate);

            foreach (var stage in _stages)
            {
                var analysis = new OperatorCapacityAnalysis
                {
                    StageId = stage.Id,
                    StageName = stage.Name
                };

                // Đếm operator có kỹ năng cho công đoạn này
                var capableOperators = _operators
                    .Where(o => o.IsActive && o.HasSkillFor(stage.Id))
                    .ToList();

                analysis.TotalOperators = capableOperators.Count;
                analysis.IndependentOperators = capableOperators
                    .Count(o => o.CanWorkIndependentlyOn(stage.Id));
                analysis.TrainingOperators = capableOperators
                    .Count(o => o.GetSkillLevel(stage.Id) == SkillLevel.Training);

                // Tính tổng giờ khả dụng
                double totalAvailableHours = 0;
                foreach (var op in capableOperators.Where(o => o.CanWorkIndependentlyOn(stage.Id)))
                {
                    for (var date = minStart; date <= maxDeadline; date = date.AddDays(1))
                    {
                        if (_calendar.IsWorkingDay(date) && op.IsAvailableOn(date))
                        {
                            var shift = _calendar.GetWorkShift(date);
                            double efficiency = op.GetEfficiencyFor(stage.Id);
                            totalAvailableHours += (shift.WorkingMinutes / 60.0) * efficiency;
                        }
                    }
                }
                analysis.TotalAvailableHours = totalAvailableHours;

                // Tính tổng giờ cần thiết (ước tính)
                double totalRequiredHours = 0;
                foreach (var product in products)
                {
                    var fastestLine = _lines
                        .Where(l => l.SupportsStage(stage.Id))
                        .OrderBy(l => l.GetActualCycleTime(stage.Id))
                        .FirstOrDefault();

                    if (fastestLine != null)
                    {
                        double processingMinutes = fastestLine.CalculateProcessingTime(
                            stage.Id, product.RequiredQuantity);
                        totalRequiredHours += processingMinutes / 60.0;
                    }
                }
                analysis.TotalRequiredHours = totalRequiredHours;

                // Ước tính số người cần thêm
                if (analysis.HasShortfall)
                {
                    int workingDays = _calendar.GetWorkingDayCount(minStart, maxDeadline);
                    double hoursPerPersonPerDay = _calendar.DefaultShift.WorkingMinutes / 60.0;
                    double totalHoursPerPerson = hoursPerPersonPerDay * workingDays;

                    if (totalHoursPerPerson > 0)
                    {
                        analysis.EstimatedHiringNeed = (int)Math.Ceiling(
                            analysis.ShortfallHours / totalHoursPerPerson);
                    }
                }

                analyses.Add(analysis);
            }

            return analyses;
        }

        /// <summary>
        /// Ước tính sản phẩm có nguy cơ trễ
        /// </summary>
        private List<Product> EstimateProductsAtRisk(List<Product> products, DateTime referenceDate)
        {
            var atRisk = new List<Product>();

            foreach (var product in products.OrderBy(p => p.DueDate))
            {
                // Tính thời gian tối thiểu cần
                double minProcessingHours = 0;
                foreach (var stage in _stages)
                {
                    var fastestLine = _lines
                        .Where(l => l.SupportsStage(stage.Id))
                        .OrderBy(l => l.GetActualCycleTime(stage.Id))
                        .FirstOrDefault();

                    if (fastestLine != null)
                    {
                        minProcessingHours += fastestLine.CalculateProcessingTime(
                            stage.Id, product.RequiredQuantity) / 60.0;
                    }
                }

                // Tính giờ khả dụng
                int workingMinutes = _calendar.GetTotalWorkingMinutes(
                    product.StartDate, product.DueDate);
                double availableHours = workingMinutes / 60.0;

                // Xét thêm yếu tố operator
                double operatorFactor = 1.0;
                foreach (var stage in _stages)
                {
                    var capableOps = _operators.Count(o => 
                        o.IsActive && o.CanWorkIndependentlyOn(stage.Id));
                    if (capableOps == 0)
                    {
                        operatorFactor = 0;
                        break;
                    }
                    else if (capableOps < 2)
                    {
                        operatorFactor = Math.Min(operatorFactor, 0.7);
                    }
                }

                double effectiveAvailable = availableHours * operatorFactor;

                if (minProcessingHours > effectiveAvailable * 0.9) // 90% threshold
                {
                    atRisk.Add(product);
                }
            }

            return atRisk;
        }

        /// <summary>
        /// Tạo gợi ý OT
        /// </summary>
        private List<OTSuggestionDetail> GenerateOTSuggestions(
            List<Product> productsAtRisk,
            List<OperatorCapacityAnalysis> capacityAnalyses,
            DateTime referenceDate)
        {
            var suggestions = new List<OTSuggestionDetail>();

            if (!productsAtRisk.Any()) return suggestions;

            var maxDeadline = productsAtRisk.Max(p => p.DueDate);
            var shortfallStages = capacityAnalyses.Where(c => c.HasShortfall).ToList();

            // 1. Gợi ý OT ngày thường (sau giờ làm)
            var weekdayOT = GenerateWeekdayOTSuggestion(
                shortfallStages, referenceDate, maxDeadline, productsAtRisk);
            if (weekdayOT != null && weekdayOT.OTDays.Any())
            {
                suggestions.Add(weekdayOT);
            }

            // 2. Gợi ý làm thứ 7
            var saturdayOT = GenerateWeekendOTSuggestion(
                shortfallStages, referenceDate, maxDeadline, productsAtRisk, DayOfWeek.Saturday);
            if (saturdayOT != null && saturdayOT.OTDays.Any())
            {
                suggestions.Add(saturdayOT);
            }

            // 3. Gợi ý làm Chủ nhật
            var sundayOT = GenerateWeekendOTSuggestion(
                shortfallStages, referenceDate, maxDeadline, productsAtRisk, DayOfWeek.Sunday);
            if (sundayOT != null && sundayOT.OTDays.Any())
            {
                suggestions.Add(sundayOT);
            }

            // 4. Gợi ý làm ngày lễ
            var holidayOT = GenerateHolidayOTSuggestion(
                shortfallStages, referenceDate, maxDeadline, productsAtRisk);
            if (holidayOT != null && holidayOT.OTDays.Any())
            {
                suggestions.Add(holidayOT);
            }

            return suggestions;
        }

        private OTSuggestionDetail GenerateWeekdayOTSuggestion(
            List<OperatorCapacityAnalysis> shortfallStages,
            DateTime startDate, DateTime endDate,
            List<Product> productsAtRisk)
        {
            var suggestion = new OTSuggestionDetail
            {
                Id = "OT-WEEKDAY",
                Type = SuggestionType.WeekdayOT,
                Title = "Tăng ca ngày thường (sau giờ làm)",
                Priority = 1,
                Feasibility = FeasibilityLevel.Easy,
                AffectedProductIds = productsAtRisk.Select(p => p.Id).ToList()
            };

            double totalShortfall = shortfallStages.Sum(s => s.ShortfallHours);
            double recoveredHours = 0;

            for (var date = startDate; date <= endDate && recoveredHours < totalShortfall; date = date.AddDays(1))
            {
                if (!_calendar.IsWorkingDay(date)) continue;

                foreach (var stageAnalysis in shortfallStages)
                {
                    var availableOps = _operators
                        .Where(o => o.IsActive && 
                                   o.CanWorkIndependentlyOn(stageAnalysis.StageId) &&
                                   o.CanOTOn(date))
                        .ToList();

                    if (!availableOps.Any()) continue;

                    var dayDetail = new OTDayDetail
                    {
                        Date = date,
                        OTHours = _config.MaxOTHoursPerDay,
                        OperatorCount = Math.Min(availableOps.Count, 3), // Tối đa 3 người OT
                        StageId = stageAnalysis.StageId,
                        StageName = stageAnalysis.StageName,
                        AvailableOperatorIds = availableOps.Select(o => o.Id).ToList(),
                        AvailableOperatorNames = availableOps.Select(o => o.Name).ToList(),
                        OTMultiplier = _config.WeekdayOTMultiplier,
                        EstimatedCost = (decimal)(_config.MaxOTHoursPerDay * 
                                       Math.Min(availableOps.Count, 3)) * 
                                       _config.AverageHourlyRate * _config.WeekdayOTMultiplier
                    };

                    // Tính sản lượng dự kiến
                    var line = _lines.FirstOrDefault(l => l.SupportsStage(stageAnalysis.StageId));
                    if (line != null)
                    {
                        double cycleTime = line.GetActualCycleTime(stageAnalysis.StageId);
                        if (cycleTime > 0)
                        {
                            dayDetail.ExpectedOutput = (int)((dayDetail.OTHours * 60 * 
                                                            dayDetail.OperatorCount) / cycleTime);
                        }
                    }

                    suggestion.OTDays.Add(dayDetail);
                    recoveredHours += dayDetail.OTHours * dayDetail.OperatorCount;
                }
            }

            suggestion.HoursGained = recoveredHours;
            suggestion.EstimatedCost = suggestion.OTDays.Sum(d => d.EstimatedCost);
            suggestion.AdditionalCapacity = suggestion.OTDays.Sum(d => d.ExpectedOutput);
            suggestion.Description = $"Tăng ca {_config.MaxOTHoursPerDay}h/ngày sau giờ làm, " +
                                    $"tổng {suggestion.TotalOTHours:F0}h OT trong {suggestion.OTDays.Count} ngày";

            return suggestion;
        }

        private OTSuggestionDetail GenerateWeekendOTSuggestion(
            List<OperatorCapacityAnalysis> shortfallStages,
            DateTime startDate, DateTime endDate,
            List<Product> productsAtRisk,
            DayOfWeek targetDay)
        {
            bool isSaturday = targetDay == DayOfWeek.Saturday;
            var suggestion = new OTSuggestionDetail
            {
                Id = isSaturday ? "OT-SAT" : "OT-SUN",
                Type = isSaturday ? SuggestionType.SaturdayWork : SuggestionType.SundayWork,
                Title = isSaturday ? "Làm việc Thứ Bảy" : "Làm việc Chủ Nhật",
                Priority = isSaturday ? 2 : 3,
                Feasibility = isSaturday ? FeasibilityLevel.Moderate : FeasibilityLevel.Difficult,
                AffectedProductIds = productsAtRisk.Select(p => p.Id).ToList()
            };

            decimal otMultiplier = isSaturday ? _config.SaturdayOTMultiplier : _config.SundayOTMultiplier;

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != targetDay) continue;

                foreach (var stageAnalysis in shortfallStages)
                {
                    var availableOps = _operators
                        .Where(o => o.IsActive &&
                                   o.CanWorkIndependentlyOn(stageAnalysis.StageId) &&
                                   o.CanOTOn(date))
                        .ToList();

                    if (!availableOps.Any()) continue;

                    var dayDetail = new OTDayDetail
                    {
                        Date = date,
                        OTHours = _config.WeekendWorkHours,
                        OperatorCount = availableOps.Count,
                        StageId = stageAnalysis.StageId,
                        StageName = stageAnalysis.StageName,
                        AvailableOperatorIds = availableOps.Select(o => o.Id).ToList(),
                        AvailableOperatorNames = availableOps.Select(o => o.Name).ToList(),
                        OTMultiplier = otMultiplier,
                        EstimatedCost = (decimal)(_config.WeekendWorkHours * availableOps.Count) *
                                       _config.AverageHourlyRate * otMultiplier
                    };

                    var line = _lines.FirstOrDefault(l => l.SupportsStage(stageAnalysis.StageId));
                    if (line != null)
                    {
                        double cycleTime = line.GetActualCycleTime(stageAnalysis.StageId);
                        if (cycleTime > 0)
                        {
                            dayDetail.ExpectedOutput = (int)((dayDetail.OTHours * 60 *
                                                            dayDetail.OperatorCount) / cycleTime);
                        }
                    }

                    suggestion.OTDays.Add(dayDetail);
                }
            }

            suggestion.HoursGained = suggestion.OTDays.Sum(d => d.OTHours * d.OperatorCount);
            suggestion.EstimatedCost = suggestion.OTDays.Sum(d => d.EstimatedCost);
            suggestion.AdditionalCapacity = suggestion.OTDays.Sum(d => d.ExpectedOutput);
            suggestion.Description = $"Làm {(isSaturday ? "Thứ Bảy" : "Chủ Nhật")} " +
                                    $"{_config.WeekendWorkHours}h/ngày, " +
                                    $"tổng {suggestion.OTDays.Count} ngày";

            return suggestion;
        }

        private OTSuggestionDetail GenerateHolidayOTSuggestion(
            List<OperatorCapacityAnalysis> shortfallStages,
            DateTime startDate, DateTime endDate,
            List<Product> productsAtRisk)
        {
            var suggestion = new OTSuggestionDetail
            {
                Id = "OT-HOLIDAY",
                Type = SuggestionType.HolidayWork,
                Title = "Làm việc ngày lễ",
                Priority = 4,
                Feasibility = FeasibilityLevel.VeryDifficult,
                AffectedProductIds = productsAtRisk.Select(p => p.Id).ToList()
            };

            var holidays = _calendar.GetHolidaysInRange(startDate, endDate)
                .Where(h => h.Type == HolidayType.PublicHoliday || h.Type == HolidayType.CompanyHoliday)
                .ToList();

            foreach (var holiday in holidays)
            {
                foreach (var stageAnalysis in shortfallStages)
                {
                    var availableOps = _operators
                        .Where(o => o.IsActive &&
                                   o.CanWorkIndependentlyOn(stageAnalysis.StageId) &&
                                   o.CanWorkOT)
                        .ToList();

                    if (!availableOps.Any()) continue;

                    var dayDetail = new OTDayDetail
                    {
                        Date = holiday.Date,
                        IsHoliday = true,
                        HolidayName = holiday.Name,
                        OTHours = _config.WeekendWorkHours,
                        OperatorCount = availableOps.Count / 2, // Giả sử 50% đồng ý làm lễ
                        StageId = stageAnalysis.StageId,
                        StageName = stageAnalysis.StageName,
                        AvailableOperatorIds = availableOps.Take(availableOps.Count / 2).Select(o => o.Id).ToList(),
                        AvailableOperatorNames = availableOps.Take(availableOps.Count / 2).Select(o => o.Name).ToList(),
                        OTMultiplier = _config.HolidayOTMultiplier,
                        EstimatedCost = (decimal)(_config.WeekendWorkHours * (availableOps.Count / 2)) *
                                       _config.AverageHourlyRate * _config.HolidayOTMultiplier
                    };

                    if (dayDetail.OperatorCount > 0)
                    {
                        suggestion.OTDays.Add(dayDetail);
                    }
                }
            }

            suggestion.HoursGained = suggestion.OTDays.Sum(d => d.OTHours * d.OperatorCount);
            suggestion.EstimatedCost = suggestion.OTDays.Sum(d => d.EstimatedCost);
            suggestion.AdditionalCapacity = suggestion.OTDays.Sum(d => d.ExpectedOutput);
            suggestion.Description = $"Làm việc các ngày lễ, lương x{_config.HolidayOTMultiplier}";
            suggestion.Risks.Add("Khó tìm người đồng ý làm ngày lễ");
            suggestion.Risks.Add("Chi phí rất cao");

            return suggestion;
        }

        /// <summary>
        /// Tạo gợi ý tuyển dụng
        /// </summary>
        private List<HiringSuggestionDetail> GenerateHiringSuggestions(
            List<OperatorCapacityAnalysis> capacityAnalyses,
            DateTime referenceDate)
        {
            var suggestions = new List<HiringSuggestionDetail>();

            var shortfallStages = capacityAnalyses.Where(c => c.HasShortfall).ToList();
            if (!shortfallStages.Any()) return suggestions;

            var hiringSuggestion = new HiringSuggestionDetail
            {
                Id = "HIRE-01",
                Title = "Tuyển dụng nhân sự bổ sung",
                Priority = 2,
                Feasibility = FeasibilityLevel.Moderate,
                Description = "Tuyển thêm operator cho các công đoạn thiếu người"
            };

            foreach (var stageAnalysis in shortfallStages)
            {
                if (stageAnalysis.EstimatedHiringNeed <= 0) continue;

                // Tìm trainer có thể đào tạo
                var trainer = _operators.FirstOrDefault(o =>
                    o.IsActive && o.GetSkillLevel(stageAnalysis.StageId) == SkillLevel.Expert);

                // Tính ngày muộn nhất phải tuyển
                // Cần training trước khi có thể làm việc
                var neededByDate = referenceDate.AddDays(30); // Giả sử cần trong 30 ngày
                var latestHireDate = _calendar.AddWorkingDays(
                    referenceDate, -_config.DefaultTrainingDays);
                if (latestHireDate < referenceDate)
                {
                    latestHireDate = referenceDate; // Tuyển ngay
                }

                var position = new HiringPosition
                {
                    PositionTitle = $"Operator {stageAnalysis.StageName}",
                    Headcount = stageAnalysis.EstimatedHiringNeed,
                    StageId = stageAnalysis.StageId,
                    StageName = stageAnalysis.StageName,
                    RequiredInitialSkill = SkillLevel.None,
                    TargetSkillAfterTraining = SkillLevel.Basic,
                    TrainingDays = _config.DefaultTrainingDays,
                    LatestHireDate = latestHireDate,
                    EffectiveDate = _calendar.AddWorkingDays(latestHireDate, _config.DefaultTrainingDays),
                    ProposedSalary = 8000000, // 8 triệu/tháng
                    ExperienceRequirement = "Không yêu cầu kinh nghiệm, sẽ được đào tạo",
                    TrainerId = trainer?.Id,
                    TrainerName = trainer?.Name ?? "Cần tìm trainer",
                    Reason = $"Thiếu {stageAnalysis.ShortfallHours:F0} giờ công cho {stageAnalysis.StageName}"
                };

                hiringSuggestion.Positions.Add(position);
            }

            if (hiringSuggestion.Positions.Any())
            {
                hiringSuggestion.RecruitmentCost = hiringSuggestion.TotalHeadcount * 
                                                   _config.RecruitmentCostPerPerson;
                hiringSuggestion.TrainingCost = hiringSuggestion.TotalHeadcount *
                                                _config.DefaultTrainingDays * _config.TrainingCostPerDay;
                hiringSuggestion.FirstMonthSalaryCost = hiringSuggestion.Positions
                    .Sum(p => p.Headcount * p.ProposedSalary);
                
                hiringSuggestion.EstimatedCost = hiringSuggestion.RecruitmentCost +
                                                 hiringSuggestion.TrainingCost +
                                                 hiringSuggestion.FirstMonthSalaryCost;

                // Tính giờ công sẽ có thêm
                double hoursPerPerson = _calendar.DefaultShift.WorkingMinutes / 60.0 * 20; // 20 ngày làm việc/tháng
                hiringSuggestion.HoursGained = hiringSuggestion.TotalHeadcount * hoursPerPerson * 0.7; // 70% hiệu suất ban đầu

                hiringSuggestion.Prerequisites.Add("Phê duyệt ngân sách tuyển dụng");
                hiringSuggestion.Prerequisites.Add("Xác định nguồn tuyển dụng");
                hiringSuggestion.Risks.Add("Thời gian tuyển dụng có thể kéo dài");
                hiringSuggestion.Risks.Add("Người mới cần thời gian thích nghi");

                suggestions.Add(hiringSuggestion);
            }

            return suggestions;
        }

        /// <summary>
        /// Tạo gợi ý training nâng cao
        /// </summary>
        private List<TrainingSuggestionDetail> GenerateTrainingSuggestions(
            List<OperatorCapacityAnalysis> capacityAnalyses,
            DateTime referenceDate)
        {
            var suggestions = new List<TrainingSuggestionDetail>();

            // Tìm operator có thể training thêm kỹ năng
            var operatorsToTrain = new List<TrainingPlan>();

            foreach (var stageAnalysis in capacityAnalyses.Where(c => c.HasShortfall))
            {
                // Tìm operator đang Training -> nâng lên Basic
                var trainingOps = _operators.Where(o =>
                    o.IsActive &&
                    o.GetSkillLevel(stageAnalysis.StageId) == SkillLevel.Training)
                    .ToList();

                foreach (var op in trainingOps)
                {
                    operatorsToTrain.Add(new TrainingPlan
                    {
                        OperatorId = op.Id,
                        OperatorName = op.Name,
                        StageId = stageAnalysis.StageId,
                        StageName = stageAnalysis.StageName,
                        CurrentSkill = SkillLevel.Training,
                        TargetSkill = SkillLevel.Basic,
                        TrainingDays = 7,
                        StartDate = referenceDate,
                        CompletionDate = _calendar.AddWorkingDays(referenceDate, 7),
                        Cost = 7 * _config.TrainingCostPerDay
                    });
                }

                // Tìm operator Basic -> nâng lên Proficient
                var basicOps = _operators.Where(o =>
                    o.IsActive &&
                    o.GetSkillLevel(stageAnalysis.StageId) == SkillLevel.Basic)
                    .Take(2)
                    .ToList();

                foreach (var op in basicOps)
                {
                    operatorsToTrain.Add(new TrainingPlan
                    {
                        OperatorId = op.Id,
                        OperatorName = op.Name,
                        StageId = stageAnalysis.StageId,
                        StageName = stageAnalysis.StageName,
                        CurrentSkill = SkillLevel.Basic,
                        TargetSkill = SkillLevel.Proficient,
                        TrainingDays = 14,
                        StartDate = referenceDate,
                        CompletionDate = _calendar.AddWorkingDays(referenceDate, 14),
                        Cost = 14 * _config.TrainingCostPerDay
                    });
                }
            }

            if (operatorsToTrain.Any())
            {
                var trainingSuggestion = new TrainingSuggestionDetail
                {
                    Id = "TRAIN-01",
                    Title = "Training nâng cao kỹ năng",
                    Priority = 3,
                    Feasibility = FeasibilityLevel.Easy,
                    Description = "Nâng cao kỹ năng cho operator hiện tại",
                    TrainingPlans = operatorsToTrain,
                    EstimatedCost = operatorsToTrain.Sum(t => t.Cost)
                };

                trainingSuggestion.HoursGained = operatorsToTrain.Count * 20; // Ước tính
                suggestions.Add(trainingSuggestion);
            }

            return suggestions;
        }

        /// <summary>
        /// Tạo gợi ý khác
        /// </summary>
        private List<Suggestion> GenerateOtherSuggestions(
            List<Product> productsAtRisk,
            List<OperatorCapacityAnalysis> capacityAnalyses)
        {
            var suggestions = new List<Suggestion>();

            double totalShortfall = capacityAnalyses.Sum(c => c.ShortfallHours);

            // Gợi ý thương lượng deadline
            if (productsAtRisk.Any())
            {
                suggestions.Add(new Suggestion
                {
                    Id = "NEGOTIATE-DL",
                    Type = SuggestionType.NegotiateDeadline,
                    Title = "Thương lượng lại deadline với khách hàng",
                    Priority = 3,
                    Feasibility = FeasibilityLevel.Moderate,
                    Description = $"Đề xuất dời deadline cho {productsAtRisk.Count} sản phẩm có nguy cơ trễ",
                    AffectedProductIds = productsAtRisk.Select(p => p.Id).ToList(),
                    EstimatedCost = 0,
                    Risks = new List<string> { "Có thể ảnh hưởng quan hệ khách hàng" }
                });
            }

            // Gợi ý chia nhỏ đơn hàng
            var largeOrders = productsAtRisk.Where(p => p.RequiredQuantity > 300).ToList();
            if (largeOrders.Any())
            {
                suggestions.Add(new Suggestion
                {
                    Id = "SPLIT-ORDER",
                    Type = SuggestionType.SplitOrder,
                    Title = "Chia nhỏ đơn hàng thành nhiều đợt",
                    Priority = 4,
                    Feasibility = FeasibilityLevel.Easy,
                    Description = $"Chia {largeOrders.Count} đơn hàng lớn thành nhiều đợt giao",
                    AffectedProductIds = largeOrders.Select(p => p.Id).ToList(),
                    EstimatedCost = 0
                });
            }

            // Gợi ý thuê ngoài nếu thiếu nhiều
            if (totalShortfall > 100)
            {
                suggestions.Add(new Suggestion
                {
                    Id = "OUTSOURCE",
                    Type = SuggestionType.Outsource,
                    Title = "Thuê ngoài một phần công việc",
                    Priority = 5,
                    Feasibility = FeasibilityLevel.Difficult,
                    Description = $"Thuê gia công bên ngoài cho ~{totalShortfall:F0} giờ công thiếu hụt",
                    EstimatedCost = (decimal)totalShortfall * _config.AverageHourlyRate * 2, // Giả sử giá gấp đôi
                    Risks = new List<string> 
                    { 
                        "Cần kiểm soát chất lượng",
                        "Chi phí cao hơn",
                        "Rủi ro về tiến độ"
                    }
                });
            }

            return suggestions;
        }
    }
}
