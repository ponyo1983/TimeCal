using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TimeCal
{
    class DeviceRequest
    {
        byte[] data;
        byte[] response;
        AutoResetEvent finishedEvent = new AutoResetEvent(false);//等到以后,自动复位。

        bool getResponse = false;

        public event EventHandler Callback;


        public DeviceRequest(byte cmd, byte[] data)
        {
            this.Cmd = cmd;
            this.data = (byte[])data.Clone();
        }

        public byte Cmd
        {
            get;
            private set;
        }

        public int Timeout
        {
            get;
            private set;
        }

        public byte[] Request
        {
            get
            {
                return data;
            }
        }

        public byte[] Response
        {
            get
            {
                return response;
            }
        }

        public bool WaitResponse(int millSecond)
        {

            if (getResponse || (millSecond == 0)) return getResponse;

            if (millSecond < 0) finishedEvent.WaitOne();
            else finishedEvent.WaitOne(millSecond, false);

            return WaitResponse(0);

        }
        public void AddResponse(byte[] response)
        {
            this.response = response;
            this.getResponse = true;
            finishedEvent.Set();

            if (Callback != null)
            {
                try
                {
                    Callback(this, null);
                }
                catch (Exception ex)
                {
                }
            }

        }
    }
}
