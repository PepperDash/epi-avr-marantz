using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core.Queues;

namespace PDT.Plugins.Marantz
{
    /// <summary>
    /// In-process stand-in for the receiver's control port. Commands sent by the driver are answered
    /// by a <see cref="MockMarantzAvrResponder"/> on a separate thread, one at a time and in send order,
    /// like replies from a real port. Changes made at the receiver (from the console) are sent unprompted
    /// on the same thread, as a real unit reports them to every open session.
    /// </summary>
    /// <remarks>
    /// Not an ISocketStatus: the driver only uses socket status for logging, and a serial-style comm keeps
    /// the mock free of connection state.
    /// </remarks>
    internal class MockMarantzAvrCommunication : IBasicCommunication
    {
        private const char Delimiter = '\r';

        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private readonly MockMarantzAvrResponder responder;
        private readonly GenericQueue replyQueue;
        private readonly int responseDelayMs;
        private readonly object bufferLock = new object();
        private string rxBuffer = string.Empty;

        /// <summary>
        /// Creates a mock comm whose replies come from the given responder
        /// </summary>
        public MockMarantzAvrCommunication(string key, MockMarantzAvrResponder responder, int responseDelayMs)
        {
            Key = key;
            this.responder = responder;
            this.responseDelayMs = Math.Max(0, responseDelayMs);
            replyQueue = new GenericQueue(key + "-replies");

            // Changes made at the receiver take their turn behind commands already queued, then report
            responder.PanelChange += change => replyQueue.Enqueue(new ActionMessage(() => Send(change())));
        }

        /// <inheritdoc />
        public string Key { get; private set; }

        /// <inheritdoc />
        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        /// <inheritdoc />
        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;

        /// <inheritdoc />
        public bool IsConnected { get; private set; }

        /// <inheritdoc />
        public void Connect()
        {
            IsConnected = true;
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            IsConnected = false;
        }

        /// <inheritdoc />
        public void SendText(string text)
        {
            if (!IsConnected || string.IsNullOrEmpty(text))
            {
                return;
            }

            lock (bufferLock)
            {
                rxBuffer += text;

                int delimiter;
                while ((delimiter = rxBuffer.IndexOf(Delimiter)) >= 0)
                {
                    var line = rxBuffer.Substring(0, delimiter);
                    rxBuffer = rxBuffer.Substring(delimiter + 1);

                    if (line.Trim().Length > 0)
                    {
                        replyQueue.Enqueue(new ProcessStringMessage(line, Reply));
                    }
                }
            }
        }

        /// <inheritdoc />
        public void SendBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return;
            }

            SendText(Latin1.GetString(bytes, 0, bytes.Length));
        }

        // Runs on the reply queue's thread, one command at a time, in send order
        private void Reply(string line)
        {
            Send(responder.Respond(line));
        }

        private void Send(IList<string> lines)
        {
            foreach (var line in lines)
            {
                if (responseDelayMs > 0)
                {
                    CrestronEnvironment.Sleep(responseDelayMs);
                }

                // Checked per line, after the delay: a fault set mid-reply (or with lines still queued)
                // must stop output at once, or late lines keep the communication monitor online
                if (!IsConnected || responder.Fault == MockMarantzAvrFault.Silent)
                {
                    return;
                }

                var text = line + Delimiter;

                var bytesHandler = BytesReceived;
                if (bytesHandler != null)
                {
                    bytesHandler(this, new GenericCommMethodReceiveBytesArgs(Latin1.GetBytes(text)));
                }

                var textHandler = TextReceived;
                if (textHandler != null)
                {
                    textHandler(this, new GenericCommMethodReceiveTextArgs(text));
                }
            }
        }

        private class ActionMessage : IQueueMessage
        {
            private readonly Action action;

            public ActionMessage(Action action)
            {
                this.action = action;
            }

            public void Dispatch()
            {
                action();
            }
        }
    }
}
