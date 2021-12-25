using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Device.Gpio;
using UDP_Client;

namespace UDP_Client
{
    public partial class Form1 : Form
    {
        // Protocol bytes
        private const byte RequestRestartCode = 0x04;
        private const byte RequestReboot = 0x08;
        private const byte RequestNormal = 0x00;
        private const byte TagCommVersion = 0x01;
        private const byte Teleoperated = 0x00;
        private const byte Test = 0x01;
        private const byte Autonomous = 0x02;
        private const byte Enabled = 0x04;
        private const byte EmergencyStop = 0x80;
        private const byte TagCANInfo = 0x0e;
        private const byte TagCPUInfo = 0x05;
        private const byte TagRAMInfo = 0x06;
        private const byte TagDiskInfo = 0x04;
        private const byte RequestTime = 0x01;
        private const byte RobotHasCode = 0x20;
        private const byte TagDate = 0x0f;
        private const byte TagJoystick = 0x0c;
        private const byte TagTimezone = 0x10;
        private const byte Red1 = 0x00;
        private const byte Red2 = 0x01;
        private const byte Red3 = 0x02;
        private const byte Blue1 = 0x03;
        private const byte Blue2 = 0x04;
        private const byte Blue3 = 0x05;

        private const byte cRTagCANInfo = 0x0e;
        private const byte cRTagCPUInfo = 0x05;
        private const byte cRTagRAMInfo = 0x06;
        private const byte cRTagDiskInfo = 0x04;
        private const byte cRequestTime = 0x01;
        private const byte cRobotHasCode = 0x20;

        // Sent robot and FMS packet counters
        private bool send_time_data;
        private ushort SentRobotPackets;

        // Control code flags N: made public so that can be turned on from UI
        public bool reboot = false;
        public bool restart_code = false;

        private Config robot = Config.Instance;


        public static int pin_index = 0;
        public static int port = 1110;
        public static string ip = "";
        public static int count = 1;

        GpioController controller = new GpioController();

        public Form1()
        {
            InitializeComponent();
        }

        int[] flexdioPins = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        byte[] robot_packet;

        private void UDP_Loopback(BackgroundWorker bw, int sleepPeriod)
        {
            for (int i = 0; i < flexdioPins.Length; i++)
            {
                if (pin_index == flexdioPins[i])
                {
                    if (!controller.IsPinOpen(flexdioPins[i]))
                    {
                        controller.OpenPin(flexdioPins[i], PinMode.Input);
                    }
                }
                break;
            }

            while (!bw.CancellationPending)
            {
                if (controller.IsPinOpen(pin_index))
                {
                    string pin_value = controller.Read(pin_index).ToString();

                    if (pin_value == "Low")
                    {
                        robot.CFG_SetRobotEnabled(true);
                        robot.CFG_SetRobotCommunications(false);

                        robot_packet = CreateRobotPacket();
                        if (SendPackets(robot_packet, "Enabled"))
                        {
                            count++;
                            Thread.Sleep(sleepPeriod);
                        }
                        else
                        {
                            Thread.Sleep(sleepPeriod);
                        }
                    }
                    else if (pin_value == "High")
                    {
                        robot.CFG_SetRobotEnabled(false);
                        robot.CFG_SetRobotCommunications(false);

                        robot_packet = CreateRobotPacket();
                        if (SendPackets(robot_packet, "Disabled"))
                        {
                            count++;
                            Thread.Sleep(sleepPeriod);
                        }
                        else
                        {
                            Thread.Sleep(sleepPeriod);
                        }
                    }
                }
                else
                {
                    robot.CFG_SetControlMode(ControlMode.CONTROL_AUTONOMOUS);
                    robot.CFG_SetRobotCommunications(false);

                    robot_packet = CreateRobotPacket();
                    if (SendPackets(robot_packet, "Auto"))
                    {
                        count++;
                        Thread.Sleep(sleepPeriod);
                    }
                    else
                    {
                        Thread.Sleep(sleepPeriod);
                    }
                }
            }
        }
        

        private byte GetControlCode()
        {
            byte code = 0;

            /* Get current control mode (Test, Auto or Teleop) */
            switch (robot.Control)
            {
                case ControlMode.CONTROL_TEST:
                    code |= Test;
                    break;
                case ControlMode.CONTROL_AUTONOMOUS:
                    code |= Autonomous;
                    break;
                case ControlMode.CONTROL_TELEOPERATED:
                    code |= Teleoperated;
                    break;
                default:
                    break;
            }

            /* Let the robot know if it should e-stop right now */
            if (robot.emergency_stopped)
                code |= EmergencyStop;

            /* Append the robot enabled state */
            if (robot.robot_enabled)
                code |= Enabled;

            return code;
        }

