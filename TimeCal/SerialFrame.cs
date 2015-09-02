using System;
using System.Collections.Generic;
using System.Text;

namespace TimeCal
{
    class SerialFrame
    {

        private byte[] frameData;


        public SerialFrame(byte[] buffer, int length)
        {
            if (length > 0)
            {
                frameData = new byte[length];
                Array.Copy(buffer, frameData, length);
            }
        }

        public byte Cmd
        {
            get
            {
                return frameData[5];
            }
        }


        public int DataLength
        {
            get
            {
                return BitConverter.ToInt16(frameData, 3);
            }
        }

        public byte[] RawData
        {
            get
            {
                return frameData;
            }
        }

        public string ToString()
        {

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < frameData.Length; i++)
            {
                sb.Append(frameData[i].ToString("X2") + " ");
            }
            sb.AppendLine();

            return sb.ToString();
        }



    }
}
