using NLog;
using NLog.Config;
using NLog.Targets;
using SuperProxy.Network;
using SuperProxy.Network.Attributes;
using System;
using System.Threading;

namespace SPClientNode1
{
    internal class Program
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static SPClient _client;

        static void Main(string[] args)
        {
            Console.Title = "Super proxy node 1";

            LogManager.Configuration = NLogDefaultConfiguration;

            _client = new SPClient(new TestHostedObject(), "testChannel");
            _client.Connect("127.0.0.1", 6669);

            Thread.Sleep(1000);

            _client.Subsribe("testChannel", (action) => 
            {
                Log.Info($"Message received from channel:{action.Channel}");
                Log.Info(action.Message.Header);
                Log.Info(action.Message.Data.ToString());
            });
          
            Console.Read();
        }

        private static LoggingConfiguration NLogDefaultConfiguration
        {
            get
            {
                var config = new LoggingConfiguration();
                var consoleTarget = new ColoredConsoleTarget
                {
                    Layout = "${time} | ${message}${onexception:${newline}EXCEPTION OCCURRED${newline}${exception:format=tostring}}",
                    UseDefaultRowHighlightingRules = false
                };

                config.AddTarget("console", consoleTarget);

                var fileTarget = new FileTarget
                {
                    Layout = "${time} | ${message}${onexception:${newline}EXCEPTION OCCURRED${newline}${exception:format=tostring}}",
                    FileName = "${basedir}/Logs/${shortdate}/${level}.txt"
                };

                config.AddTarget("file", fileTarget);

                consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule("level == LogLevel.Debug", ConsoleOutputColor.DarkGray, ConsoleOutputColor.Black));
                consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule("level == LogLevel.Info", ConsoleOutputColor.White, ConsoleOutputColor.Black));
                consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule("level == LogLevel.Warn", ConsoleOutputColor.Yellow, ConsoleOutputColor.Black));
                consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule("level == LogLevel.Error", ConsoleOutputColor.Red, ConsoleOutputColor.Black));
                consoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule("level == LogLevel.Fatal", ConsoleOutputColor.Red, ConsoleOutputColor.White));

                var rule1 = new LoggingRule("*", LogLevel.Debug, consoleTarget);
                config.LoggingRules.Add(rule1);

                var rule2 = new LoggingRule("*", LogLevel.Debug, fileTarget);
                config.LoggingRules.Add(rule2);

                return config;
            }
        }
    }

    public class TestHostedObject
    {
        [SPMessage]
        public void TestRemoteMethod(string nodeName, string foo, int boo)
        {
            Program.Log.Info($"{nodeName} callback me, Foo:{foo} Boo:{boo}");
        }

        [SPMessage]
        public Foo GetFoo() => new Foo("Some string", byte.MaxValue, short.MaxValue, int.MaxValue, decimal.MaxValue, long.MaxValue, Guid.NewGuid(), DateTime.Now);
    }

    public class Foo
    {
        public string String { get; set; }
        public byte Byte { get; set; }
        public short Short { get; set; }
        public int Int { get; set; }
        public decimal Decimal { get; set; }
        public long Long { get; set; }
        public Guid Guid { get; set; }
        public DateTime SomeTime { get; set; }

        public Foo(string s, byte b, short shrt, int i, decimal d, long l, Guid g, DateTime dt)
        {
            String = s;
            Byte = b;
            Short = shrt;
            Int = i;
            Decimal = d;
            Long = l;
            Guid = g;
            SomeTime = dt;
        }

        public Foo() { }
    }
}
