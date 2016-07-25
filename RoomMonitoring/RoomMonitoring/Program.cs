using Gadgeteer.Modules.GHIElectronics;
using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;
using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Microsoft.SPOT.Net.NetworkInformation;
using Json.NETMF;
using GHI.Networking;
using System.IO.Ports;
using System.Text;
using System.Net;
//using uPLibrary.Networking.M2Mqtt;
//using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.SPOT.Time;
using Microsoft.SPOT.Hardware;
using Amqp.Framing;
using Amqp;
using Amqp.Types;
using System.Security.Cryptography;

using PervasiveDigital.Utilities;
using RoSchmi.Net.Azure.Storage;
using System.Security.Cryptography.X509Certificates;
namespace System.Diagnostics
{
    public enum DebuggerBrowsableState
    {
        Never,
        Collapsed,
        RootHidden
    }
}
namespace RoomMonitoring
{
    public partial class Program
    {
        // *****************  Settings:   These parameters have to be set by the user  **************************************

        // Your Azure Storage Account Name
        private const string myAccount = "roomdata";

        // Your Azure storage key
        private const string myKey = "7WEpOSQcIxVJa4LFlsi3o9rJRl3HtJPxasEwA3QmNpYCFSahE88IG7hL0dpLFFbQGi+0OnNLVlzY+Q9WlpuBgQ==";

        private const bool useHTTPS = true;      // use http or or https
        //private const bool useHTTPS = false;

        byte[] ca = Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root);  // Certificate of Azure included as a Resource
        // See -https://blog.devmobile.co.nz/2013/03/01/https-with-netmf-http-client-managing-certificates/ how to include a certificate

        private int timeZoneOffset = 120;        // in minutes

        // You can select what kind of Debug.Print messages are sent
        private static AzureStorageHelper.DebugMode DebugMode = AzureStorageHelper.DebugMode.StandardDebug;
        private static AzureStorageHelper.DebugLevel DebugLevel = AzureStorageHelper.DebugLevel.DebugErrorsPlusMessages;

        // To use Fiddler as WebProxy set attachFiddler = true and set the proper IPAddress and port
        // Use the local IP-Address of the PC where Fiddler is running
        // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
        private bool attachFiddler = false;
        private const string fiddlerIPAddress = "192.168.1.48"; // Set to the IP-Adress of your PC
        private const int fiddlerPort = 8888;                   // Standard port of fiddler

        //********************      End of Setting       *************************************************************

        private CloudStorageAccount myCloudStorageAccount;
        public BlobClient blob;
        public TableClient table;
        private X509Certificate[] caCerts;

        private const string HOST = "iot-gateway.azure-devices.net";
        private const int PORT = 5671;
        private const string DEVICE_ID = "Room-Sensor";
        private const string DEVICE_KEY = "kfUeRhwjDBXkpRZFkRV2mlarb2N1y7w/QtnOhdj6RBc=";//"SharedAccessSignature sr=Algo-IoT-Hub.azure-devices.net%2fdevices%2fRoom-Sensor&sig=jTTXF%2b6y4e3xw131tO3NayQKvElUPfEsZgJieBPj2%2fw%3d&se=1493959076";

        private static Address address;
        private static Connection connection;
        private static Session session;

        private static Thread receiverThread;



        //TimeServer Settings
        private int _daylightSavingsShift;
        private const int LocalTimeZone = 7 * 60;


        //public static MqttClient client { set; get; }
        //const string MQTT_BROKER_ADDRESS = "192.168.1.100";
        /*
        void SubscribeMessage()
        {
            //event handler saat message dari mqtt masuk
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            //subcribe ke topik tertentu di mqtt
            client.Subscribe(new string[] { "/iot/room", "/iot/control" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

        }
        
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            //conversi byte message ke string
            string pesan = new string(Encoding.UTF8.GetChars(e.Message));
            if(e.Topic=="/iot/control")
            {
                switch (pesan)
                {
                    case "photo":
                        var thPhoto = new Thread(new ThreadStart(Jepret));
                        thPhoto.Start();
                        break;
                    default:
                        break;
                }
            }
            //cetak ke console tuk kebutuhan debug
            Debug.Print("Message : " + pesan);
        }
        
        
        void Jepret()
        {
            //ambil foto dari camera
            serialCameraL1.StartStreaming();
            while (!serialCameraL1.NewImageReady)
            {
                Thread.Sleep(50);
            }

            byte[] dataImage = serialCameraL1.GetImageData();
            serialCameraL1.StopStreaming();
            // bikin konten yang mau di post ke server                    
            var content = Gadgeteer.Networking.POSTContent.CreateBinaryBasedContent(dataImage);

            // bikin request
            var request = Gadgeteer.Networking.HttpHelper.CreateHttpPostRequest(
                @"http://" + MQTT_BROKER_ADDRESS + ":991/api/Upload.ashx" // url service/handler
                , content // data gambar
                , "image/jpeg" // tipe mime di header
            );
            request.ResponseReceived += (HttpRequest s, HttpResponse response) =>
            {
                if (response.StatusCode == "200")
                {
                    Debug.Print("sukses:" + response.Text);
                    client.Publish("/iot/photo", Encoding.UTF8.GetBytes(response.Text), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
                }

            };
            // kirim request via http post
            request.SendRequest();
        }*/
        
