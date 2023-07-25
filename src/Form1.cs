using NModbus;
using PrimaryInterOp.Cross3Krc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace ModbusKUKA
{
    public partial class Form1 : Form
    {
        public struct vars
        {
            public string name;
            public string type;
            public string address;
            public string RW;
            public string value;
            public string state;
        }

        public struct settings
        {
            public string robot_ip;
            public string address;
            public string fall_keep;
            public string endian;
        }

        private settings set = new settings();
        private List<vars> var = new List<vars>();
        DateTime dt1, dt2;
        TimeSpan ts;

        public Form1()
        {
            InitializeComponent();

            try
            {
                string[] lines = File.ReadAllLines("config.csv", Encoding.UTF8);

                string[] config = lines[1].Split(',');

                set.robot_ip = config[0];
                set.address = config[1];
                set.fall_keep = config[2];
                set.endian = config[3];
                string[] temp = { };
                vars tempvar = new vars();

                for (int i = 0; i < (lines.Length - 4); i++)
                {
                    temp = lines[i + 4].Split(',');
                    tempvar.name = temp[0];
                    tempvar.type = temp[1];
                    tempvar.address = temp[2];
                    tempvar.RW = temp[3];
                    tempvar.value = "0";
                    var.Add(tempvar);
                }

                //Modbus thread
                Thread thread = new Thread(new ThreadStart(StartModbusTcpSlave));
                thread.IsBackground = true;
                thread.Start();

                //Data thread
                Thread thread2 = new Thread(new ThreadStart(StartCrossCommunication));
                thread2.IsBackground = true;
                thread2.Start();

                //Text thread
                Thread thread3 = new Thread(new ThreadStart(StartTextUpdate));
                thread3.IsBackground = true;
                thread3.Start();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        /// <summary>
        ///     Simple Modbus TCP slave.
        /// </summary>
        public void StartModbusTcpSlave()
        {
            IPAddress address = new IPAddress(new byte[] { 0, 0, 0, 0 });
            string[] tempaddress = set.robot_ip.Split('.');
            if (tempaddress.Length == 4)
            {
                address = new IPAddress(new byte[] {byte.Parse(tempaddress[0]),byte.Parse(tempaddress[1]),byte.Parse(tempaddress[2]),byte.Parse(tempaddress[3]) });
            }

            // create and start the TCP slave
            TcpListener slaveTcpListener = new TcpListener(address, 502);
            slaveTcpListener.Start();

            IModbusFactory factory = new ModbusFactory();

            IModbusSlaveNetwork network = factory.CreateSlaveNetwork(slaveTcpListener);

            IModbusSlave slave = factory.CreateSlave(byte.Parse(set.address));

            network.AddSlave(slave);
            network.ListenAsync();
            //network.ListenAsync().GetAwaiter().GetResult();
            while (true)
            {
                for (int i = 0; i < var.Count; i++)
                {
                    vars actvar = var[i];
                    int arraysize = 1;
                    Match match = Regex.Match(actvar.type, @"\[(\d+)\]");
                    if (match.Success) arraysize = int.Parse(match.Groups[1].Value);
                    ushort[] buffer = new ushort[arraysize*2];
                    int buf;

                    if (actvar.RW == "R")
                    {
                        if (match.Success)
                        {
                            //it is an array
                            string[] ele = actvar.value.Split(' ');
                            if (ele.Length > 1)
                            {
                                int length = ele.Length;
                                if (arraysize < ele.Length) length = arraysize;
                                for (int j = 0; j < length; j++)
                                {
                                    if (ele[j].Length > 0)
                                    {
                                        if (actvar.type.Contains("REAL"))
                                        {
                                            buf = (int)(float.Parse(ele[j]) * 100);
                                        }
                                        else if (actvar.type.Contains("INT"))
                                        {
                                            buf = Int32.Parse(ele[j]);
                                        }
                                        else
                                        {
                                            buf = 0;
                                        }

                                        if (set.endian == "0")
                                        {
                                            buffer[j * 2] = ((ushort)buf);
                                            buffer[j * 2 + 1] = (ushort)(buf >> 16);
                                        }
                                        else
                                        {
                                            buffer[j * 2] = (ushort)(buf >> 16);
                                            buffer[j * 2 + 1] = ((ushort)buf);
                                        }
                                    }
                                    else
                                    {
                                        buffer[j * 2] = 0;
                                        buffer[j * 2 + 1] = 0;
                                    }
                                }
                            }

                        }
                        else
                        {
                            if (actvar.type == "REAL")
                            {
                                buf = (int)(float.Parse(actvar.value) * 100);
                            }
                            else if (actvar.type == "INT")
                            {
                                buf = Int32.Parse(actvar.value);
                            }
                            else
                            {
                                buf = 0;
                            }

                            if (set.endian == "0")
                            {
                                buffer[0] = ((ushort)buf);
                                buffer[1] = (ushort)(buf >> 16);
                            }
                            else
                            {
                                buffer[0] = (ushort)(buf >> 16);
                                buffer[1] = ((ushort)buf);
                            }
                        }
                        slave.DataStore.HoldingRegisters.WritePoints(UInt16.Parse(actvar.address), buffer);
                    }
                    if (actvar.RW == "W")
                    {
                        /*
                        if (match.Success)
                        {
                            //it is an array
                            buffer = slave.DataStore.HoldingRegisters.ReadPoints(UInt16.Parse(actvar.address), (ushort)(arraysize * 2));
                            string tempstr = "";
                            actvar.state = "BUSY";
                            actvar.value = "";
                            for (int j = 0; j < arraysize; j++)
                            {
                                if (set.endian == "0")
                                {
                                    tempstr = ((int)buffer[j] | (int)buffer[j + 1] << 16).ToString();
                                }
                                else
                                {
                                    tempstr = ((int)buffer[j+1] | (int)buffer[j] << 16).ToString();
                                }
                                actvar.value = actvar.value + tempstr;
                                if (j < arraysize - 1) actvar.value = actvar.value + " ";
                            }
                            actvar.state = "OK";
                            var[i] = actvar;
                        }*/
                        
                        ushort size = 2;
                        if (actvar.type == "INT") size = 2;
                        ushort[] test = slave.DataStore.HoldingRegisters.ReadPoints(UInt16.Parse(actvar.address), size);
                        if (set.endian == "0")
                        {
                            if (actvar.type == "INT") {
                                actvar.value = ((int)test[0] | (int)test[1] << 16).ToString();
                            }
                            else if (actvar.type == "REAL")
                            {
                                actvar.value = ((float)((int)test[0] | (int)test[1] << 16) / 100.0).ToString();
                            }

                        }
                        else
                        {
                            if (actvar.type == "INT") { 
                                actvar.value = ((int)test[1] | (int)test[0] << 16).ToString();
                            }
                            else if (actvar.type == "REAL")
                            {
                                actvar.value = ((float)((int)test[1] | (int)test[0] << 16) / 100.0).ToString();
                            }
                        }
                        var[i] = actvar;
                                
                    }
                }
                Thread.Sleep(10);
            }
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
                return;
            }
        }

        private void mainNotifyIcon_MouseDoubleClick(object sender, EventArgs e)
        {
            if (this.Visible)
            {
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
            }
            else
            {
                this.Visible = true;
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }

        private void eXITToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("QUIT?", "TIP", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            {
                this.notifyIcon1.Visible = false;
                this.Close();
                this.Dispose();
                System.Environment.Exit(System.Environment.ExitCode);
            }
        }

        /// <summary>
        ///     SharedMemory Communication with KUKA CROSS3.
        /// </summary>
        public void StartCrossCommunication()
        {
            try
            {
                var objServiceFactory = new KrcServiceFactoryClass();
                var itfSyncvar = (ICKSyncVar)objServiceFactory.GetService("WBC_KrcLib.SyncVar", "ModbusKUKA");

                while (true)
                {
                    dt1 = DateTime.Now;
                    for (int i = 0; i < var.Count; i++)
                    {
                        vars actvar = var[i];
                        if (actvar.RW == "R")
                        {
                            actvar.value = itfSyncvar.ShowVar(actvar.name);
                            if (actvar.value.Length < 1) actvar.value = set.fall_keep;
                            var[i] = actvar;
                        }
                        if (actvar.RW == "W")
                        {
                            itfSyncvar.SetVar(actvar.name, actvar.value);
                        }
                    }
                    Thread.Sleep(10);
                    dt2 = DateTime.Now;
                    ts = dt2.Subtract(dt1);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        /// <summary>
        ///     Textbox update
        /// </summary>
        public void StartTextUpdate()
        {
            try
            {
                string temp = "";
                Thread.Sleep(2000);
                while (true)
                {
                    textBox1.Invoke((Action)delegate{
                        textBox1.Clear();
                        
                        textBox1.AppendText("\r\n");
                        textBox1.AppendText("\r\n");
                        textBox1.AppendText(" Modbus KUKA Service Started \r\n");
                        temp = " IP: " + set.robot_ip + "\r\n";
                        textBox1.AppendText(temp);
                        temp = " Unit-ID: " + set.address + "\r\n";
                        textBox1.AppendText(temp);
                        temp = " fall_keep: " + set.fall_keep + "\r\n";
                        textBox1.AppendText(temp);
                        temp = " endian: " + set.endian + "\r\n";
                        textBox1.AppendText(temp);
                        temp = " data number: " + var.Count.ToString() + "\r\n";
                        textBox1.AppendText(temp);

                        textBox1.AppendText(" ------------------\r\n");

                        temp = " Cross3ProcessTime: " + ((int)ts.TotalMilliseconds).ToString() + "MS \r\n";
                        textBox1.AppendText(temp);



                    });
                    Thread.Sleep(200);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

    }
}