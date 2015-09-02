using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Collections;

namespace TimeCal
{
    class FrameMonitor
    {


        AutoResetEvent eventFrame = new AutoResetEvent(false);

        Queue<SerialFrame> queueFrame = new Queue<SerialFrame>();

        public FrameMonitor()
        {
            this.Cmd = -1;
        }


        public int Cmd
        {
            get;
            set;
        }

        public SerialFrame GetFrame(int timeout)
        {

            SerialFrame sf = null;
            lock (((ICollection)queueFrame).SyncRoot)
            {
                if (queueFrame.Count > 0)
                {
                    sf = queueFrame.Dequeue();
                }
            }

            if (timeout == 0 || sf != null) return sf;

            if (timeout < 0)
            {
                eventFrame.WaitOne();
            }
            else
            {
                eventFrame.WaitOne(timeout, false);
            }

            return GetFrame(0);

        }

        public void PutFrame(SerialFrame sf)
        {

            if (sf == null) return;
            if (this.Cmd > 0 && this.Cmd != sf.Cmd) return;

            lock (((ICollection)queueFrame).SyncRoot)
            {

                if (queueFrame.Count > 100)
                {
                    queueFrame.Dequeue();
                }
                queueFrame.Enqueue(sf);
                eventFrame.Set();
            }
        }



        public void Reset()
        {
            lock (((ICollection)queueFrame).SyncRoot)
            {
                queueFrame.Clear();
                eventFrame.Reset();
            }
        }
    }
}
