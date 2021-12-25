using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;

namespace UDP_Client
{
    #region Enums (Default Public)
    enum ControlMode
    {
        CONTROL_TEST,
        CONTROL_AUTONOMOUS,
        CONTROL_TELEOPERATED
    }
    enum Alliance
    {
        RED,
        BLUE,
    }

    enum RobotPosition
    {
        POSITION_1,
        POSITION_2,
        POSITION_3,
    }

    enum RobotSocketType
    {
        SOCKET_UDP,
        SOCKET_TCP,
    }

    enum MatchType
    {
        NONE,
        PRACTICE,
        QUALIFICATIONS,
        ELIMINATIONS,
    }
    #endregion

    sealed class Config
    {
        #region ThreadSafe Initialization
        private static readonly object padlock = new object();

        // N: See Connect for details, thread safe singleton initialization

        private static Config instance = null;
        public static Config Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (padlock)
                    {
                        if (instance == null)
                        {
                            instance = new Config();
                        }
                    }
                }
                return instance;
            }
        }
        #endregion


        #region Variables
        static int team;
        public bool robot_code { get; private set; } = false;//CFG_SetRobotCode, CFG_GetRobotCode

        public bool robot_enabled { get; private set; } = false; // CFG_SetRobotEnabled, CFG_GetRobotEnabled
        public float can_utilization = -1; //CFG_SetCANUtilization, CFG_GetCANUtilization
        public float robot_voltage { get;  private set; } = -1;
        public bool emergency_stopped { get; private set; } = false;
        public bool radio_communications { get; private set; } = false;
        public bool Communications { get; private set; } = false;


        public ControlMode Control { get; set; } = ControlMode.CONTROL_TELEOPERATED;
        public Alliance Color { get; set; } = Alliance.RED;
        public RobotPosition Position { get; set; } = RobotPosition.POSITION_1;
        #endregion

        #region Robot Status
        public void CFG_SetRobotCommunications(bool communications)
        {
            if (Communications != communications)
            {
                Communications = communications;
            }
        }


        public int CFG_GetRobotTeam()
        {
            return team;
        }
        /**
         * Updates the available state of the robot code
         */
        public void CFG_SetRobotCode(bool code)
        {
            if (robot_code != code)
            {
                robot_code = code;
            }
        }

        /**
         * Updates the robot's \a enabled state
         */
        public void CFG_SetRobotEnabled(bool enabled)
        {
            if (robot_enabled != enabled)
            {
                robot_enabled = enabled;
            }
        }

        /**
         * Changes the control \a mode of the robot
         */
        public void CFG_SetControlMode(ControlMode mode)
        {
            if (Control != mode)
            {
                Control = mode;
            }
        }

        /// <summary>
        /// This returns the control mode as a string.
        /// I didn't want to do the switch statement in enable in Operator tab
        /// </summary>
        /// <returns>string</returns>
        public string CFG_GetControlMode()
        {
            switch (Control)
            {
                case ControlMode.CONTROL_TELEOPERATED:
                    return "Teleoperated";
                case ControlMode.CONTROL_AUTONOMOUS:
                    return "Autonomous";
                case ControlMode.CONTROL_TEST:
                    return "Test";
                default:
                    break;
            }
            return "";
        }

        /**
         * Updates the emergency \a stopped state of the robot.
         */
        public void CFG_SetEmergencyStopped(bool stopped)
        {
            if (emergency_stopped != stopped)
            {
                emergency_stopped = stopped;
            }
        }
        #endregion

        private float respect_range(float input, int min, int max)
        {
            if (input < min)
                return min;
            else if (input > max)
                return max;
            return input;
        }

        public void WatchdogExpired() 
        {
            // Resets all graphics because watchdog expired
            CFG_SetRobotCode(false);
            CFG_SetRobotEnabled(false);
            CFG_SetEmergencyStopped(false);
            CFG_SetRobotCommunications(false);
        }
    }
}