﻿using NModbus;
using PrimaryInterOp.Cross3Krc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

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

            IModbusSlave slave = factory.CreateSlave(0xFF);

            network.AddSlave(slave);
            network.ListenAsync();
            //network.ListenAsync().GetAwaiter().GetResult();
            while (true)
            {
                for (int i = 0; i < var.Count; i++)
                {
                    vars actvar = var[i];
                    if (actvar.RW == "R")
                    {
                        ushort[] test = { 0x00, 0x00 };
                        uint test2 = UInt32.Parse(actvar.value);
                        test[0] = ((ushort)test2);
                        test[1] = (ushort)(test2 >> 16);
                        slave.DataStore.HoldingRegisters.WritePoints(UInt16.Parse(actvar.address), test);
                    }
                    if (actvar.RW == "W")
                    {
                        ushort size = 2;
                        if (actvar.type == "INT") size = 2;
                        ushort[] test = slave.DataStore.HoldingRegisters.ReadPoints(UInt16.Parse(actvar.address), size);
                        actvar.value = ((uint)test[0] | (uint)test[1] << 16).ToString();
                        var[i] = actvar;
                    }
                }
                Thread.Sleep(10);
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
                    for (int i = 0; i < var.Count; i++)
                    {
                        vars actvar = var[i];
                        if (actvar.RW == "R")
                        {
                            actvar.value = itfSyncvar.ShowVar(actvar.name);
                            if (actvar.value.Length < 1) actvar.value = "0";
                            var[i] = actvar;
                        }
                        if (actvar.RW == "W")
                        {
                            itfSyncvar.SetVar(actvar.name, actvar.value);
                        }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}