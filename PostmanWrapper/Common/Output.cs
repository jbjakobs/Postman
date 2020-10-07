namespace Postman.Common
{
    public partial class Output
    {
        public Run Run { get; set; }
    }

    public partial class Run
    {
        public Stats Stats { get; set; }
        public object[] Failures { get; set; }
    }

    public partial class Stats
    {
        public Assertions Iterations { get; set; }
        public Assertions Items { get; set; }
        public Assertions Scripts { get; set; }
        public Assertions Prerequests { get; set; }
        public Assertions Requests { get; set; }
        public Assertions Tests { get; set; }
        public Assertions Assertions { get; set; }
        public Assertions TestScripts { get; set; }
        public Assertions PrerequestScripts { get; set; }
    }

    public partial class Assertions
    {
        public long Total { get; set; }
        public long Pending { get; set; }
        public long Failed { get; set; }
    }
}
