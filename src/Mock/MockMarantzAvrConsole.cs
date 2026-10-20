using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using PepperDash.Core;

namespace PDT.Plugins.Marantz
{
    /// <summary>
    /// "mockmarantz" console command for driving mock receivers by device key
    /// </summary>
    internal static class MockMarantzAvrConsole
    {
        private const string Usage =
            "mockmarantz <deviceKey> status | power on|off | input <SAT/CBL|DVD|BD|TV|...> | volume 0-980 | mute on|off | fault none|silent";

        // The console rejects help text over 79 bytes; the full usage prints when the command is run without arguments
        private const string Help = "Mock Marantz AVR: mockmarantz <key> status|power|input|volume|mute|fault";

        private static readonly Dictionary<string, MockMarantzAvrResponder> Responders =
            new Dictionary<string, MockMarantzAvrResponder>(StringComparer.OrdinalIgnoreCase);

        private static readonly object RegisterLock = new object();
        private static bool commandAdded;

        /// <summary>
        /// Makes a mock receiver reachable from the console, adding the command on first use
        /// </summary>
        public static void Register(string deviceKey, MockMarantzAvrResponder responder)
        {
            lock (RegisterLock)
            {
                Responders[deviceKey] = responder;

                if (commandAdded)
                {
                    return;
                }

                // The console command is a convenience: never let it stop the receiver from loading
                // Only a successful add counts, so the next mock to register retries after a failure.
                try
                {
                    commandAdded = CrestronConsole.AddNewConsoleCommand(Handle, "mockmarantz", Help, ConsoleAccessLevelEnum.AccessOperator);
                    if (!commandAdded)
                    {
                        Debug.LogError("Unable to add the mockmarantz console command (it may already exist)");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to add the mockmarantz console command: {0}", e.Message);
                }
            }
        }

        private static void Handle(string args)
        {
            var parts = (args ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Mocks can still be registering while the command runs, so read the dictionary under the lock
            MockMarantzAvrResponder responder = null;
            string[] keys;
            lock (RegisterLock)
            {
                if (parts.Length >= 1)
                {
                    Responders.TryGetValue(parts[0], out responder);
                }

                keys = Responders.Keys.ToArray();
            }

            if (parts.Length < 2 || responder == null)
            {
                CrestronConsole.ConsoleCommandResponse("Usage: {0}\r\nMock receivers: {1}\r\n", Usage, string.Join(", ", keys));
                return;
            }

            var verb = parts[1].ToLower();
            // Input names can contain a slash but no spaces (SAT/CBL); keep their case for the reply
            var arg = parts.Length > 2 ? parts[2] : string.Empty;
            var ok = true;

            switch (verb)
            {
                case "status":
                    break;
                case "power":
                    ok = arg.Equals("on", StringComparison.OrdinalIgnoreCase) || arg.Equals("off", StringComparison.OrdinalIgnoreCase);
                    if (ok) responder.SetPower(arg.Equals("on", StringComparison.OrdinalIgnoreCase));
                    break;
                case "input":
                    ok = responder.SetInput(arg);
                    break;
                case "volume":
                    int level;
                    ok = int.TryParse(arg, out level) && level >= 0 && level <= 980;
                    if (ok) responder.SetVolume(level);
                    break;
                case "mute":
                    ok = arg.Equals("on", StringComparison.OrdinalIgnoreCase) || arg.Equals("off", StringComparison.OrdinalIgnoreCase);
                    if (ok) responder.SetMute(arg.Equals("on", StringComparison.OrdinalIgnoreCase));
                    break;
                case "fault":
                    ok = arg.Equals("none", StringComparison.OrdinalIgnoreCase) || arg.Equals("silent", StringComparison.OrdinalIgnoreCase);
                    if (ok) responder.Fault = arg.Equals("silent", StringComparison.OrdinalIgnoreCase) ? MockMarantzAvrFault.Silent : MockMarantzAvrFault.None;
                    break;
                default:
                    ok = false;
                    break;
            }

            if (!ok)
            {
                CrestronConsole.ConsoleCommandResponse("Usage: {0}\r\n", Usage);
                return;
            }

            // status and fault take effect now. The other changes are queued behind any commands the receiver
            // is still answering, then reported to the driver, so the state isn't theirs to show yet.
            if (verb == "status" || verb == "fault")
            {
                CrestronConsole.ConsoleCommandResponse("{0}: {1}\r\n", parts[0], responder.Describe());
                return;
            }

            CrestronConsole.ConsoleCommandResponse("{0}: queued {1} {2} (check with: mockmarantz {0} status)\r\n",
                parts[0], verb, arg);
        }
    }
}
