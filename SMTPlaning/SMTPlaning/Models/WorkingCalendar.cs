using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Loại ngày nghỉ
    /// </summary>
    public enum HolidayType
    {
        /// <summary>Nghỉ lễ (Tết, Quốc khánh...)</summary>
        PublicHoliday,
        
        /// <summary>Nghỉ cuối tuần (Thứ 7, CN)</summary>
        Weekend,
        
        /// <summary>Nghỉ bảo trì máy móc</summary>
        Maintenance,
        
        /// <summary>Nghỉ khác (công ty quyết định)</summary>
        CompanyHoliday,
        
        /// <summary>Nghỉ đột xuất</summary>
        Unplanned
    }

    /// <summary>
    /// Thông tin một ngày nghỉ
    /// </summary>
    public class Holiday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; }
        public HolidayType Type { get; set; }
        
        /// <summary>
        /// Áp dụng cho Line cụ thể (null = tất cả)
        /// </summary>
        public string LineId { get; set; }
        
        /// <summary>
        /// Nghỉ cả ngày hay một phần
        /// </summary>
        public bool IsFullDay { get; set; } = true;
        
        /// <summary>
        /// Nếu nghỉ một phần: giờ bắt đầu nghỉ
        /// </summary>
        public TimeSpan? PartialStartTime { get; set; }
        
        /// <summary>
        /// Nếu nghỉ một phần: giờ kết thúc nghỉ
        /// </summary>
        public TimeSpan? PartialEndTime { get; set; }

        public Holiday() { }

        public Holiday(DateTime date, string name, HolidayType type, string lineId = null)
        {
            Date = date.Date;
            Name = name;
            Type = type;
            LineId = lineId;
            IsFullDay = true;
        }

        /// <summary>
        /// Tạo ngày nghỉ một phần (VD: nghỉ buổi chiều)
        /// </summary>
        public static Holiday CreatePartialHoliday(DateTime date, string name, HolidayType type,
            TimeSpan startTime, TimeSpan endTime, string lineId = null)
        {
            return new Holiday
            {
                Date = date.Date,
                Name = name,
                Type = type,
                LineId = lineId,
                IsFullDay = false,
                PartialStartTime = startTime,
                PartialEndTime = endTime
            };
        }

        public override string ToString()
        {
            string lineInfo = LineId != null ? $" (Line: {LineId})" : " (Tất cả)";
            string timeInfo = IsFullDay ? "Cả ngày" : $"{PartialStartTime:hh\\:mm}-{PartialEndTime:hh\\:mm}";
            return $"{Date:dd/MM/yyyy} - {Name} [{Type}] {timeInfo}{lineInfo}";
        }
    }

    /// <summary>
    /// Ca làm việc trong ngày
    /// </summary>
    public class WorkShift
    {
        public string Name { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        /// <summary>
        /// Thời gian nghỉ giữa ca (phút)
        /// </summary>
        public int BreakMinutes { get; set; } = 0;

        /// <summary>
        /// Tổng số phút làm việc trong ca
        /// </summary>
        public int WorkingMinutes => (int)(EndTime - StartTime).TotalMinutes - BreakMinutes;

        public WorkShift() { }

        public WorkShift(string name, TimeSpan startTime, TimeSpan endTime, int breakMinutes = 0)
        {
            Name = name;
            StartTime = startTime;
            EndTime = endTime;
            BreakMinutes = breakMinutes;
        }

        /// <summary>
        /// Ca mặc định 8 giờ (8:00 - 17:00, nghỉ trưa 1 tiếng)
        /// </summary>
        public static WorkShift Default => new WorkShift("Ca ngày", 
            new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);

        /// <summary>
        /// Ca sáng (6:00 - 14:00)
        /// </summary>
        public static WorkShift Morning => new WorkShift("Ca sáng",
            new TimeSpan(6, 0, 0), new TimeSpan(14, 0, 0), 30);

        /// <summary>
        /// Ca chiều (14:00 - 22:00)
        /// </summary>
        public static WorkShift Afternoon => new WorkShift("Ca chiều",
            new TimeSpan(14, 0, 0), new TimeSpan(22, 0, 0), 30);

        /// <summary>
        /// Ca đêm (22:00 - 6:00)
        /// </summary>
        public static WorkShift Night => new WorkShift("Ca đêm",
            new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 30);

        public override string ToString()
        {
            return $"{Name}: {StartTime:hh\\:mm}-{EndTime:hh\\:mm} ({WorkingMinutes} phút)";
        }
    }

    /// <summary>
    /// Lịch làm việc - Quản lý ngày nghỉ và thời gian làm việc
    /// </summary>
    public class WorkingCalendar
    {
        /// <summary>
        /// Danh sách ngày nghỉ
        /// </summary>
        public List<Holiday> Holidays { get; set; } = new List<Holiday>();

        /// <summary>
        /// Các ngày trong tuần được làm việc (mặc định: T2-T6)
        /// </summary>
        public List<DayOfWeek> WorkingDays { get; set; } = new List<DayOfWeek>
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

        /// <summary>
        /// Ca làm việc mặc định
        /// </summary>
        public WorkShift DefaultShift { get; set; } = WorkShift.Default;

        /// <summary>
        /// Ca làm việc theo ngày trong tuần (nếu khác nhau)
        /// Key = DayOfWeek, Value = WorkShift
        /// </summary>
        public Dictionary<DayOfWeek, WorkShift> ShiftsByDay { get; set; } 
            = new Dictionary<DayOfWeek, WorkShift>();

        /// <summary>
        /// Ca làm việc riêng cho từng Line
        /// Key = LineId
        /// </summary>
        public Dictionary<string, WorkShift> ShiftsByLine { get; set; }
            = new Dictionary<string, WorkShift>();

        public WorkingCalendar() { }

        #region Holiday Management

        /// <summary>
        /// Thêm ngày nghỉ lễ
        /// </summary>
        public void AddHoliday(DateTime date, string name, HolidayType type = HolidayType.PublicHoliday)
        {
            Holidays.Add(new Holiday(date, name, type));
        }

        /// <summary>
        /// Thêm nhiều ngày nghỉ liên tiếp (VD: Tết)
        /// </summary>
        public void AddHolidayRange(DateTime startDate, DateTime endDate, string name, 
            HolidayType type = HolidayType.PublicHoliday)
        {
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                Holidays.Add(new Holiday(date, name, type));
            }
        }

        /// <summary>
        /// Thêm ngày nghỉ bảo trì cho một Line cụ thể
        /// </summary>
        public void AddMaintenanceDay(DateTime date, string lineId, string description = "Bảo trì")
        {
            Holidays.Add(new Holiday(date, description, HolidayType.Maintenance, lineId));
        }

        /// <summary>
        /// Thêm các ngày nghỉ lễ Việt Nam cho một năm
        /// </summary>
        public void AddVietnameseHolidays(int year)
        {
            // Tết Dương lịch
            AddHoliday(new DateTime(year, 1, 1), "Tết Dương lịch");

            // Giỗ Tổ Hùng Vương (10/3 Âm lịch - cần convert)
            // Tạm thời dùng ngày cố định, thực tế cần thư viện convert âm lịch
            AddHoliday(new DateTime(year, 4, 18), "Giỗ Tổ Hùng Vương");

            // Ngày Thống nhất
            AddHoliday(new DateTime(year, 4, 30), "Ngày Thống nhất");

            // Quốc tế Lao động
            AddHoliday(new DateTime(year, 5, 1), "Quốc tế Lao động");

            // Quốc khánh
            AddHolidayRange(new DateTime(year, 9, 2), new DateTime(year, 9, 3), "Quốc khánh");

            // Tết Nguyên đán (cần convert âm lịch)
            // Tạm dùng khoảng cuối tháng 1 - đầu tháng 2
            // AddHolidayRange(new DateTime(year, 1, 29), new DateTime(year, 2, 4), "Tết Nguyên đán");
        }

        /// <summary>
        /// Tự động thêm weekend làm ngày nghỉ trong khoảng thời gian
        /// </summary>
        public void AddWeekendsAsHolidays(DateTime startDate, DateTime endDate)
        {
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    if (!Holidays.Any(h => h.Date == date && h.LineId == null))
                    {
                        Holidays.Add(new Holiday(date, 
                            date.DayOfWeek == DayOfWeek.Saturday ? "Thứ Bảy" : "Chủ Nhật", 
                            HolidayType.Weekend));
                    }
                }
            }
        }

        #endregion

        #region Working Day Checks

        /// <summary>
        /// Kiểm tra một ngày có phải ngày làm việc không (cho tất cả Line)
        /// </summary>
        public bool IsWorkingDay(DateTime date)
        {
            return IsWorkingDay(date, null);
        }

        /// <summary>
        /// Kiểm tra một ngày có phải ngày làm việc không (cho Line cụ thể)
        /// </summary>
        public bool IsWorkingDay(DateTime date, string lineId)
        {
            date = date.Date;

            // Kiểm tra có trong danh sách ngày làm việc trong tuần không
            if (!WorkingDays.Contains(date.DayOfWeek))
                return false;

            // Kiểm tra có phải ngày nghỉ không
            var holiday = Holidays.FirstOrDefault(h => 
                h.Date == date && 
                h.IsFullDay && 
                (h.LineId == null || h.LineId == lineId));

            return holiday == null;
        }

        /// <summary>
        /// Kiểm tra một ngày có phải ngày nghỉ không
        /// </summary>
        public bool IsHoliday(DateTime date, string lineId = null)
        {
            return !IsWorkingDay(date, lineId);
        }

        /// <summary>
        /// Lấy thông tin ngày nghỉ (nếu có)
        /// </summary>
        public Holiday GetHoliday(DateTime date, string lineId = null)
        {
            return Holidays.FirstOrDefault(h =>
                h.Date == date.Date &&
                (h.LineId == null || h.LineId == lineId));
        }

        #endregion

        #region Working Time Calculations

        /// <summary>
        /// Lấy số phút làm việc trong một ngày
        /// </summary>
        public int GetWorkingMinutesPerDay(DateTime date, string lineId = null)
        {
            if (!IsWorkingDay(date, lineId))
                return 0;

            // Kiểm tra nghỉ một phần
            var partialHoliday = Holidays.FirstOrDefault(h =>
                h.Date == date.Date &&
                !h.IsFullDay &&
                (h.LineId == null || h.LineId == lineId));

            var shift = GetWorkShift(date, lineId);

            if (partialHoliday != null && partialHoliday.PartialStartTime.HasValue)
            {
                // Trừ đi thời gian nghỉ một phần
                var breakMinutes = (partialHoliday.PartialEndTime.Value - 
                                   partialHoliday.PartialStartTime.Value).TotalMinutes;
                return Math.Max(0, shift.WorkingMinutes - (int)breakMinutes);
            }

            return shift.WorkingMinutes;
        }

        /// <summary>
        /// Lấy ca làm việc cho một ngày cụ thể
        /// </summary>
        public WorkShift GetWorkShift(DateTime date, string lineId = null)
        {
            // Ưu tiên 1: Ca riêng cho Line
            if (lineId != null && ShiftsByLine.TryGetValue(lineId, out var lineShift))
                return lineShift;

            // Ưu tiên 2: Ca riêng cho ngày trong tuần
            if (ShiftsByDay.TryGetValue(date.DayOfWeek, out var dayShift))
                return dayShift;

            // Mặc định
            return DefaultShift;
        }

        /// <summary>
        /// Tính tổng số phút làm việc trong một khoảng thời gian
        /// </summary>
        public int GetTotalWorkingMinutes(DateTime startDate, DateTime endDate, string lineId = null)
        {
            int total = 0;
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                total += GetWorkingMinutesPerDay(date, lineId);
            }
            return total;
        }

        /// <summary>
        /// Đếm số ngày làm việc trong một khoảng
        /// </summary>
        public int GetWorkingDayCount(DateTime startDate, DateTime endDate, string lineId = null)
        {
            int count = 0;
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (IsWorkingDay(date, lineId))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Lấy danh sách ngày làm việc trong một khoảng
        /// </summary>
        public List<DateTime> GetWorkingDays(DateTime startDate, DateTime endDate, string lineId = null)
        {
            var result = new List<DateTime>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (IsWorkingDay(date, lineId))
                    result.Add(date);
            }
            return result;
        }

        /// <summary>
        /// Lấy danh sách ngày nghỉ trong một khoảng
        /// </summary>
        public List<Holiday> GetHolidaysInRange(DateTime startDate, DateTime endDate, string lineId = null)
        {
            return Holidays.Where(h =>
                h.Date >= startDate.Date &&
                h.Date <= endDate.Date &&
                (h.LineId == null || h.LineId == lineId))
                .OrderBy(h => h.Date)
                .ToList();
        }

        #endregion

        #region Date Conversion

        /// <summary>
        /// Chuyển đổi ngày sang số phút làm việc (từ ngày tham chiếu)
        /// Chỉ tính ngày làm việc, bỏ qua ngày nghỉ
        /// </summary>
        public int ConvertDateToWorkingMinutes(DateTime date, DateTime referenceDate, string lineId = null)
        {
            if (date <= referenceDate)
                return 0;

            int totalMinutes = 0;
            for (var d = referenceDate.Date; d < date.Date; d = d.AddDays(1))
            {
                totalMinutes += GetWorkingMinutesPerDay(d, lineId);
            }

            // Thêm phần giờ trong ngày cuối (nếu là ngày làm việc)
            if (IsWorkingDay(date, lineId))
            {
                var shift = GetWorkShift(date, lineId);
                var minutesInDay = (date.TimeOfDay - shift.StartTime).TotalMinutes;
                if (minutesInDay > 0)
                {
                    totalMinutes += Math.Min((int)minutesInDay, shift.WorkingMinutes);
                }
            }

            return totalMinutes;
        }

        /// <summary>
        /// Chuyển đổi số phút làm việc sang ngày thực tế
        /// Tự động bỏ qua ngày nghỉ
        /// </summary>
        public DateTime ConvertWorkingMinutesToDate(int workingMinutes, DateTime referenceDate, string lineId = null)
        {
            if (workingMinutes <= 0)
                return referenceDate;

            int remainingMinutes = workingMinutes;
            var currentDate = referenceDate.Date;

            while (remainingMinutes > 0)
            {
                int minutesToday = GetWorkingMinutesPerDay(currentDate, lineId);

                if (minutesToday > 0)
                {
                    if (remainingMinutes <= minutesToday)
                    {
                        // Hoàn thành trong ngày này
                        var shift = GetWorkShift(currentDate, lineId);
                        return currentDate.Add(shift.StartTime).AddMinutes(remainingMinutes);
                    }

                    remainingMinutes -= minutesToday;
                }

                currentDate = currentDate.AddDays(1);

                // Safety check - không cho chạy quá 1000 ngày
                if ((currentDate - referenceDate).TotalDays > 1000)
                {
                    throw new InvalidOperationException("Không thể tính ngày hoàn thành - vượt quá 1000 ngày");
                }
            }

            return currentDate;
        }

        /// <summary>
        /// Tìm ngày làm việc tiếp theo từ một ngày
        /// </summary>
        public DateTime GetNextWorkingDay(DateTime fromDate, string lineId = null)
        {
            var date = fromDate.Date;
            while (!IsWorkingDay(date, lineId))
            {
                date = date.AddDays(1);
                if ((date - fromDate).TotalDays > 365)
                    throw new InvalidOperationException("Không tìm thấy ngày làm việc trong 365 ngày tới");
            }
            return date;
        }

        /// <summary>
        /// Tìm ngày làm việc trước đó từ một ngày
        /// </summary>
        public DateTime GetPreviousWorkingDay(DateTime fromDate, string lineId = null)
        {
            var date = fromDate.Date.AddDays(-1);
            while (!IsWorkingDay(date, lineId))
            {
                date = date.AddDays(-1);
                if ((fromDate - date).TotalDays > 365)
                    throw new InvalidOperationException("Không tìm thấy ngày làm việc trong 365 ngày trước");
            }
            return date;
        }

        /// <summary>
        /// Thêm số ngày làm việc vào một ngày
        /// </summary>
        public DateTime AddWorkingDays(DateTime fromDate, int workingDays, string lineId = null)
        {
            var date = fromDate.Date;
            int count = 0;

            while (count < workingDays)
            {
                date = date.AddDays(1);
                if (IsWorkingDay(date, lineId))
                    count++;

                if ((date - fromDate).TotalDays > 1000)
                    throw new InvalidOperationException("Vượt quá 1000 ngày");
            }

            return date;
        }

        #endregion

        #region Report

        /// <summary>
        /// In báo cáo lịch làm việc
        /// </summary>
        public string GenerateReport(DateTime startDate, DateTime endDate)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("                    LỊCH LÀM VIỆC                          ");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine($"Khoảng thời gian: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
            sb.AppendLine($"Tổng số ngày: {(endDate - startDate).TotalDays + 1}");
            sb.AppendLine($"Số ngày làm việc: {GetWorkingDayCount(startDate, endDate)}");
            sb.AppendLine($"Tổng phút làm việc: {GetTotalWorkingMinutes(startDate, endDate):N0}");
            sb.AppendLine();

            sb.AppendLine("--- NGÀY LÀM VIỆC TRONG TUẦN ---");
            sb.AppendLine($"  {string.Join(", ", WorkingDays)}");
            sb.AppendLine();

            sb.AppendLine("--- CA LÀM VIỆC MẶC ĐỊNH ---");
            sb.AppendLine($"  {DefaultShift}");
            sb.AppendLine();

            var holidays = GetHolidaysInRange(startDate, endDate);
            if (holidays.Any())
            {
                sb.AppendLine("--- NGÀY NGHỈ ---");
                foreach (var h in holidays)
                {
                    sb.AppendLine($"  {h}");
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
