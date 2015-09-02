using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Collections;
using System.Threading;

namespace TimeCal
{
    class SampleModule
    {
        private SerialPort sp;
        List<FrameMonitor> listMonitor = new List<FrameMonitor>();

        AutoResetEvent eventRequest = new AutoResetEvent(false);
        Queue<DeviceRequest> listRequest = new Queue<DeviceRequest>();

        Thread threadRequest;
        Thread rxThread;
        public SampleModule(string portName)
        {
            this.sp = new SerialPort(portName);
        }


        public bool Open()
        {
            if (sp.IsOpen) return true;
            try
            {
                sp.BaudRate = 576000;
                sp.Open();
                    
                rxThread = new Thread(new ThreadStart(ReceiveData));

                rxThread.IsBackground = true;
                rxThread.Start();

                threadRequest = new Thread(new ThreadStart(ProcessRequest));
                threadRequest.IsBackground = true;
                threadRequest.Start();

                return true;
            }
            catch (Exception)
            { 
            }

            return false;
           
        }

        public void Close()
        {
            if (sp.IsOpen)
            {
                threadRequest.Abort();
                rxThread.Abort();
                Thread.Sleep(100);
                sp.Close();
            }
        }

        public FrameMonitor AddMonitor()
        {
            FrameMonitor monitor = new FrameMonitor();

            lock (((ICollection)listMonitor).SyncRoot)
            {
                listMonitor.Add(monitor);
            }

            return monitor;
        }

        public void DeleteMonitor(FrameMonitor monitor)
        {
            lock (((ICollection)listMonitor).SyncRoot)
            {
                listMonitor.Remove(monitor);
            }
        }


        public void SendRequest(DeviceRequest request)
        {

            lock (((ICollection)listRequest))
            {
                listRequest.Enqueue(request);
                eventRequest.Set();
            }
        }

        private void SendSerialData(byte cmd, byte[] data)
        {
            List<byte> cmdList = new List<byte>();

            cmdList.Add(0xaa);
            cmdList.Add(0xaa);

            cmdList.Add(0x01);

            UInt16 length = (UInt16)(1 + data.Length);

            cmdList.Add((byte)(length & 0xff));

            cmdList.Add((byte)((length >> 8) & 0xff));

            Int16 sumcheck = cmd;

            cmdList.Add(cmd);


            for (int i = 0; i < data.Length; i++)
            {
                cmdList.Add(data[i]);

                sumcheck += data[i];
            }

            cmdList.Add((byte)(sumcheck & 0xff));

            cmdList.Add((byte)((sumcheck >> 8) & 0xff));

            cmdList.Add((byte)'\r');
            cmdList.Add((byte)'\n');


            byte[] rawData = cmdList.ToArray();

            try
            {
                sp.Write(rawData, 0, rawData.Length);
            }
            catch (Exception)
            { }



        }


        /// <summary>
        /// 处理设备的请求命令
        /// </summary>
        private void ProcessRequest()
        {

            try
            {
                FrameMonitor monitor = AddMonitor();
                while (Thread.CurrentThread.ThreadState==ThreadState.Background)
                {
                    DeviceRequest[] requests = null;
                    lock (((ICollection)listRequest).SyncRoot)
                    {
                        requests = listRequest.ToArray();

                        listRequest.Clear();
                    }


                    if (requests == null || requests.Length <= 0)
                    {

                        eventRequest.WaitOne();
                        continue;
                    }

                    for (int i = 0; i < requests.Length; i++)
                    {

                        monitor.Cmd = requests[i].Cmd - 0x80;

                        SendSerialData(requests[i].Cmd, requests[i].Request);

                        monitor.Reset();


                        SerialFrame sf = monitor.GetFrame(500);
                        if (sf != null)
                        {
                            requests[i].AddResponse(sf.RawData);
                        }

                    }
                }
            }
            catch (Exception)
            {

            }


        }



