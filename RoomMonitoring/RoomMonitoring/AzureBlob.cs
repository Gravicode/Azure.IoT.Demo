using System;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.SPOT;
using System.Security.Cryptography;

namespace RoomMonitoring
{
    public class AzureBlob
    {
        public string Account { get; set; }
        public string BlobType { get; set; }
        public string BlobEndPoint { get; set; }
        public string Key { get; set; }
        public string SharedKeyAuthorizationScheme { get; set; }

        public AzureBlob()
        {
            BlobType = "BlockBlob";
            SharedKeyAuthorizationScheme = "SharedKey";
        }

        public void PutBlob(String containerName, String blobName, byte[] blobContent, bool error = false)
        { 
            String requestMethod = "PUT";            
            StringBuilder urlPath = new StringBuilder("{0}/{1}").Replace("{0}", containerName).Replace("{1}", blobName);
            String storageServiceVersion = "2009-09-19";
            DateTime currentDateTime = DateTime.UtcNow;
            String dateInRfc1123Format = currentDateTime.ToString("R");  //Please note the blog where this is changed to "s" in the WebAPI
            
            Int32 blobLength = blobContent.Length;

            StringBuilder canonicalizedResource = new StringBuilder("/{0}/{1}")
                .Replace("{0}", Account).Replace("{1}", urlPath.ToString());

            StringBuilder canonicalizedHeaders = new StringBuilder("x-ms-blob-type:{0} \nx-ms-date:{1} \nx-ms-version:{2}")
                .Replace("{0}", BlobType).Replace("{1}", dateInRfc1123Format).Replace("{2}", storageServiceVersion);

            StringBuilder stringToSign = new StringBuilder("{0} \n\n\n{1} \n\n\n\n\n\n\n\n\n{2} \n{3}")
                .Replace("{0}", requestMethod).Replace("{1}", blobLength.ToString())
                .Replace("{2}", canonicalizedHeaders.ToString()).Replace("{3}", canonicalizedResource.ToString());

            string xMSBlobType = "x-ms-blob-type:BlockBlob";
            string dateNoHeader = currentDateTime.ToString("s");
            string xMSVersion = "x-ms-version:2009-09-19";

            string authorizationHeader = CreateAuthorizationHeader(stringToSign.ToString()); //CreateAuthorizationHeaderWebAPI(requestMethod, blobLength, xMSBlobType, xMSVersion, canonicalizedResource.ToString(), dateNoHeader);

            Uri uri = new Uri(BlobEndPoint + urlPath.ToString());
            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)WebRequest.Create(uri);
            request.Method = requestMethod;
            request.Headers.Add("x-ms-blob-type", BlobType);
            request.Headers.Add("x-ms-date", dateInRfc1123Format);
            request.Headers.Add("x-ms-version", storageServiceVersion);
            request.Headers.Add("Authorization", authorizationHeader);
            request.ContentLength = blobLength;

            Debug.Print("BlobEndPoint: " + BlobEndPoint);
            Debug.Print("urlPath: " + urlPath);
            Debug.Print("dateInRfc1123Format: " + dateInRfc1123Format);
            Debug.Print("blobLength: " + blobLength);
            Debug.Print("canonicalizedHeaders: " + canonicalizedHeaders.ToString());
            Debug.Print("canonicalizedResource: " + canonicalizedResource.ToString());
            Debug.Print("stringToSign: " + stringToSign.ToString());
            Debug.Print("authorizationHeader: " + authorizationHeader);
            Debug.Print("uri: " + uri);
            Debug.Print("x-ms-blob-type: " + request.Headers["x-ms-blob-type"]);
            Debug.Print("x-ms-date: " + request.Headers["x-ms-date"]);
            Debug.Print("x-ms-version: " + request.Headers["x-ms-version"]);
            Debug.Print("Authorization: " + request.Headers["Authorization"]);

            try
            {
                Debug.Print("BEGIN: request.GetRequestStream()");
                using (Stream requestStream = request.GetRequestStream())
                {
                    Debug.Print("END: request.GetRequestStream()");
                    requestStream.Write(blobContent, 0, blobLength);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    Debug.Print("HttpWebResponse.StatusCode: " + response.StatusCode.ToString());
                    Debug.Print("HttpWebResponse.StatusCode: " + response.StatusDescription.ToString());
                }
                error = false;
            }
            catch (WebException ex)
            {
                Debug.Print("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);
                
                error = true;
                using (Stream stream = ex.Response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        var s = sr.ReadToEnd();
                        Debug.Print(s);
                    }
                }
            }

        }

        private string CreateAuthorizationHeader(string canonicalizedstring)
        {
            //This is the code which runs the in the WebAPI
            string signature = string.Empty;
            using (HashAlgorithm hashSHA256 = new HashAlgorithm(HashAlgorithmType.SHA256))
            {
                Byte[] dataToHmac = System.Text.Encoding.UTF8.GetBytes(canonicalizedstring);
                signature = Convert.ToBase64String(hashSHA256.ComputeHash(dataToHmac));
            }

            StringBuilder authorizationHeader = new StringBuilder("{0} {1}:{2}")
                .Replace("{0}", SharedKeyAuthorizationScheme).Replace("{1}", Account).Replace("{2}", signature);

            //return SharedKeyAuthorizationScheme + " " + Account + ":" + signature;
            return authorizationHeader.ToString();
        }
       