        // Initialization of AMQP connection to Azure Event Hubs
        void insertImageintoAzureBlob(GT.Picture picture, string fileName,string ContainerName)
        {
            /*
            AzureBlob storage = new AzureBlob()
            {
                //Account=StorageAccountName, BlobEndpoint=StorageAccountName.blob.core.windows.net, Key=your storage account key
                Account = "roomcapture",
                BlobEndPoint = "https://roomcapture.blob.core.windows.net/",
                Key = "A79h3bKQkgKEq52dXLzpkUnZcKO1YRaJshRTqIoWv5/4urzBt5sWaZb7aQ5D6CkxmpST7RUfYX2mxb2I8aJABg=="
            };
            */
            //bool error = false;

            if (wifiRS21.IsNetworkUp)
            {
                try
                {
                    // bikin konten yang mau di post ke server                    
                    var content = Gadgeteer.Networking.POSTContent.CreateBinaryBasedContent(picture.PictureData);

                    // bikin request
                    var request = Gadgeteer.Networking.HttpHelper.CreateHttpPostRequest(
                        @"http://192.168.1.101:3131/BlobHandler.ashx" // url service/handler
                        , content // data gambar
                        , "image/jpeg" // tipe mime di header
                    );
                    request.ResponseReceived += (HttpRequest s, HttpResponse response) =>
                    {
                        if (response.StatusCode == "200")
                        {
                            Debug.Print("sukses:" + response.Text);
                            //client.Publish("/iot/photo", Encoding.UTF8.GetBytes(response.Text), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
                        }

                    };
                    // kirim request via http post
                    request.SendRequest();
                    /*
                    //"blob" is the name of your container
                    storage.KirimBlob(ContainerName, fileName, picture.PictureData, error);
                    if (error)
                    {
                        Debug.Print("ERROR:  " + error.ToString());
                    }
                    else
                    {
                        Debug.Print("There was no error via PutBlob.");
                    }*/
                }
                catch (Exception ex)
                {
                    Debug.Print("EXCEPTION: " + ex.Message);
                }
            }
            else
            {
                Debug.Print("NO NETWORK CONNECTION");
            }
        }
        void TakePhoto() {
            //ambil foto dari camera
            serialCameraL1.StartStreaming();
            while (!serialCameraL1.NewImageReady)
            {
                Thread.Sleep(50);
            }

            byte[] dataImage = serialCameraL1.GetImageData();
            serialCameraL1.StopStreaming();
            GT.Picture picture = new GT.Picture(dataImage, GT.Picture.PictureEncoding.JPEG);
            var FileName ="myroom-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".jpg";
            insertImageintoAzureBlob(picture, FileName,"photo");
        }
        void ProgramStarted()
        {
            Debug.Print("Program Started");
            //setup wifi
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            var netif = wifiRS21.NetworkInterface;
            netif.Open();
            netif.EnableDhcp();
            netif.EnableDynamicDns();
            netif.Join("wifi berbayar", "123qweasd");

            while (netif.IPAddress == "0.0.0.0")
            {
                Debug.Print("Waiting for DHCP");
                Thread.Sleep(250);
            }

            // get Internet Time using SNTP
            GetInternetTime();

            Amqp.Trace.TraceLevel = Amqp.TraceLevel.Frame | Amqp.TraceLevel.Verbose;
            Amqp.Trace.TraceListener = (f, a) => Debug.Print(DateTime.Now.ToString("[hh:ss.fff]") + " " + Fx.Format(f, a));
            address = new Address(HOST, PORT, null, null);
            connection = new Connection(address);

            string audience = Fx.Format("{0}/devices/{1}", HOST, DEVICE_ID);
            string resourceUri = Fx.Format("{0}/devices/{1}", HOST, DEVICE_ID);
            const string KEYNAME = "iothubowner";
            string sasToken = GetSharedAccessSignature(KEYNAME, DEVICE_KEY, resourceUri, new TimeSpan(0,1, 0, 0));
            bool cbs = PutCbsToken(connection, HOST, sasToken, audience);

            if (cbs)
            {
                session = new Session(connection);
                //receiverThread = new Thread(ReceiveCommands);
                //receiverThread.Start();
     
            }

            //receiverThread.Join();

            //session.Close();
            //connection.Close();

            /*
            //konek ke mqtt broker
            client = new MqttClient("");

            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId, "guest", "guest");
            //subcribe topik
            SubscribeMessage();
            */
          
            //setup blob
            caCerts = new X509Certificate[] { new X509Certificate(ca) };

            myCloudStorageAccount = new CloudStorageAccount(myAccount, myKey, useHttps: useHTTPS);

            //setup timer untuk ambil data sensor ruangan
            GT.Timer timer = new GT.Timer(2000);
            timer.Tick += timer_Tick;
            timer.Start();

            //TakePhoto();

            //TestAzure();
        }