        private void ReceiveData()
        {
            byte[] buffer = new byte[128 * 1024 * 2];

            byte[] frameBuffer = new byte[128 * 1024];

            int frameLength = 0;

            int leftLength = 0;

            int dataLength = 0;

            int realLength = 0;

            bool rollback = false;

            try
            {
                sp.ReadTimeout = 500;

                while (true)
                {

                    dataLength = 0;

                    if (rollback) //前一个数据帧回滚
                    {
                        for (int i = 1; i < frameLength; i++)
                        {
                            if (frameBuffer[i] == 0xaa && ((i == frameLength - 1) || (frameBuffer[i + 1] == 0xaa)))
                            {
                                dataLength = frameLength - i;
                                Array.Copy(frameBuffer, i, buffer, 0, dataLength);
                                break;
                            }
                        }
                        frameLength = 0;
                        rollback = false;
                    }

                    if (leftLength > 0) //前一个BUffer剩下的Data
                    {
                        Array.Copy(buffer, buffer.Length / 2, buffer, dataLength, leftLength);
                        dataLength += leftLength;
                        leftLength = 0;
                    }

                    if (dataLength <= 0) //串口数据
                    {
                        try
                        {
                            dataLength = sp.Read(buffer, 0, buffer.Length / 2);
                        }
                        catch (TimeoutException)
                        {
                            rollback = true;
                            continue;
                        }

                    }

                    for (int i = 0; i < dataLength; i++)
                    {
                        frameBuffer[frameLength++] = buffer[i];
                        leftLength = dataLength - i - 1;
                        switch (frameLength)
                        {
                            case 1:
                            case 2:
                                if (frameBuffer[frameLength - 1] != 0xaa) //帧头
                                {
                                    rollback = true;
                                }
                                break;
                            case 3:
                                if (frameBuffer[2] != 1) //版本协议
                                {
                                    rollback = true;
                                }
                                break;
                            case 4:
                                break;
                            case 5:
                                {
                                    realLength = frameBuffer[3] + (frameBuffer[4] << 8);//数据长度
                                    if (realLength + 9 > frameBuffer.Length || (realLength <= 0))
                                    {
                                        rollback = true;
                                    }
                                }
                                break;
                            default:
                                if (frameLength == realLength + 9) //收到完整数据
                                {
                                    
                                    UInt16 calSum = CalSUMCheck(frameBuffer, 5, realLength);

                                    UInt16 realSum = (UInt16)(frameBuffer[realLength + 5] + (frameBuffer[realLength + 6] << 8));
                                    if (calSum != realSum)
                                    {
                                        rollback = true;
                                    }
                                    else
                                    {
                                        SerialFrame sf = new SerialFrame(frameBuffer, frameLength);
                                        lock (((ICollection)listMonitor).SyncRoot)
                                        {
                                            foreach (FrameMonitor monitor in listMonitor)
                                            {
                                                monitor.PutFrame(sf);
                                            }
                                        }

                                        frameLength = 0;

                                    }
                                }
                                break;
                        }

                        if (rollback || frameLength >= frameBuffer.Length)
                        {
                            Array.Copy(buffer, i + 1, buffer, buffer.Length / 2, leftLength);
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
        }


        public UInt16 CalSUMCheck(byte[] data, int index, int cnt)
        {

            int sum = 0;
            for (int i = index; i < index + cnt; i++)
            {

                sum += data[i];
            }

            return (UInt16)(sum & 0xffff);

        }


        public DeviceRequest SetTime()
        {
            DateTime dt = DateTime.Now;
            byte year = Convert.ToByte(dt.Year - 2000);
            byte month = Convert.ToByte(dt.Month);
            byte day = Convert.ToByte(dt.Day);
            byte hour = Convert.ToByte(dt.Hour);
            byte minute = Convert.ToByte(dt.Minute);
            byte seconds = Convert.ToByte(dt.Second);
            List<byte> bytes = new List<byte>();
            bytes.Add(year);
            bytes.Add(month);
            bytes.Add(day);
            bytes.Add(hour);
            bytes.Add(minute);
            bytes.Add(seconds);
            byte[] timeData = bytes.ToArray();
            DeviceRequest req = new DeviceRequest(0x82, timeData);
            return req;
        }

        public DeviceRequest GetTimeNow()
        {
            DeviceRequest req = new DeviceRequest(0x92, new byte[] { 0 });
            return req;
        }


    }
}
