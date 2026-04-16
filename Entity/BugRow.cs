namespace BUGAUDITSCRIPT
{
    public class BugRow
    {
        public string BugId { get; set; }
        public string Status { get; set; }
        public string MissingFields { get; set; }
        public string RootCause { get; set; }
        public string FixVersions { get; set; }
        public string CommitsPR { get; set; }
        public string GeneratedAtIST { get; set; }
        public string HasRootCause { get; set; }
        public string HasFix { get; set; }
        public string HasImpact { get; set; }

    }
}