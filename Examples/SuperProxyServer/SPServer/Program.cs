using NLog;
using NLog.Config;
using NLog.Targets;
using System;

namespace SPServer
{
    class Program
    {
        private static SuperProxy.Network.SPServer _spServer;

        static void Main(string[] args)
        {
            Console.Title = "Super proxy server";

            LogManager.Configuration = NLogDefaultConfiguration;

            _spServer = new SuperProxy.Network.SPServer();
            _spServer.Initialize("127.0.0.1", 6669, 5, 15, 3);

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
}
