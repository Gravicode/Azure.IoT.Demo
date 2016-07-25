// Copyright RoSchmi 2016 License apache 2.0
// Version 2.0 28.05.2016 NETMF 4.3 GHI SDK 2016 R1 Prerelease 2
// Parts of the code were taken from
// Benjamin Perkins
// -https://msdn.microsoft.com/en-us/magazine/dn913186.aspx
// by AndyCross: netmfazurestorage / Table / TableClient.cs
// -https://github.com/azure-contrib/netmfazurestorage/blob/master/netmfazurestorage/Table/TableClient.cs
//
// Other parts of the code are taken from martin calsyn
// -https://github.com/PervasiveDigital/serialwifi/tree/master/src/Common


using System;
using System.Net;   //Reference System.Http .NET MicroFramework 4.3 must be additionally included
using System.Text;
using System.IO;
using Microsoft.SPOT;
using PervasiveDigital.Utilities;
using PervasiveDigital.Security.ManagedProviders;
using RoSchmi.Net.Azure.Storage;

//using System.Security.Cryptography;

namespace RoSchmi.Net.Azure.Storage
{
    public class BlobClient
    {
            private readonly CloudStorageAccount _account;
        
            private string _versionHeader = "2015-04-05";
            //private string _versionHeader = "2015-02-21";
            private string BlobType = "BlockBlob";
           
            private string _responseHeader_ETag = null;
            private string _responseHeader_Content_MD5 = null;
            private string _responseHeader_Date = null;
            private string _responseHeader_Last_Modified = null;
            private string _response_StatusDescription = null;

            private const string ContainerString = "restype=container";

            private HttpStatusCode _response_StatusCode;

            private static bool _fiddlerIsAttached = false;
            private static IPAddress _fiddlerIP = null;
            private static int _fiddlerPort = 8888;

            public enum PublicAccessType
            {
                noPublicAccess,
                blob,
                container
            }
            public string HttpVerb { get; set; }
            
            public string ResponseHeader_ETag { get { return _responseHeader_ETag; } }
            public string ResponseHeader_Content_MD5 { get{ return _responseHeader_Content_MD5;}}
            public string ResponseHeader_Date { get { return _responseHeader_Date; } }
            public string ResponseHeader_Last_Modified { get { return _responseHeader_Last_Modified; } }
            public HttpStatusCode Response_StatusCode { get { return _response_StatusCode ; } }
            public string Response_StatusDescription { get { return _response_StatusDescription; } }


            public BlobClient(CloudStorageAccount account, string storageServiceVersion = "2015-04-05")
            {
                _account = account;
                _versionHeader = storageServiceVersion;
                BlobType = "BlockBlob";
            }

            public void attachFiddler(bool pfiddlerIsAttached, IPAddress pfiddlerIP, int pfiddlerPort)
            {
                _fiddlerIsAttached = pfiddlerIsAttached;
                _fiddlerIP = pfiddlerIP;
                _fiddlerPort = pfiddlerPort;
            }

            #region PutBlockBlob
            public HttpStatusCode PutBlockBlob(string containerName, string blobName, Byte[] blobContent)
            {
                HttpVerb = "PUT";

                string  urlPath = StringUtilities.Format("{0}/{1}", containerName, blobName);
                string BlobEndPoint = _account.UriEndpoints["Blob"].ToString();
                string canonicalizedResource = StringUtilities.Format("/{0}/{1}", _account.AccountName, urlPath);
                int contentLength = blobContent.Length;
                string timestamp = GetDateHeader();
                string canonicalizedHeaders = StringUtilities.Format("x-ms-blob-type:{0}\nx-ms-date:{1}\nx-ms-version:{2}", BlobType, timestamp, _versionHeader);

                string authorizationHeader = CreateAuthorizationHeader(_versionHeader, canonicalizedHeaders, canonicalizedResource, timestamp, "", contentLength, HttpVerb);
                
                Uri uri = new Uri(BlobEndPoint +"/" + urlPath.ToString());

                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)WebRequest.Create(uri);
                request.Method = HttpVerb;
                request.Headers.Add("x-ms-blob-type", BlobType);
                request.Headers.Add("x-ms-date", timestamp);
                request.Headers.Add("x-ms-version", _versionHeader);
                request.Headers.Add("Authorization", authorizationHeader);
                request.ContentLength = contentLength;

                Debug.Print("\nTime of request: " + DateTime.Now);
                Debug.Print("Url: " + BlobEndPoint + "/" + urlPath);

                //*******************************************************
                // To use Fiddler as WebProxy include this code segment
                // Use the local IP-Address of the PC where Fiddler is running
                // See here how to configurate Fiddler; -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
                if (_fiddlerIsAttached)
                {
                    request.Proxy = new WebProxy(_fiddlerIP.ToString(), _fiddlerPort);
                }
                //**********

                _responseHeader_Content_MD5 = null;
                _responseHeader_ETag = null;
                _responseHeader_Date = null;
                _responseHeader_Last_Modified = null;
                _response_StatusDescription = null;

