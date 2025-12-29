namespace SMTScheduler.Models
{
    /// <summary>
    /// Công đoạn sản xuất SMT
    /// </summary>
    public class Stage
    {
        /// <summary>
        /// ID công đoạn
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tên công đoạn (VD: Solder Paste, Pick&Place, Reflow, AOI)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Thứ tự công đoạn (1, 2, 3, 4)
        /// Sản phẩm phải hoàn thành công đoạn nhỏ trước mới được làm công đoạn lớn
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Công đoạn này có cần Feeder/linh kiện không?
        /// VD: Pick&Place = true, Reflow = false
        /// </summary>
        public bool RequiresFeeder { get; set; } = false;

        /// <summary>
        /// Mô tả công đoạn
        /// </summary>
        public string Description { get; set; }

        public Stage() { }

        public Stage(int id, string name, int order, bool requiresFeeder = false, string description = null)
        {
            Id = id;
            Name = name;
            Order = order;
            RequiresFeeder = requiresFeeder;
            Description = description;
        }

        public override string ToString()
        {
            string feederInfo = RequiresFeeder ? " [Cần Feeder]" : "";
            return $"CĐ{Order}: {Name}{feederInfo}";
        }
    }
}
