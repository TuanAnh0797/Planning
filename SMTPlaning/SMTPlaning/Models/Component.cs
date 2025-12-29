using System;

namespace SMTScheduler.Models
{
    /// <summary>
    /// Linh kiện SMT (Component)
    /// </summary>
    public class Component
    {
        /// <summary>
        /// Mã linh kiện (VD: R001, C001, IC001)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tên linh kiện
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Loại linh kiện (Resistor, Capacitor, IC, Connector, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Số khe Feeder cần chiếm (thường = 1, nhưng IC lớn có thể > 1)
        /// </summary>
        public int FeederSlots { get; set; } = 1;

        /// <summary>
        /// Thời gian để thay/gắn linh kiện này vào Feeder (phút)
        /// </summary>
        public double ChangeoverTimeMinutes { get; set; } = 2;

        public Component() { }

        public Component(string id, string name, string type = null, 
                        int feederSlots = 1, double changeoverTimeMinutes = 2)
        {
            Id = id;
            Name = name;
            Type = type ?? "Unknown";
            FeederSlots = feederSlots;
            ChangeoverTimeMinutes = changeoverTimeMinutes;
        }

        public override string ToString()
        {
            return $"{Id}: {Name} ({FeederSlots} slot, {ChangeoverTimeMinutes} phút thay)";
        }

        public override bool Equals(object obj)
        {
            if (obj is Component other)
                return Id == other.Id;
            return false;
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }
    }
}
