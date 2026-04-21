using System.Collections.Generic;

namespace asset_monitoring.Models
{
    public class ComplaintLogViewModel
    {
        public List<ComplaintLog> Complaints { get; set; } = new();
        public int OpenComplaintCount { get; set; }

        /// <summary>
        /// When true, render the Excel/PDF/CSV export buttons.
        /// Admin view = true; JE view = false (role-scoped exports not wired yet).
        /// </summary>
        public bool ShowExports { get; set; }

        /// <summary>
        /// When true, render the Action dropdown (Resolve / Reject) on OPEN rows.
        /// Admin + JE = true; future read-only views = false.
        /// </summary>
        public bool ShowActions { get; set; } = true;
    }
}
