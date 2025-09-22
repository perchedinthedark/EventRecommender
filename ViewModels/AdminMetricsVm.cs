using System;
using System.Collections.Generic;

namespace EventRecommender.ViewModels
{
    public class AdminMetricsVm
    {
        public int Users { get; set; }
        public int Events { get; set; }
        public int Interactions { get; set; }
        public int Clicks { get; set; }
        public bool ModelsExist { get; set; }
        public DateTime? LastTrainUtc { get; set; }
        public DateTime? LastServeUtc { get; set; }
        public int ColdStartUsers { get; set; }
        public int CandidatePoolUpcoming { get; set; }
        public List<ClickRow> RecentClicks { get; set; } = new();

        public class ClickRow
        {
            public DateTime WhenUtc { get; set; }
            public string EventTitle { get; set; } = "";
            public string UserName { get; set; } = "";
            public int? DwellMs { get; set; }
        }
    }
}