        private void SendEvent(string MessageStr)
        {
            string entity = Fx.Format("/devices/{0}/messages/events", DEVICE_ID);
            SenderLink senderLink = new SenderLink(session, "sender-link", entity);

            var messageValue = Encoding.UTF8.GetBytes(MessageStr);
            Message message = new Message()
            {
                BodySection = new Data() { Binary = messageValue }
            };

            senderLink.Send(message);
            senderLink.Close();
        }

        static private void ReceiveCommands()
        {
            string entity = Fx.Format("/devices/{0}/messages/deviceBound", DEVICE_ID);

            ReceiverLink receiveLink = new ReceiverLink(session, "receive-link", entity);

            Message received = receiveLink.Receive();
            if (received != null)
            {
                receiveLink.Accept(received);
                //var data = new string(Encoding.UTF8.GetChars((byte[])received.Body));
            }
            receiveLink.Close();
        }

        void TestAzure()
        {
            var localIpAddress= wifiRS21.NetworkInterface.IPAddress;
            if (localIpAddress == null)
            {
                Debug.Print("Cannot send to the Cloud, no local IP-Address!");
                return;
            }

            // Tip: Collaps all #regions in the button_ButtonPressed method to see the sequence of commands

            #region send a blob (here: html page) to Azure. Create container if it does not exist
            Debug.Print("\r\nGoing to create a blob container and send a blob");
            byte[] theDocument = System.Text.Encoding.UTF8.GetBytes("<html><head></head><body>Hello Azure, here is Gadgeteer. Date: " + DateTime.Now.ToLocalTime().ToString() + "</body></html>");

            // calculate MD5 Hash of the blob
            string homeMD5Hash = ComputeHashMD5(theDocument);

            string returnMD5Hash = null;
            HttpStatusCode sendBlobResultCode = sendBlob(myCloudStorageAccount, "con7", theDocument, out returnMD5Hash);    // the containername may only include lower case characters

            if (sendBlobResultCode == HttpStatusCode.Created)
            {
                // Compare the hash computed on the device with the hash computed by Azure
                if (homeMD5Hash == returnMD5Hash)
                {
                    Debug.Print("The blob was sent, return code: " + sendBlobResultCode.ToString() + " and MD5 hashes are equal");
                }
                else
                {
                    Debug.Print("The blob was sent, return code: " + sendBlobResultCode.ToString() + " but MD5 hashes are different");
                }
            }
            else
            {
                Debug.Print("Failed to send the blob, return code: " + sendBlobResultCode.ToString());
            }

            #endregion


            // First we create a new table in the Azure storage account and delete this table at once




            string tableName = string.Empty;
            HttpStatusCode createTableReturnCode = HttpStatusCode.Ambiguous;


            #region Create a table with the name "OutsideTemperature"
            Debug.Print("\r\nGoing to create a table named OutsideTemperature");
            tableName = "OutsideTemperature";
            createTableReturnCode = createTable(myCloudStorageAccount, tableName);

            if (createTableReturnCode == HttpStatusCode.Created)
            { Debug.Print("Table was created: " + tableName + ". HttpStatusCode: " + createTableReturnCode.ToString()); }
            else
            {
                if (createTableReturnCode == HttpStatusCode.Conflict)
                { Debug.Print("Table " + tableName + " already exists"); }
                else
                {
                    if (createTableReturnCode == HttpStatusCode.NoContent)
                    {
                        Debug.Print("Create Table operation. HttpStatusCode: " + createTableReturnCode.ToString());
                    }
                    else
                    {
                        Debug.Print("Failed to create Table " + tableName + ". HttpStatusCode: " + createTableReturnCode.ToString());
                    }
                }
            }
            #endregion


            #region Delete the just created table to show that it works
            Debug.Print("\r\nGoing to delete the table OutsideTemperature");
            tableName = "OutsideTemperature";
            HttpStatusCode deleteTableReturnCode = deleteTable(myCloudStorageAccount, tableName);
            if (deleteTableReturnCode == HttpStatusCode.NoContent)
            { Debug.Print("Table was deleted: " + tableName + ". HttpStatusCode: " + deleteTableReturnCode.ToString()); }
            else
            {
                if (deleteTableReturnCode == HttpStatusCode.NotFound)
                { Debug.Print("Table " + tableName + " not found. HttpStatusCode: " + deleteTableReturnCode.ToString()); }
                else
                { Debug.Print("Failed to delete Table " + tableName + ". HttpStatusCode: " + deleteTableReturnCode.ToString()); }
            }
            #endregion

            // Now we create the table for entity operations

            #region  Create another table with the name "RoomTemperature" which we want to use for some Entity operations
            Debug.Print("\r\nGoing to create Table RoomTemperature");
            tableName = "RoomTemperature";
            createTableReturnCode = createTable(myCloudStorageAccount, tableName);
            if (createTableReturnCode == HttpStatusCode.Created)
            { Debug.Print("Table was created: " + tableName + ". HttpStatusCode: " + createTableReturnCode.ToString()); }
            else
            {
                if (createTableReturnCode == HttpStatusCode.Conflict)
                { Debug.Print("Table " + tableName + " already exists. HttpStatusCode: " + createTableReturnCode.ToString()); }
                else
                { Debug.Print("Failed to create Table " + tableName + ". HttpStatusCode: " + createTableReturnCode.ToString()); }
            }
            #endregion

            // for entity operations we now use the table "RoomTemperature"
            string myTableName = "RoomTemperature";

            // in this example we use the actual year as PartitionKey
            string myPartitionKey = "Year_" + DateTime.Now.Year;

            #region Create a first table entity and send it to the Cloud
            Debug.Print("\r\nGoing to insert a first entity");
            // Now we create an Arraylist to hold the properties of a table Row,
            // write these items to an entity
            // and send this entity to the Cloud

            ArrayList propertiesAL = new System.Collections.ArrayList();
            TableEntityProperty property;

            //Add properties to ArrayList (Name, Value, Type)
            property = new TableEntityProperty("actTemperature", "21", "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));
            property = new TableEntityProperty("location", "livingroom", "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));
            property = new TableEntityProperty("sampleTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));

            RoomTempEntity myRoomTempEntity = new RoomTempEntity(myPartitionKey, Guid.NewGuid().ToString(), propertiesAL);

            string insertEtag = null;
            HttpStatusCode insertEntityReturnCode = insertTableEntity(myCloudStorageAccount, myTableName, myRoomTempEntity, out insertEtag);


            if ((insertEntityReturnCode == HttpStatusCode.Created) || (insertEntityReturnCode == HttpStatusCode.NoContent))
            { Debug.Print("Entity was inserted. HttpStatusCode: " + insertEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to insert Entity. HttpStatusCode: " + insertEntityReturnCode.ToString()); }
            #endregion

            #region Create a second table entity and send it to the Cloud
            Debug.Print("\r\nGoing to insert a second entity");
            propertiesAL.Clear();
            //Add properties to ArrayList with string array of 4 string values in each row (xml-expression, Name, Value, Type)
            property = new TableEntityProperty("actTemperature", "25", "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));
            property = new TableEntityProperty("location", "livingroom", "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));
            property = new TableEntityProperty("sampleTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));

            myRoomTempEntity = new RoomTempEntity(myPartitionKey, Guid.NewGuid().ToString(), propertiesAL);

            insertEtag = null;
            insertEntityReturnCode = insertTableEntity(myCloudStorageAccount, myTableName, myRoomTempEntity, out insertEtag);

            if ((insertEntityReturnCode == HttpStatusCode.Created) || (insertEntityReturnCode == HttpStatusCode.NoContent))
            { Debug.Print("Entity was inserted. HttpStatusCode: " + insertEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to insert Entity. HttpStatusCode: " + insertEntityReturnCode.ToString()); }
            #endregion

            #region Query for selected rows (here: the last row) of the table

            Debug.Print("\r\nGoing to query for Entities");

            // Now we query for the last row of the table as selected by the query string "$top=1"
            ArrayList queryArrayList = new ArrayList();

            //This operation does not work with https, so the CloudStorageAccount is set to use http
            myCloudStorageAccount = new CloudStorageAccount(myAccount, myKey, useHttps: false);
            HttpStatusCode queryEntityReturnCode = queryTableEntities(myCloudStorageAccount, myTableName, "$top=1", out queryArrayList);
            myCloudStorageAccount = new CloudStorageAccount(myAccount, myKey, useHttps: useHTTPS);  // Reset Cloudstorageaccount to the original settings (http or https)


            var entityHashtable = queryArrayList[0] as Hashtable;
            string thisLastRowKey = entityHashtable["RowKey"].ToString();


            if (queryEntityReturnCode == HttpStatusCode.OK)
            { Debug.Print("Query for entities completed. HttpStatusCode: " + queryEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to query Entities. HttpStatusCode: " + queryEntityReturnCode.ToString()); }
            #endregion

            #region Query for a single row as selected by the PartitionKey and the RowKey (unique identifier in a partition) and get the ETag
            Debug.Print("\r\nGoing to query for a single entity");
            // When operation is completed, the hashtable queryResult holds the properties, the string queryETag holds the ETag
            string queryETag = null;
            Hashtable singleEntityHashtable = new Hashtable();

            //This operation does not work with https, so the CloudStorageAccount is set to use http
            myCloudStorageAccount = new CloudStorageAccount(myAccount, myKey, useHttps: false);
            HttpStatusCode querySingleEntityReturnCode = querySingleTableEntity(myCloudStorageAccount, myTableName, myPartitionKey, thisLastRowKey, "", out queryETag, out singleEntityHashtable);
            myCloudStorageAccount = new CloudStorageAccount(myAccount, myKey, useHttps: useHTTPS);  // Reset Cloudstorageaccount to the original settings (http or https)

            if (queryEntityReturnCode == HttpStatusCode.OK)
            { Debug.Print("Query for singel Entity completed. HttpStatusCode: " + querySingleEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to insert Entity. HttpStatusCode: " + querySingleEntityReturnCode.ToString()); }
            #endregion

            #region Update the selected row as selected by PartitionKey and RowKey, use ETag to check for possible changes in the meantime by other users
            Debug.Print("\r\nGoing to update an entity");
            propertiesAL.Clear();
            //Add properties to ArrayList with string array of 4 string values in each row (xml-expression, Name, Value, Type)
            property = new TableEntityProperty("actTemperature", "80", "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));
            property = new TableEntityProperty("location", "kitchen", "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));
            property = new TableEntityProperty("sampleTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Edm.String");
            propertiesAL.Add(makePropertyArray.result(property));

            myRoomTempEntity = new RoomTempEntity(myPartitionKey, thisLastRowKey, propertiesAL);

            //queryETag = "";  // if queryETag == "" the row is updated independent of a possible alteration by another user in the meantime
            string updateReturnETag = null;
            HttpStatusCode updateEntityReturnCode = updateTableEntity(myCloudStorageAccount, myTableName, myPartitionKey, thisLastRowKey, myRoomTempEntity, out updateReturnETag, queryETag);


            if ((updateEntityReturnCode == HttpStatusCode.OK) || (updateEntityReturnCode == HttpStatusCode.NoContent))
            { Debug.Print("Update of Entity completed. HttpStatusCode: " + updateEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to update Entity. HttpStatusCode: " + updateEntityReturnCode.ToString()); }
            #endregion

            #region Delete selected row as selected by PartitionKey and RowKey, use ETag to check for possible changes in the meantime by other users
            Debug.Print("\r\n\rGoing to delete a certain entity");
            // updateReturnETag is the actual ETag of the row, it was retrieved from the preceding updateTableEntity command
            HttpStatusCode deleteEntityReturnCode = deleteTableEntity(myCloudStorageAccount, myTableName, myPartitionKey, thisLastRowKey, updateReturnETag);

            if (deleteEntityReturnCode == HttpStatusCode.NoContent)
            { Debug.Print("Entity deleted. HttpStatusCode: " + deleteEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to delete Entity. HttpStatusCode: " + deleteEntityReturnCode.ToString()); }
            #endregion

            //Debug.GC(true);
        }

        static private bool PutCbsToken(Connection connection, string host, string shareAccessSignature, string audience)
        {
            bool result = true;
            Session session = new Session(connection);

            string cbsReplyToAddress = "cbs-reply-to";
            var cbsSender = new SenderLink(session, "cbs-sender", "$cbs");
            var cbsReceiver = new ReceiverLink(session, cbsReplyToAddress, "$cbs");

            // construct the put-token message
            var request = new Message(shareAccessSignature);
            request.Properties = new Properties();
            request.Properties.MessageId = Guid.NewGuid().ToString();
            request.Properties.ReplyTo = cbsReplyToAddress;
            request.ApplicationProperties = new ApplicationProperties();
            request.ApplicationProperties["operation"] = "put-token";
            request.ApplicationProperties["type"] = "azure-devices.net:sastoken";
            request.ApplicationProperties["name"] = audience;
            cbsSender.Send(request);

            // receive the response
            var response = cbsReceiver.Receive();
            if (response == null || response.Properties == null || response.ApplicationProperties == null)
            {
                result = false;
            }
            else
            {
                int statusCode = (int)response.ApplicationProperties["status-code"];
                string statusCodeDescription = (string)response.ApplicationProperties["status-description"];
                if (statusCode != (int)202 && statusCode != (int)200) // !Accepted && !OK
                {
                    result = false;
                }
            }

            // the sender/receiver may be kept open for refreshing tokens
            cbsSender.Close();
            cbsReceiver.Close();
            session.Close();

            return result;
        }

        private static readonly long UtcReference = (new DateTime(1970, 1, 1, 0, 0, 0, 0)).Ticks;

        static string GetSharedAccessSignature(string keyName, string sharedAccessKey, string resource, TimeSpan tokenTimeToLive)
        {
            // http://msdn.microsoft.com/en-us/library/azure/dn170477.aspx
            // the canonical Uri scheme is http because the token is not amqp specific
            // signature is computed from joined encoded request Uri string and expiry string

            // needed in .Net Micro Framework to use standard RFC4648 Base64 encoding alphabet
            System.Convert.UseRFC4648Encoding = true;
            string expiry = ((long)(DateTime.UtcNow - new DateTime(UtcReference, DateTimeKind.Utc) + tokenTimeToLive).TotalSeconds()).ToString();
            string encodedUri = HttpUtility.UrlEncode(resource);

            byte[] hmac = SHA.computeHMAC_SHA256(Convert.FromBase64String(sharedAccessKey), Encoding.UTF8.GetBytes(encodedUri + "\n" + expiry));
            string sig = Convert.ToBase64String(hmac);

            if (keyName != null)
            {
                return Fx.Format(
                "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                encodedUri,
                HttpUtility.UrlEncode(sig),
                HttpUtility.UrlEncode(expiry),
                HttpUtility.UrlEncode(keyName));
            }
            else
            {
                return Fx.Format(
                    "SharedAccessSignature sr={0}&sig={1}&se={2}",
                    encodedUri,
                    HttpUtility.UrlEncode(sig),
                    HttpUtility.UrlEncode(expiry));
            }
        }
       
        private static void NetworkChange_NetworkAddressChanged(object sender, Microsoft.SPOT.EventArgs e)
        {
            Debug.Print("Network address changed");
        }

        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Debug.Print("Network availability: " + e.IsAvailable.ToString());
        }
        void timer_Tick(GT.Timer timer)
        {
            //ambil data dari sensor
            var Temp = tempHumidSI70.TakeMeasurement();
            //cetak di character display
            string DisplayData = "Temp:" + System.Math.Round(Temp.Temperature) + " C";
            characterDisplay.Clear();
            characterDisplay.Print(DisplayData);
            DisplayData = "Light :" + System.Math.Round(lightSense.GetIlluminance()) + ", Gas:" + System.Math.Round(gasSense.ReadProportion());
            characterDisplay.SetCursorPosition(1, 0);
            characterDisplay.Print(DisplayData);
            //masukin data dari sensor ke class room
            var CurrentCondition = new Room() { Temp = Temp.Temperature, Humid = Temp.RelativeHumidity, Gas = gasSense.ReadProportion(), Light = lightSense.GetIlluminance(), Created=DateTime.Now };
            //serialize ke json
            string Data = Json.NETMF.JsonSerializer.SerializeObject(CurrentCondition);
            SendEvent(Data);
        }
        private void GetInternetTime()
        {
            try
            {
                var NTPTime = new TimeServiceSettings();
                NTPTime.AutoDayLightSavings = true;
                NTPTime.ForceSyncAtWakeUp = true;
                NTPTime.RefreshTime = 3600;
                //Thread.Sleep(1500);
                NTPTime.PrimaryServer = Dns.GetHostEntry("2.ca.pool.ntp.org").AddressList[0].GetAddressBytes();
                NTPTime.AlternateServer = Dns.GetHostEntry("time.nist.gov").AddressList[0].GetAddressBytes();
                //Thread.Sleep(1500);
                TimeService.Settings = NTPTime;
                TimeService.SetTimeZoneOffset(LocalTimeZone); // MST Time zone : GMT-7
                TimeService.SystemTimeChanged += OnSystemTimeChanged;
                TimeService.TimeSyncFailed += OnTimeSyncFailed;
                TimeService.Start();
                //Thread.Sleep(500);
                TimeService.UpdateNow(0);
                //Thread.Sleep(9000);
                //Debug.Print("It is : " + DateTime.Now);

                var time = DateTime.Now;

                Utility.SetLocalTime(time);
                TimeService.Stop();
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }
        private void OnSystemTimeChanged(Object sender, SystemTimeChangedEventArgs e)
        // Called on successful NTP Synchronization
        {
            var now = DateTime.Now; // Used to manipulate dates and time

            #region Check if we are in summer time and thus daylight savings apply

            // Check if we are in Daylight savings. The following algorithm works pour Europe and associated countries
            // In Europe, daylight savings (+60 min) starts the last Sunday of march and ends the last sunday of october

            var aprilFirst = new DateTime(now.Year, 4, 1);
            var novemberFirst = new DateTime(now.Year, 11, 1);
            var sundayshift = new[] { 0, -1, -2, -3, -4, -5, -6 };
            var marchLastSunday = aprilFirst.DayOfYear + sundayshift[(int)aprilFirst.DayOfWeek];
            var octoberLastSunday = novemberFirst.DayOfYear + sundayshift[(int)novemberFirst.DayOfWeek];

            if ((now.DayOfYear >= marchLastSunday) && (now.DayOfYear < octoberLastSunday))
                _daylightSavingsShift = 60;
            else
                _daylightSavingsShift = 0;

            TimeService.SetTimeZoneOffset(LocalTimeZone + _daylightSavingsShift);

            #endregion

            // Display the synchronized date on the Debug Console

            now = DateTime.Now;
            var date = now.ToString("dd/MM/yyyy");
            var here = now.ToString("HH:mm:ss");
            Debug.Print(date + " // " + here);
        }

        private void OnTimeSyncFailed(Object sender, TimeSyncFailedEventArgs e)
        // Called on unsuccessful NTP Synchronization
        {
            Debug.Print("NTPService : Error synchronizing system time with NTP server");
        }

        //AZURE 
        #region Table operations

        private HttpStatusCode updateTableEntity(CloudStorageAccount pCloudStorageAccount, string tableName, string partitionKey, string rowKey, TableEntity pTableEntity, out string returnETag, string selectETag = "")
        {

            table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);

            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

            HttpStatusCode resultCode = table.UpdateTableEntity(tableName, partitionKey, rowKey, pTableEntity, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, TableClient.ResponseType.dont_returnContent, selectETag, useSharedKeyLite: false);
            //var body = table.OperationResponseBody;
            returnETag = table.OperationResponseETag;
            return resultCode;
        }

        private HttpStatusCode queryTableEntities(CloudStorageAccount pCloudStorageAccount, string tableName, string query, out ArrayList queryResult)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);


            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

            HttpStatusCode resultCode = table.QueryTableEntities(tableName, query, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);
            // now we can get the results by reading the properties: table.OperationResponse......
            queryResult = table.OperationResponseQueryList;
            // var body = table.OperationResponseBody;
            // this shows how to get a special value (here the RowKey)of the first entity
            // var entityHashtable = queryResult[0] as Hashtable;
            // var theRowKey = entityHashtable["RowKey"];
            return resultCode;
        }


        private HttpStatusCode querySingleTableEntity(CloudStorageAccount pCloudStorageAccount, string tableName, string partitionKey, string rowKey, string query, out string queryETag, out Hashtable queryResult)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);
            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse("192.168.1.48"), 8888); }

            HttpStatusCode resultCode = table.QueryTableEntities(tableName, partitionKey, rowKey, query, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);
            queryResult = table.OperationResponseSingleQuery;  // Export the properties as an Hashtable
            queryETag = table.OperationResponseETag;           // Export the ETag
            return resultCode;
        }

        private HttpStatusCode deleteTableEntity(CloudStorageAccount pCloudStorageAccount, string tableName, string partitionKey, string rowKey, string ETag = "")
        {
            table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);
            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

            HttpStatusCode resultCode = table.DeleteTableEntity(tableName, partitionKey, rowKey, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIjson, ETag, useSharedKeyLite: false);
            return resultCode;
        }


        private HttpStatusCode deleteTable(CloudStorageAccount pCloudStorageAccount, string pTableName)
        {
            if ((pTableName == "") || (pTableName == string.Empty) || (pTableName == null))
            {
                Debug.Print("It is not implemented to delete all Tables");
                return HttpStatusCode.NotImplemented;
            }
            else
            {
                table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);
                // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
                // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
                if (attachFiddler)
                { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

                HttpStatusCode resultCode = table.DeleteTable(pTableName, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);
                return resultCode;
            }
        }

        private void queryTable(string pTableName = "")
        {
            table = new TableClient(myCloudStorageAccount, caCerts, DebugMode, DebugLevel);
            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

            HttpStatusCode resultCode = table.QueryTables(pTableName, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);
            if (resultCode == HttpStatusCode.OK)
            { Debug.Print("Table(s) were found"); }
            else
            {
                if (resultCode == HttpStatusCode.NotFound)
                { Debug.Print("Table could not be found"); }
                else
                { Debug.Print("Error occured when trying to query for Tables"); }
            }
        }


        private HttpStatusCode createTable(CloudStorageAccount pCloudStorageAccount, string pTableName)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);

            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

            HttpStatusCode resultCode = table.CreateTable(pTableName, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIjson, TableClient.ResponseType.dont_returnContent, useSharedKeyLite: false);
            return resultCode;
        }



        private HttpStatusCode insertTableEntity(CloudStorageAccount pCloudStorageAccount, string pTable, TableEntity pTableEntity, out string pInsertETag)
        {
            table = new TableClient(pCloudStorageAccount, caCerts, DebugMode, DebugLevel);
            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }

            var resultCode = table.InsertTableEntity(pTable, pTableEntity, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIjson, TableClient.ResponseType.dont_returnContent, useSharedKeyLite: false);
            pInsertETag = table.OperationResponseETag;
            //var body = table.OperationResponseBody;
            //Debug.Print("Entity inserted");
            return resultCode;
        }
        #endregion

        #region sendBlob()
        private HttpStatusCode sendBlob(CloudStorageAccount pCloudStorageAccount, string pContainerName, byte[] pDocument, out string returnMD5)
        {
            if (wifiRS21.NetworkInterface.IPAddress != null)
            {
               
                blob = new BlobClient(pCloudStorageAccount, storageServiceVersion: "2015-04-05");
                // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
                // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
                if (attachFiddler)
                { blob.attachFiddler(true, IPAddress.Parse(fiddlerIPAddress), fiddlerPort); }


                var containerName = pContainerName.ToLower();

                // if needed...
                // (the containername may only include lower case characters.  BlobClient.PublicAccessType defines if there is public access 0n container or blob)
                HttpStatusCode createContainerReturnCode = blob.CreateContainer(containerName, BlobClient.PublicAccessType.blob); // .blob means puplix access
                if (createContainerReturnCode == HttpStatusCode.Created)
                { Debug.Print("Container was created: " + containerName + ". HttpStatusCode: " + createContainerReturnCode.ToString()); }
                else
                {
                    if (createContainerReturnCode == HttpStatusCode.Conflict)
                    { Debug.Print("Container " + containerName + " already exists"); }
                    else
                    { Debug.Print("Failed to create Container " + containerName + ". HttpStatusCode: " + createContainerReturnCode.ToString()); }
                }

                HttpStatusCode putBlockBlobReturnCode = blob.PutBlockBlob(containerName, "Hello_Azure_from_Gadgeteer.html", pDocument);

                if (putBlockBlobReturnCode == HttpStatusCode.Created)
                {
                    //Debug.Print("Blob loaded to Cloud");
                    //Debug.Print("Status Code: " + blob.Response_StatusCode + "  " + blob.Response_StatusDescription);
                    //Debug.Print("Date: " + blob.ResponseHeader_Date);
                    //Debug.Print("Last-Moidfied: " + blob.ResponseHeader_Last_Modified);
                    //Debug.Print("ETag: " + blob.ResponseHeader_ETag);
                    //Debug.Print("Content-MD5: " + blob.ResponseHeader_Content_MD5);
                }
                else
                {
                    Debug.Print("An error occured loading Blob to Cloud");
                    Debug.Print("Status Code: " + blob.Response_StatusCode + "  " + blob.Response_StatusDescription);
                }
                returnMD5 = blob.ResponseHeader_Content_MD5;
                return putBlockBlobReturnCode;
            }
            else
            {
                Debug.Print("Could not send, no local IP-Address!");
                returnMD5 = null;
                return HttpStatusCode.BadRequest;
            }
        }
        #endregion

        #region CumputeHashMD5
        private string ComputeHashMD5(byte[] data)
        {
            byte[] hash = new byte[data.Length];
            using (HashAlgorithm md5Check = new HashAlgorithm(HashAlgorithmType.MD5))
            {
                md5Check.TransformBlock(data, 0, data.Length, hash, 0);
                md5Check.TransformFinalBlock(new byte[0], 0, 0);
                hash = md5Check.Hash;
            }
            return Convert.ToBase64String(hash).Replace("!", "+").Replace("*", "/");
        }
        #endregion

    }

    //class penampung data sensor
    public class Room
    {
        public double Temp { set; get; }
        public double Humid { set; get; }
        public double Light { set; get; }
        public double Gas { set; get; } 
        public DateTime Created { set; get; }

    }
}

