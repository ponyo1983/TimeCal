using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO.Ports;

namespace TimeCal
{
    public partial class FormMain : Form
    {

        SampleModule module = null;

        Thread threadSearch;


        public FormMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            button2.Enabled = false;
            button3.Enabled = false;
            if (threadSearch == null || (threadSearch.IsAlive == false))
            {
                threadSearch = new Thread(new ThreadStart(SearchModule));
                threadSearch.IsBackground = true;
                threadSearch.Start();
            }
        }

        private void SearchModule()
        {

            if (module != null)
            {
                module.Close();
                module = null;
            }
            string[] ports = SerialPort.GetPortNames();


            bool findDev = false;
            for (int i = 0; i < ports.Length; i++)
            {
                SampleModule sm = new SampleModule(ports[i]);

                if (sm.Open())
                {

                    Thread.Sleep(100);
                    DeviceRequest dr = sm.GetTimeNow();
                    sm.SendRequest(dr);

                    if (dr.WaitResponse(1000))
                    {
                        module = sm;
                        this.Invoke((EventHandler)delegate {

                            button2.Enabled = true;
                            button3.Enabled = true;

                            findDev = true;

                        });
                        break;
                    }
                    else
                    {
                        sm.Close();
                    }

                }

            }

            if (findDev == false)
            {
                this.Invoke((EventHandler)delegate
                {
                    MessageBox.Show("请确保设备连接到电脑并处于工作状态!", "查找设备", MessageBoxButtons.OK, MessageBoxIcon.Information);

                });
            }


        }

        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            if (module == null) return;
            DeviceRequest req = module.SetTime();

            module.SendRequest(req);
            if (req.WaitResponse(1000))
            {

                byte[] response = req.Response;
                if (response[6] == 0)
                {
                    MessageBox.Show("写入成功！");
                }
                else if (response[6] == 0xFF)
                {
                    MessageBox.Show("写入失败！");
                }
                else
                {
                    MessageBox.Show("返回信息有误！");
                }
            }
            else
            {
                MessageBox.Show("设备无响应！");
            }
        }


        /// <summary>
        /// 获取时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {

            if (module == null) return;
            DeviceRequest req = module.GetTimeNow();
            module.SendRequest(req);
            if (req.WaitResponse(1000))
            {
                byte[] response = req.Response;
                if (response[6] == 0)
                {
                    string time = (2000 + response[7]) + "-" + response[8] + "-" + response[9] + " " + response[10] + ":" + response[11] + ":" + response[12];
                    MessageBox.Show(time);
                }
                else if (response[6] == 1)
                {
                    string time = (2000 + response[7]) + "-" + response[8] + "-" + response[9] + " " + response[10] + ":" + response[11] + ":" + response[12];
                    MessageBox.Show(time);
                }
                else if (response[6] == 0xFF)
                {
                    MessageBox.Show("读取失败！");
                }
                else
                {
                    MessageBox.Show("返回信息有误！");
                }
            }
            else
            {
                MessageBox.Show("设备无响应！");
            }

        }
    }
}
