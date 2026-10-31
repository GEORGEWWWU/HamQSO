using System;
using System.Text.Json.Serialization;

namespace HamJi.Core
{
    public enum RecKind
    {
        Frequency = 0,
        Log = 1
    }

    public class QsoRecord
    {
        public string Id { get; set; }
        public int Kind { get; set; }
        public double Freq { get; set; }
        public string Mode { get; set; }
        public long Time { get; set; }
        public string Call { get; set; }
        public string RstTx { get; set; }
        public string RstRx { get; set; }
        public string Qth { get; set; }
        public string Rig { get; set; }
        public string Power { get; set; }
        public string Ant { get; set; }
        public string Notes { get; set; }
        public long Created { get; set; }

        [JsonIgnore]
        public bool IsLog => Kind == (int)RecKind.Log;

        [JsonIgnore]
        public DateTime LocalTime => DateTimeOffset.FromUnixTimeMilliseconds(Time).LocalDateTime;

        [JsonIgnore]
        public string FreqText
        {
            get
            {
                if (Freq <= 0) return "";
                if (Freq >= 1000) return Freq.ToString("0.0000");
                if (Freq >= 10) return Freq.ToString("0.000");
                return Freq.ToString("0.00000");
            }
        }

        [JsonIgnore]
        public string UnitText => Freq >= 1000 ? "GHz" : "MHz";

        [JsonIgnore]
        public string TimeText => LocalTime.ToString("HH:mm");

        [JsonIgnore]
        public string RstText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RstTx) && string.IsNullOrWhiteSpace(RstRx)) return "";
                var a = string.IsNullOrWhiteSpace(RstTx) ? "—" : RstTx;
                var b = string.IsNullOrWhiteSpace(RstRx) ? "—" : RstRx;
                return a + " / " + b;
            }
        }
    }
}
