using System;
using Microsoft.SPOT;
using System.Net;

namespace RoSchmi.Net
{
    public struct BasicHttpResponse
    {
        public string Content_MD5 { get; set; }
        public string ETag { get; set; }
        public string Body { get; set; }
        public HttpStatusCode StatusCode { get; set; }
    }
}




