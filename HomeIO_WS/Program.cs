using EngineIO;
using Nancy;
using Nancy.Extensions;
using Nancy.Hosting.Self;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HomeIO_WS {
    class Program {
        static void Main(string[] args) {
            HostConfiguration hostConfigs = new HostConfiguration() {
                UrlReservations = new UrlReservations() { CreateAutomatically = true }
            };
            using (var host = new NancyHost(new Uri("http://localhost:15971"), new DefaultNancyBootstrapper(), hostConfigs)) {
                host.Start();
                Console.WriteLine("Running on http://localhost:15971");
                Console.ReadLine();
            }
        }
    }

    public class HomeIOWSModule : NancyModule {
        static Dictionary<string, Memory> topicToMemory;
        static Dictionary<Memory, string> memoryToTopic;
        static bool init = false;

        public HomeIOWSModule() {
            // register routes
            Get("/", args => string.Join("\n", topicToMemory.Keys.Select(k=>"/home/" + k)));
            Get("/home/{devid*}", args => getValue(topicToMemory[args.devid]));
            Post("/home/{devid*}", args => setValue(args));

            if (!init) {
                init = true;
                // init HomeIO
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
                            topicToMemory.Add(room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic), memory);
                            memoryToTopic.Add(memory, room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic));

                            Console.WriteLine(memory.MemoryType + "," + memory.Address + "," + memory.Name + "," + room + "," + GetTypeForMemory(memory) + "," + "/home/" + room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic));

                            // Listen to memory changes
                            /*if (memory.MemoryType == MemoryType.Input || memory.MemoryType == MemoryType.Memory) {
                                memory.PropertyChanged += Memory_PropertyChanged;
                            }

                            // Subscribe to the topic
                            if (memory.MemoryType == MemoryType.Output) {
                                topics.Add("/home/" + room + "/" + memory.MemoryType + "/" + GetTypeForMemory(memory) + "/" + Clean(topic));
                                qosLevels.Add(MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE);
                            }*/
                        }
                    }
                }
                Console.WriteLine(topicToMemory.Count);
                if (/*topics.Count != qosLevels.Count ||*/ topicToMemory.Count == 0 || memoryToTopic.Count == 0) {
                    Console.WriteLine("! Home IO init FAILED\n\nMake sure Home IO is running and that at least one device has its DeviceMode set to External\n");
                    Console.WriteLine("! Restart this application");
                }

                Thread t = new Thread(new ParameterizedThreadStart(HomeIOUpdate));
                t.Start(homeio);

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

        private String setValue(dynamic args) {
            Memory mem = topicToMemory[args.devid];
            var str = Request.Body.AsString();
            try {
                switch (mem.GetType().Name) {
                    case "MemoryBit":
                        (mem as MemoryBit).Value = bool.Parse(str);
                        break;
                    case "MemoryByte":
                        (mem as MemoryByte).Value = byte.Parse(str);
                        break; ;
                    case "MemoryShort":
                        (mem as MemoryShort).Value = short.Parse(str);
                        break;
                    case "MemoryInt":
                        (mem as MemoryInt).Value = int.Parse(str);
                        break;
                    case "MemoryLong":
                        (mem as MemoryLong).Value = long.Parse(str);
                        break; 
                    case "MemoryFloat":
                        (mem as MemoryFloat).Value = float.Parse(str);
                        break;
                    case "MemoryDouble":
                        (mem as MemoryDouble).Value = double.Parse(str);
                        break;
                    case "MemoryString":
                        (mem as MemoryString).Value = args.value;
                        break;
                    case "MemoryDateTime":
                        (mem as MemoryDateTime).Value = DateTime.Parse(str);
                        break;
                    case "MemoryTimeSpan":
                        (mem as MemoryTimeSpan).Value = TimeSpan.Parse(str);
                        break;
                    default: break;
                }
            } catch(Exception ex) {
                return args.devid + " NOT set to " + getValue(mem) + "\n Error: " + ex.Message;
            }
            return args.devid + " set to " + getValue(mem);
        }

        private String getValue(Memory mem) {
            object value = "";
            switch (mem.GetType().Name) {
                case "MemoryBit":
                    value = (mem as MemoryBit).Value;
                    break;
                case "MemoryByte":
                    value = (mem as MemoryByte).Value;
                    break; ;
                case "MemoryShort":
                    value = (mem as MemoryShort).Value;
                    break;
                case "MemoryInt":
                    value = (mem as MemoryInt).Value;
                    break;
                case "MemoryLong":
                    value = (mem as MemoryLong).Value;
                    break;
                case "MemoryFloat":
                    value = (mem as MemoryFloat).Value;
                    break;
                case "MemoryDouble":
                    value = (mem as MemoryDouble).Value;
                    break;
                case "MemoryString":
                    value = (mem as MemoryString).Value;
                    break;
                case "MemoryDateTime":
                    value = (mem as MemoryDateTime).Value;
                    break;
                case "MemoryTimeSpan":
                    value = (mem as MemoryTimeSpan).Value;
                    break;
                default: break;
            }
            return value.ToString();
        }

        // Memory update callback
        private void Memory_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
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
            /*foreach (MqttClient client in clients) {
                Console.WriteLine("Publishing to client " + topic);
                client.Publish(topic, Encoding.UTF8.GetBytes(value.ToString()));
            }*/
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
