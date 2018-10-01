using EngineIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace HomeIO_MQTT {
    class Program {
        static List<MqttClient> clients = new List<MqttClient>();
        static Dictionary<string, Memory> topicToMemory;
        static Dictionary<Memory, string> memoryToTopic;

        static void Main(string[] args) {
            Console.WriteLine("Initializing Home IO...");
            MemoryBit[] bitI, bitO, bitIO;
            MemoryByte[] byteI, byteO, byteIO;
            MemoryShort[] shortI, shortO, shortIO;
            MemoryInt[] intI, intO, intIO;
            MemoryLong[] longI, longO, longIO;
            MemoryFloat[] floatI, floatO, floatIO;
            MemoryDouble[] doubleI, doubleO, doubleIO;
            MemoryString[] stringI, stringO, stringIO;
            MemoryDateTime[] dtI, dtO, dtIO;
            MemoryTimeSpan[] tsI, tsO, tsIO;

            MemoryMap homeio = MemoryMap.Instance;

            bitI = homeio.GetBitMemories(MemoryType.Input);
            bitO = homeio.GetBitMemories(MemoryType.Output);
            bitIO = homeio.GetBitMemories(MemoryType.Memory);

            byteI = homeio.GetByteMemories(MemoryType.Input);
            byteO = homeio.GetByteMemories(MemoryType.Output);
            byteIO = homeio.GetByteMemories(MemoryType.Memory);

            shortI = homeio.GetShortMemories(MemoryType.Input);
            shortO = homeio.GetShortMemories(MemoryType.Output);
            shortIO = homeio.GetShortMemories(MemoryType.Memory);

            intI = homeio.GetIntMemories(MemoryType.Input);
            intO = homeio.GetIntMemories(MemoryType.Output);
            intIO = homeio.GetIntMemories(MemoryType.Memory);

            longI = homeio.GetLongMemories(MemoryType.Input);
            longO = homeio.GetLongMemories(MemoryType.Output);
            longIO = homeio.GetLongMemories(MemoryType.Memory);

            floatI = homeio.GetFloatMemories(MemoryType.Input);
            floatO = homeio.GetFloatMemories(MemoryType.Output);
            floatIO = homeio.GetFloatMemories(MemoryType.Memory);

            doubleI = homeio.GetDoubleMemories(MemoryType.Input);
            doubleO = homeio.GetDoubleMemories(MemoryType.Output);
            doubleIO = homeio.GetDoubleMemories(MemoryType.Memory);

            stringI = homeio.GetStringMemories(MemoryType.Input);
            stringO = homeio.GetStringMemories(MemoryType.Output);
            stringIO = homeio.GetStringMemories(MemoryType.Memory);

            dtI = homeio.GetDateTimeMemories(MemoryType.Input);
            dtO = homeio.GetDateTimeMemories(MemoryType.Output);
            dtIO = homeio.GetDateTimeMemories(MemoryType.Memory);

            tsI = homeio.GetTimeSpanMemories(MemoryType.Input);
            tsO = homeio.GetTimeSpanMemories(MemoryType.Output);
            tsIO = homeio.GetTimeSpanMemories(MemoryType.Memory);

            homeio.Update();

            List<string> topics = new List<string>();
            List<byte> qosLevels = new List<byte>();

            topicToMemory = new Dictionary<string, Memory>();
            memoryToTopic = new Dictionary<Memory, string>();

            Regex pattern = new Regex(@"[A-Z]\ -\ *");

            Memory[][] allMemories = new Memory[][] {
                bitI, byteI, shortI, intI, longI, floatI, doubleI, stringI, dtI, tsI,
                bitO, byteO, shortO, intO, longO, floatO, doubleO, stringO, dtO, tsO,
                bitIO, byteIO, shortIO, intIO, longIO, floatIO, doubleIO, stringIO, dtIO, tsIO
            };
            foreach (var memoryArray in allMemories) {
                foreach (var memory in memoryArray) {
                    if (memory.HasName) {
                        string room = "general";
                        string topic = memory.Name;
                        if (pattern.Match(topic).Success) {
                            room = topic.Substring(0, 1);
                            topic = topic.Substring(4);
                        }
                        topicToMemory.Add("/home/" + room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic), memory);
                        memoryToTopic.Add(memory, "/home/" + room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic));

                        Console.WriteLine(memory.MemoryType + "," + memory.Address + "," + memory.Name + "," + room + "," + GetTypeForMemory(memory) + "," + "/home/" + room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic));

                        // Listen to memory changes
                        if (memory.MemoryType == MemoryType.Input || memory.MemoryType == MemoryType.Memory) {
                            memory.PropertyChanged += Memory_PropertyChanged;
                        }

                        // Subscribe to the topic
                        if (memory.MemoryType == MemoryType.Output) {
                            topics.Add("/home/" + room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic));
                            qosLevels.Add(MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE);
                        }
                    }
                }
            }

            if(topics.Count != qosLevels.Count || topics.Count==0 || qosLevels.Count == 0) {
                Console.WriteLine("Home IO init FAILED\n\nMake sure Home IO is running\n");
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
                return;
            }


            // parametrized
            foreach (string param in args) {
                try { 
                    MqttClient client = new MqttClient(param);
                    client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                    client.Connect(Guid.NewGuid().ToString());
                    client.Subscribe(topics.ToArray(), qosLevels.ToArray());
                    clients.Add(client);
                    Console.WriteLine("\nConnected to broker at " + param + "\n");
                } catch(Exception e) {
                    Console.WriteLine("Unable to connect to broker " + param + "\n->" + e);
                }
            }
            // default
            if(args.Length == 0) {
                try {
                    MqttClient client = new MqttClient("localhost");
                    client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                    client.Connect(Guid.NewGuid().ToString());
                    client.Subscribe(topics.ToArray(), qosLevels.ToArray());
                    clients.Add(client);
                    Console.WriteLine("\nConnected to broker at localhost\n");
                } catch (Exception e) {
                    Console.WriteLine("Unable to connect to broker localhost\n->" + e);
                }
            }

            Thread t = new Thread(new ParameterizedThreadStart(HomeIOUpdate));
            t.Start(homeio);

            Console.WriteLine("\nConnected\n\nRunning...");
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();

            t.Abort();
            foreach (MqttClient client in clients) {
                client.Disconnect();
            }
            homeio.Dispose();
        }


        // MQTT message received
        private static void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e) {
            Memory memory = topicToMemory[e.Topic];
            string message = Encoding.UTF8.GetString(e.Message);

            switch (memory.GetType().Name) {
                case "MemoryBit":
                    (memory as MemoryBit).Value = bool.Parse(message);
                    break;
                case "MemoryByte":
                    (memory as MemoryByte).Value = byte.Parse(message);
                    break; ;
                case "MemoryShort":
                    (memory as MemoryShort).Value = short.Parse(message);
                    break; 
                case "MemoryInt":
                    (memory as MemoryInt).Value = int.Parse(message);
                    break;
                case "MemoryLong":
                    (memory as MemoryLong).Value = long.Parse(message);
                    break;
                case "MemoryFloat":
                    (memory as MemoryFloat).Value = float.Parse(message);
                    break;
                case "MemoryDouble":
                    (memory as MemoryDouble).Value = double.Parse(message);
                    break;
                case "MemoryString":
                    (memory as MemoryString).Value = message;
                    break;
                case "MemoryDateTime":
                    (memory as MemoryDateTime).Value = DateTime.Parse(message);
                    break;
                case "MemoryTimeSpan":
                    (memory as MemoryTimeSpan).Value = TimeSpan.Parse(message);
                    break;
                default: break;
            }
        }


        // Threaded home IO update
        private static void HomeIOUpdate(object parameter) {
            MemoryMap homeio = parameter as MemoryMap;
            try {
                while (true) {
                    homeio.Update();
                    Thread.Sleep(100);
                }
            } catch (ThreadAbortException) { }
        }

        // Memory update callback
        private static void Memory_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            string topic = memoryToTopic[sender as Memory];
            object value = "";

            switch (sender.GetType().Name) {
                case "MemoryBit":
                    value = (sender as MemoryBit).Value;
                    break;
                case "MemoryByte":
                    value = (sender as MemoryByte).Value;
                    break; ;
                case "MemoryShort":
                    value = (sender as MemoryShort).Value;
                    break;
                case "MemoryInt":
                    value = (sender as MemoryInt).Value;
                    break;
                case "MemoryLong":
                    value = (sender as MemoryLong).Value;
                    break;
                case "MemoryFloat":
                    value = (sender as MemoryFloat).Value;
                    break;
                case "MemoryDouble":
                    value = (sender as MemoryDouble).Value;
                    break;
                case "MemoryString":
                    value = (sender as MemoryString).Value;
                    break;
                case "MemoryDateTime":
                    value = (sender as MemoryDateTime).Value;
                    break;
                case "MemoryTimeSpan":
                    value = (sender as MemoryTimeSpan).Value;
                    break;
                default: break;
            }
            foreach (MqttClient client in clients) {
                client.Publish(topic, Encoding.UTF8.GetBytes(value.ToString()));
            }
        }

        // Removes characters that shouldn't be in the topic names
        static string Clean(string input) {
            return input.Replace(' ', '_').Replace('/', '-');
        }

        // Returns the topic path type for an input memory
        static string GetTypeForMemory(Memory input) {
            switch (input.GetType().Name) {
                case "MemoryBit": return "bool";
                case "MemoryByte": return "byte";
                case "MemoryShort": return "short";
                case "MemoryInt": return "int";
                case "MemoryLong": return "long";
                case "MemoryFloat": return "float";
                case "MemoryDouble": return "double";
                case "MemoryString": return "string";
                case "MemoryDateTime": return "DateTime";
                case "MemoryTimeSpan": return "TimeSpan";
                default: return "unknown";
            }
        }
    }
}
