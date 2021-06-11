using System.Collections.Generic;

namespace RepoCleanup.Models
{
    public class Altinn2ReportingService
    {
        public bool RestEnabled { get; set; }

        public List<Altinn2Form> FormsMetaData { get; set; }
    }
}
