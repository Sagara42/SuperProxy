using NLog;
using NLog.Config;
using NLog.Targets;
using SuperProxy.Network;
using System;
using System.Threading;

namespace SPClientNode2
{
    internal class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static SPClient _client;

        static void Main(string[] args)
        {
            Console.Title = "Super proxy node 2";

            LogManager.Configuration = NLogDefaultConfiguration;

            _client = new SPClient();
            _client.Connect("127.0.0.1", 6669);

            Thread.Sleep(1000);

            _client.Publish("testChannel", "Some message", "Hello! Im node 2!");

            Test();

            Console.Read();
        }

        private static async void Test()
        {
            var foo = "Foo as string";
            var boo = 256;

            await _client.RemoteCall("testChannel", "TestRemoteMethod", "node 2", foo, boo);
            var fooObject = await _client.RemoteCall<Foo>("testChannel", "GetFoo");

            _log.Info($"foo object received:\n{fooObject.ToString()}");
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

        public override string ToString()
        {
            return 
                $"String:{String}\n" +
                $"Byte:{Byte}\n" +
                $"Short:{Short}\n" +
                $"Int:{Int}\n" +
                $"Decimal:{Decimal}\n" +
                $"Long:{Long}\n" +
                $"GUID:{Guid}\n" +
                $"DateTime:{SomeTime}";
        }
    }
}
