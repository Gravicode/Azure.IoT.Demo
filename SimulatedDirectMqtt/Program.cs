using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Newtonsoft.Json;
using static System.Console;
namespace SimulatedDirectMqtt
{
    class Program
    {
        public static MqttClient client { set; get; }

        const string DeviceId = "Room-Sensor";
        const string MQTT_BROKER_ADDRESS = "iot-gateway.azure-devices.net";
        const string Username = "iot-gateway.azure-devices.net/Room-Sensor";
        const string Pass = "SharedAccessSignature sr=iot-gateway.azure-devices.net&sig=EkLNrnrX%2FN6EEziP4fFXqHSOXaIlVxvoABMmeAg6SHk%3D&se=1501684848&skn=iothubowner";
        //"SharedAccessSignature sig=xdBQ%2bdCAXYhBAhQDXsaR8kI%2bRsdwCHlDIw15q6u4Pzk%3d&se=1469450357&sr=iot-gateway.azure-devices.net%2fdevices%2fRoom-Sensor&skn=iothubowner";
        //"SharedAccessSignature sr=iot-gateway.azure-devices.net%2fdevices%2fRoom-Sensor&sig=5Ae9tHQVmFeXvptiCt72D9z%2fY3nbvLPng9KcMlgrTnU%3d&se=1500982158&skn=iothubowner";

        const string PublishedTopic = "devices/" + DeviceId + "/messages/events/";
        const string SubcribeTopic = "devices/" + DeviceId + "/messages/devicebound/#";
        //const string SubcribeTopic2 = "devices/" + DeviceId + "/messages/devicebound/";
        static void Main(string[] args)
        {
          
            //StartSimulation();
            Task tsk = new Task(new Action(StartSimulation));
            tsk.Start();
            Thread.Sleep(Timeout.Infinite);
        }

        static void StartSimulation()
        {
            Random rnd = new Random(Environment.TickCount);
            var cert = new X509Certificate(Resource1.mqtt_ca);
            var client_cert = new X509Certificate(Resource1.BaltimoreCyberTrustRoot);
            client = new MqttClient(MQTT_BROKER_ADDRESS,
            8883,
            true,
            cert, client_cert
            , MqttSslProtocols.TLSv1_1);
            client.Connect(DeviceId, Username, Pass);
            //subscribe
            //subcribe ke topik tertentu di mqtt
            //event handler saat message dari mqtt masuk
            client.MqttMsgPublishReceived += (object sender, MqttMsgPublishEventArgs e) =>
            {
                //conversi byte message ke string
                string pesan = new string(Encoding.UTF8.GetChars(e.Message));

                //cetak ke console tuk kebutuhan debug
                WriteLine("Message : " + pesan + ", topic:" + e.Topic);
            };
            client.Subscribe(new string[] { SubcribeTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            client.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;

            while (true)
            {
                var Node = new Room() { Created = DateTime.Now, Gas = rnd.Next(100), Humid = rnd.Next(100), Light = rnd.Next(100), Temp = rnd.Next(100) };
                //serialize ke json
                string Data = JsonConvert.SerializeObject(Node);
                //kirim ke mqtt broker
                client.Publish(PublishedTopic, Encoding.UTF8.GetBytes(Data), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
                Thread.Sleep(2000);
            }
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
