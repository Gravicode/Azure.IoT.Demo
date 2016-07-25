using System;
using Microsoft.SPOT;
using System.Collections;
using PervasiveDigital.Json;
using RoSchmi.Net.Azure.Storage;

namespace RoomMonitoring
{
    public class RoomTempEntity : TableEntity
    {
public string actTemperature { get; set; }
        public string location { get; set; }

        // Your entity type must expose a parameter-less constructor
        public RoomTempEntity() { }

        // Define the PK and RK
        public RoomTempEntity(string partitionKey, string rowKey, ArrayList pProperties)
            : base(partitionKey, rowKey)
        {
            //this.TimeStamp = DateTime.Now;
            this.Properties = pProperties;    // store the ArrayList

            var myProperties = new PropertyClass()
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                // get the values out of the ArrayList
                actTemperature = ((string[])this.Properties[0])[2],  // Row 0, arrayfield 2
                location = ((string[])this.Properties[1])[2],        // Row 1, arrayfield 2
                sampleTime = ((string[])this.Properties[2])[2]       // Row 2, arrayfield 2
            };

            this.JsonString = JsonConverter.Serialize(myProperties).ToString();
        }
        private class PropertyClass
        {
            public string RowKey;
            public string PartitionKey;
            public string sampleTime;
            public string actTemperature;
            public string location;
        }

    }
}


//*******************************************************

        /*
    }
    public class TempEntity_PropertyClass
    {
        public string RowKey;
        public string PartitionKey;
        public string actTemperature;
        public string location;
    }
    */
    /*
    var test = new TestClass()
            {
                aString = "A string",
                i = 10,
                someName = "who?",
                Timestamp = DateTime.UtcNow,
                intArray = new[] { 1, 3, 5, 7, 9 },
                stringArray = new[] { "two", "four", "six", "eight" },
                child1 = new ChildClass() { one = 1, two = 2, three = 3 },
                Child = new ChildClass() { one = 100, two = 200, three = 300 }
            };
            var result = JsonConverter.Serialize(test);
            Debug.Print("Serialization:");
            var stringValue = result.ToString();
            Debug.Print(stringValue);
    */


