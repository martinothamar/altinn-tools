using System;
using System.Collections.Generic;

namespace RepoCleanup.Models
{
    public class Altinn2Service
    {
        public string ServiceOwnerCode { get; set; }

        public string OrganizationNumber { get; set; }

        public string ServiceOwnerName { get; set; }

        public string ServiceName { get; set; }

        public string ServiceCode { get; set; }

        public string ServiceEditionCode { get; set; }

        public DateTime ValidFrom { get; set; }

        public DateTime ValidTo { get; set; }

        public string ServiceType { get; set; }

        public string EnterpriseUserEnabled { get; set; }

        public List<AltinnFormMetaData> Forms { get; set; }
    }
}
