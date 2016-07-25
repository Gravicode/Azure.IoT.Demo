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
using Gadgeteer.Modules.GHIElectronics;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using Microsoft.SPOT.Net.NetworkInformation;
using Microsoft.SPOT.Time;
using Microsoft.SPOT.Hardware;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace RoomMonitoringMqtt
{
    public partial class Program
    {
        //TimeServer Settings
        private int _daylightSavingsShift;
        private const int LocalTimeZone = 7 * 60;
        public static MqttClient client { set; get; }
        const string DeviceId = "Room-Sensor";
        const string MQTT_BROKER_ADDRESS = "iot-gateway.azure-devices.net";
        const string Username = "iot-gateway.azure-devices.net/Room-Sensor";
        const string Pass = "SharedAccessSignature sr=iot-gateway.azure-devices.net%2fdevices%2fRoom-Sensor&sig=5Ae9tHQVmFeXvptiCt72D9z%2fY3nbvLPng9KcMlgrTnU%3d&se=1500982158&skn=iothubowner";
        const string PublishedTopic = "devices/" + DeviceId + "/messages/events/";
        void SubscribeMessage()
        {
            //event handler saat message dari mqtt masuk
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            //subcribe ke topik tertentu di mqtt
            client.Subscribe(new string[] { "devices/"+DeviceId+"/messages/devicebound/" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

        }

        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            //conversi byte message ke string
            string pesan = new string(Encoding.UTF8.GetChars(e.Message));
            if (e.Topic == "/iot/control")
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
            Debug.Print("Message : " + pesan + ", topic:" + e.Topic);
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
        }
        // This method is run when the mainboard is powered up or reset.   
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

            //konek ke mqtt broker
           
           var cert = new X509Certificate(Resources.GetBytes(Resources.BinaryResources.mqtt_ca));
            client = new MqttClient(MQTT_BROKER_ADDRESS,
            8883,
            true,
            null,cert
            ,MqttSslProtocols.SSLv3);
            
            client.Connect(DeviceId, Username, Pass);
            //subcribe topik
            SubscribeMessage();


            //setup timer untuk ambil data sensor ruangan
            GT.Timer timer = new GT.Timer(2000);
            timer.Tick += timer_Tick;
            timer.Start();

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
            var CurrentCondition = new Room() { Temp = Temp.Temperature, Humid = Temp.RelativeHumidity, Gas = gasSense.ReadProportion(), Light = lightSense.GetIlluminance() };
            //serialize ke json
            string Data = Json.NETMF.JsonSerializer.SerializeObject(CurrentCondition);
            //kirim ke mqtt broker
            client.Publish(PublishedTopic, Encoding.UTF8.GetBytes(Data), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
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
    }
    public class Room
    {
        public double Temp { set; get; }
        public double Humid { set; get; }
        public double Light { set; get; }
        public double Gas { set; get; }
        public DateTime Created { set; get; }

    }
}