        private static string CreateAuthorizationHeaderWebAPI(string requestMethod, int blobLength, string xMSBlobType, string xMSVersion, string canonicalizedResource, string now)
        {
            try
            {
                string hashedValue = string.Empty;
                string SharedKeyAuthorizationScheme = "SharedKey";
                
                string queryString = "requestMethod=" + requestMethod + "&blobLength=" + blobLength.ToString() + "&xMSBlobType=" + xMSBlobType + "&xMSVersion=" + xMSVersion + "&canonicalizedResource=" + canonicalizedResource + "&now=" + now;
                Uri uri = new Uri("https://**??**.azurewebsites.net/api/HMACSHA256?" + queryString);
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                System.IO.Stream dataStream = response.GetResponseStream();
                System.IO.StreamReader reader = new System.IO.StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                reader.Close();
                response.Close();

                string string2find1 = "HashedValue\":\"";
                string string2find2 = "\"";
                int start = responseFromServer.IndexOf(string2find1) + string2find1.Length;
                int stop = responseFromServer.IndexOf(string2find2, start);
                hashedValue = responseFromServer.Substring(start, stop - start);

                StringBuilder authorizationHeader = new StringBuilder("{0} {1}:{2}")
                .Replace("{0}", SharedKeyAuthorizationScheme).Replace("{1}", "put").Replace("{2}", hashedValue);

                return authorizationHeader.ToString();
            }
            catch (System.Net.WebException ex)
            {
                Debug.Print("Exception: " + ex.Message);
                return null;
            }
        }

        public void KirimBlob(String containerName, String blobName, byte[] blobContent, bool error = false)
        {
            String requestMethod = "PUT";
            StringBuilder urlPath = new StringBuilder("{0}/{1}").Replace("{0}", containerName).Replace("{1}", blobName);
            String storageServiceVersion = "2015-02-21";
            DateTime currentDateTime = DateTime.UtcNow;
            String dateInRfc1123Format = currentDateTime.ToString("R");  //Please note the blog where this is changed to "s" in the WebAPI

            Int32 blobLength = blobContent.Length;

            StringBuilder canonicalizedResource = new StringBuilder("/{0}/{1}")
                .Replace("{0}", Account).Replace("{1}", containerName);//.Replace("{2}", blobName);

            StringBuilder canonicalizedHeaders = new StringBuilder("x-ms-date:{0} \nx-ms-version:{1}\n")
                .Replace("{0}", dateInRfc1123Format).Replace("{1}", storageServiceVersion);

            StringBuilder stringToSign = new StringBuilder("{0} \n{1} \n\n\n\n\n\n\n\n\n\n\n\n{2} \n{3} \nrestype:container \ntimeout:30")
                .Replace("{0}", requestMethod).Replace("{1}", blobLength.ToString()).Replace("{2}", canonicalizedHeaders.ToString())
                .Replace("{3}", canonicalizedResource.ToString());


            string authorizationHeader = CreateAuthorizationHeader(stringToSign.ToString()); //CreateAuthorizationHeaderWebAPI(requestMethod, blobLength, xMSBlobType, xMSVersion, canonicalizedResource.ToString(), dateNoHeader);

            Uri uri = new Uri(BlobEndPoint + urlPath.ToString());
            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)WebRequest.Create(uri);
            request.Method = requestMethod;
            request.Headers.Add("x-ms-date", dateInRfc1123Format);
            request.Headers.Add("x-ms-version", storageServiceVersion);
            request.Headers.Add("Authorization", authorizationHeader);
            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Headers.Add("x-ms-meta-m1", "v1");
            request.Headers.Add("x-ms-meta-m2", "v2");
            request.ContentLength = blobLength;

            //Debug.Print("BlobEndPoint: " + BlobEndPoint);
            //Debug.Print("urlPath: " + urlPath);
            //Debug.Print("dateInRfc1123Format: " + dateInRfc1123Format);
            //Debug.Print("blobLength: " + blobLength);
            //Debug.Print("canonicalizedHeaders: " + canonicalizedHeaders.ToString());
            //Debug.Print("canonicalizedResource: " + canonicalizedResource.ToString());
            //Debug.Print("stringToSign: " + stringToSign.ToString());
            //Debug.Print("authorizationHeader: " + authorizationHeader);
            //Debug.Print("uri: " + uri);
            //Debug.Print("x-ms-blob-type: " + request.Headers["x-ms-blob-type"]);
            //Debug.Print("x-ms-date: " + request.Headers["x-ms-date"]);
            //Debug.Print("x-ms-version: " + request.Headers["x-ms-version"]);
            //Debug.Print("Authorization: " + request.Headers["Authorization"]);

            try
            {
                Debug.Print("BEGIN: request.GetRequestStream()");
                using (Stream requestStream = request.GetRequestStream())
                {
                    Debug.Print("END: request.GetRequestStream()");
                    requestStream.Write(blobContent, 0, blobLength);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    Debug.Print("HttpWebResponse.StatusCode: " + response.StatusCode.ToString());
                    Debug.Print("HttpWebResponse.StatusCode: " + response.StatusDescription.ToString());
                }
                error = false;
            }
            catch (WebException ex)
            {
                Debug.Print("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);

                error = true;
                using (Stream stream = ex.Response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        var s = sr.ReadToEnd();
                        Debug.Print(s);
                    }
                }
            }

        }
    }
}