        /**
         * Generates the request code sent to the robot, which may instruct it to:
         * - Operate normally
         * - Reboot the roboRIO
         * - Restart the robot code process
         */
        private byte GetRequestCode()
        {
            byte code = RequestNormal;

            /* Config has comms, check if we need to send additional flags */
            if (robot.Communications)
            {
                if (reboot)
                {
                    code = RequestReboot;
                    reboot = false;
                }

                else if (restart_code)
                {
                    code = RequestRestartCode;
                    restart_code = false;
                }

            }

            return code;
        }

        /**
         * Returns the team station code sent to the robot.
         * This value may be used by the robot program to use specialized autonomous
         * modes or adjust sensor input.
         */
        private byte GetStationCode()
        {
            if (robot.Color == Alliance.RED)
                switch (robot.Position)
                {
                    case RobotPosition.POSITION_1:
                        return Red1;
                    case RobotPosition.POSITION_2:
                        return Red2;
                    case RobotPosition.POSITION_3:
                        return Red3;
                    default:
                        return Red1;
                }
            else
                switch (robot.Position)
                {
                    case RobotPosition.POSITION_1:
                        return Blue1;
                    case RobotPosition.POSITION_2:
                        return Blue2;
                    case RobotPosition.POSITION_3:
                        return Blue3;
                    default:
                        return Blue1;
                }
        }
        private byte[] get_timezone_data()
        {
            byte[] data = new byte[14];

            /* Get current time */
            DateTime now = DateTime.UtcNow;
            // time_t rt = 0; // tim: realtime as an time_t
            int ms = now.Millisecond; // milliseconds

            // Get local timezone
            byte[] tz = Encoding.ASCII.GetBytes(TimeZoneInfo.Local.StandardName);

            /* Encode date/time in datagram */
            data[0] = 11; // N: 11 is the fixed length of the date section
            data[1] = TagDate;

            data[2] = (byte)(ms >> 24);
            data[3] = (byte)(ms >> 16);
            data[4] = (byte)(ms >> 8);

            data[5] = (byte)ms; // I don't think this will be accurate in c#, it was in cpp

            data[6] = (byte)now.Second;// seconds after the minute - [0, 60] including leap second
            data[7] = (byte)now.Minute;// minutes after the hour - [0, 59]
            data[8] = (byte)now.Hour;// hours since midnight - [0, 23]
            data[9] = (byte)now.Day;// day of the month - [1, 31]
            data[10] = (byte)(now.Month - 1);// months since January - [0, 11]
            data[11] = (byte)(now.Year - 1900);// years since 1900

            /* Add timezone length and tag */
            data[12] = (byte)(tz.Length + 1); // N: tz.L + 1 because length byte includes the tag
            data[13] = TagTimezone;

            /* Add timezone string */
            data = data.Concat(tz).ToArray();

            /* Return the obtained data */
            return data;
        }

        public byte[] CreateRobotPacket()
        {
            byte[] arr = new byte[6];
            byte[] tz;

            arr[0] = (byte)(SentRobotPackets >> 8);
            arr[1] = (byte)SentRobotPackets;
            arr[2] = TagCommVersion;
            arr[3] = GetControlCode();
            arr[4] = GetRequestCode();
            arr[5] = GetStationCode();
            if (send_time_data)
            {
                // Add timezone data (if robot wants it)
                tz = get_timezone_data();
                arr = arr.Concat(tz).ToArray();
            }

            SentRobotPackets++;
            return arr;
        }

        bool SendPackets(byte[] packet, string status)
        {
            byte[] packetdata = packet;
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                sock.SendTo(packetdata, ep);
                //To avoid cross-threading we use Method Invoker
                this.Invoke(new MethodInvoker(delegate
                {
                    richTextBox1.AppendText(String.Format("{0}: {1} Packet(s) have been successfully sent!\n", status, count));
                }));
                return true;
            }
            catch (Exception ex)
            {
                this.Invoke(new MethodInvoker(delegate
                {
                    richTextBox1.AppendText(String.Format("Error while sending packets\n"));
                }));
                return false;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            port = Convert.ToInt32(textBox1.Text);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            ip = textBox2.Text;
        }

        private void label3_Click(object sender, EventArgs e)
        {
            pin_index = Convert.ToInt32(textBox3.Text);
        }

        private void backgroundWorker1_DoWork_1(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            //Extract the argument
            int arg = (int)e.Argument;
            
            //Start sending packets
            UDP_Loopback(worker, arg);

            if (worker.CancellationPending)
            {
                e.Cancel = true;
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                // The user canceled the operation.
                MessageBox.Show("Operation was cancelled");
            }
            else if (e.Error != null)
            {
                // There was an error during the operation.
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
            else
            {
                // The operation completed normally.
                MessageBox.Show("Operation Completed");
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            this.backgroundWorker1.RunWorkerAsync(20); //Sends Packets at a rate of 50Hz or 20ms          
        }
        private void button2_Click(object sender, EventArgs e)
        {
            this.backgroundWorker1.CancelAsync();
        }
        
    }
}