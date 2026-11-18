using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SnipPrompt
{
    /// One running instance. A second start (e.g. the pinned taskbar icon) only signals
    /// the running instance to start a capture and exits.
    sealed class SingleInstance
    {
        const string MutexName = @"Local\SnipPrompt.Instance";
        const string EventName = @"Local\SnipPrompt.Capture";

        Mutex mutex;
        EventWaitHandle signal;

        public bool TryBecomePrimary()
        {
            bool created;
            mutex = new Mutex(true, MutexName, out created);
            if (created) signal = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            return created;
        }

        /// Called by a second instance. Grants the running instance the right to take the
        /// foreground, which the second instance has because the user just clicked it.
        public static void SignalPrimary()
        {
            try
            {
                AllowSetForegroundWindow(ASFW_ANY);
                using (var ev = EventWaitHandle.OpenExisting(EventName)) ev.Set();
            }
            catch (Exception) { }
        }

        public void Listen(Action onSignal)
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    signal.WaitOne();
                    onSignal();
                }
            }) { IsBackground = true, Name = "SnipPrompt.Signal" };
            t.Start();
        }

        const int ASFW_ANY = -1;
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    }
}
