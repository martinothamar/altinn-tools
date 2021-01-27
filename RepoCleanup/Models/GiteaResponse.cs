using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace RepoCleanup.Models
{
    public class GiteaResponse
    {
        public Exception Exception { get; set; }
        public HttpStatusCode StatusCode { get; set; }

        public string ResponseMessage { get; set; }

        public bool Success { get; set; }
    }
}