                try
                {
                    using (Stream requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(blobContent, 0, contentLength);
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        _response_StatusCode = response.StatusCode;
                        _response_StatusDescription = response.StatusDescription;
                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            _responseHeader_Content_MD5 = response.Headers["Content-MD5"].ToString();
                            _responseHeader_ETag = response.Headers["ETag"].ToString();
                            _responseHeader_Date = response.Headers["Date"].ToString();
                            _responseHeader_Last_Modified = response.Headers["Last-Modified"].ToString();
                            
                        }
                        return response.StatusCode;
                    }
                }
                catch (WebException ex)
                {
                    Debug.Print("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);
                    _response_StatusCode = ((HttpWebResponse)ex.Response).StatusCode;
                    _response_StatusDescription = ((HttpWebResponse)ex.Response).StatusDescription;
                    using (Stream stream = ex.Response.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(stream))
                        {
                            var s = sr.ReadToEnd();
                            Debug.Print(s);
                            return _response_StatusCode;
                        }
                    }
                }
            }
            #endregion

            #region CreateContainer
            public HttpStatusCode CreateContainer(string pcontainerName, PublicAccessType pAccessType)
            {
                    string accessType = string.Empty;
                    if (pAccessType == PublicAccessType.blob)
                    { accessType = "blob"; }
                    else
                    {
                        if (pAccessType == PublicAccessType.container)
                        { accessType = "container";}
                    }
                    // container names may only include lower-case characters
                    string containerName = pcontainerName.ToLower();
                    HttpVerb = "PUT";

                    string urlPath = StringUtilities.Format("{0}?restype=container", containerName);
                    string BlobEndPoint = _account.UriEndpoints["Blob"].ToString();
                    
                    string timestamp = GetDateHeader();
                    
                    string canonicalizedResource = StringUtilities.Format("/{0}/{1}\nrestype:container", _account.AccountName, containerName);

                    string canonicalizedHeaders = string.Empty;
                    
                    if (pAccessType == PublicAccessType.noPublicAccess)
                    {
                        canonicalizedHeaders = StringUtilities.Format("x-ms-date:{0}\nx-ms-version:{1}", timestamp, _versionHeader);
                    }
                    else
                    {
                        canonicalizedHeaders = StringUtilities.Format("x-ms-blob-public-access:{2}\nx-ms-date:{0}\nx-ms-version:{1}", timestamp, _versionHeader, accessType);
                    }

                    byte[] content = new byte[1];
                    int contentLength = 0;

                    string authorizationHeader = CreateAuthorizationHeader(_versionHeader, canonicalizedHeaders, canonicalizedResource, timestamp, "", contentLength, HttpVerb);

                    Uri uri = new Uri(BlobEndPoint + "/" + urlPath.ToString());

                    System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)WebRequest.Create(uri);
                    request.Method = HttpVerb;
                    if (pAccessType != PublicAccessType.noPublicAccess)
                    {
                        request.Headers.Add("x-ms-blob-public-access", accessType);
                    }
                    request.Headers.Add("x-ms-date", timestamp);
                    request.Headers.Add("x-ms-version", _versionHeader);
                    request.Headers.Add("Authorization", authorizationHeader);
                    request.ContentLength = contentLength;

                    Debug.Print("\nTime of request: " + DateTime.Now);
                    Debug.Print("Url: " + BlobEndPoint + "/" + urlPath);


                    //*******************************************************
                    // To use Fiddler as WebProxy include this code segment
                    // Use the local IP-Address of the PC where Fiddler is running
                    // See here how to configurate Fiddler; -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
                    if (_fiddlerIsAttached)
                    {
                        request.Proxy = new WebProxy(_fiddlerIP.ToString(), _fiddlerPort);
                    }
                    //**********


                    _responseHeader_Content_MD5 = null;
                    _responseHeader_ETag = null;
                    _responseHeader_Date = null;
                    _responseHeader_Last_Modified = null;
                    _response_StatusDescription = null;

                    try
                    {
                        using (Stream requestStream = request.GetRequestStream())
                        {
                            requestStream.Write(content, 0, contentLength);
                        }

                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        {
                            if (response.StatusCode == HttpStatusCode.Created)
                            {
                                _response_StatusCode = response.StatusCode;
                                _response_StatusDescription = response.StatusDescription;
                                _responseHeader_ETag = response.Headers["ETag"].ToString();
                                _responseHeader_Date = response.Headers["Date"].ToString();
                                _responseHeader_Last_Modified = response.Headers["Last-Modified"].ToString();
                            }
                            return response.StatusCode;
                        }
                    }
                    catch (WebException ex)
                    {
                    Debug.Print("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);
                    _response_StatusCode = ((HttpWebResponse)ex.Response).StatusCode;
                    _response_StatusDescription = ((HttpWebResponse)ex.Response).StatusDescription;
                    using (Stream stream = ex.Response.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(stream))
                        {
                            var s = sr.ReadToEnd();
                            Debug.Print(s);
                            return _response_StatusCode;
                        }
                    }
                }
            }
            #endregion

        
        protected string CreateAuthorizationHeader(string storageServiceVersion, string canHeadersString, string canResource, string timestamp, string options = "", int contentLength = 0, string pHttpVerb = "PUT")
        {
            HttpVerb = pHttpVerb;
            string toSign = string.Empty;
            if (((storageServiceVersion == "2015-02-21")||(storageServiceVersion == "2015-04-05")) && contentLength == 0)
            {
                toSign = StringUtilities.Format("{0}\n\n\n\n\n\n\n\n\n\n\n{5}\n{6}\n{4}",
                                        HttpVerb, contentLength, timestamp, storageServiceVersion, canResource, options, canHeadersString);
            }
            else
            {
                toSign = StringUtilities.Format("{0}\n\n\n{1}\n\n\n\n\n\n\n\n{5}\n{6}\n{4}",
                                             HttpVerb, contentLength, timestamp, storageServiceVersion, canResource, options, canHeadersString);
            }
            
            var hmac = new HMACSHA256(Convert.FromBase64String(_account.AccountKey));
            var hmacBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            string signature = Convert.ToBase64String(hmacBytes).Replace("!", "+").Replace("*", "/"); ;
            return "SharedKey " + _account.AccountName + ":" + signature;
        }

        protected string GetDateHeader()
        {
            return DateTime.UtcNow.ToString("R");
        }
    }
}
