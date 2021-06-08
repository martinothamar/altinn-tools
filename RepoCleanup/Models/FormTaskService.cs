using System.Collections.Generic;

namespace RepoCleanup.Models
{
    public class FormTaskService
    {
        public string RestEnabled { get; set; }

        public List<AltinnFormMetaData> FormsMetaData { get; set; }
    }
}
