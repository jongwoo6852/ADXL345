// Copyright (c) Microsoft. All rights reserved.
// Distributed with a free-will license.
// Use it any way you want, profit or free, provided it fits in the licenses of its associated works.
// ADXL345
// This code is designed to work with the ADXL345_I2CS I2C Mini Module available from ControlEverything.com.
// https://www.controleverything.com/content/Accelorometer?sku=ADXL345_I2CS#tabs-0-product_tabset-2

using System;
using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace ADXL345
{
    struct Acceleration
    {
        public double X;
        public double Y;
        public double Z;
    };

    //	App that reads data over I2C from an attached ADXL345 accelerometer
    public sealed partial class MainPage : Page
    {
        private const byte ACCEL_I2C_ADDR = 0x53;           //	I2C address of the ADXL345
        private const byte ACCEL_REG_BANDW_RATE = 0x2C;     //	Bandwidth Rate register
        private const byte ACCEL_REG_POWER_CONTROL = 0x2D;  //	Power Control register
        private const byte ACCEL_REG_DATA_FORMAT = 0x31;    //	Data Format register
        private const byte ACCEL_REG_X = 0x32;              //	X Axis data register
        private const byte ACCEL_REG_Y = 0x34;              //	Y Axis data register
        private const byte ACCEL_REG_Z = 0x36;              //	Z Axis data register

        private I2cDevice I2CAccel;
        private Timer periodicTimer;

        public MainPage()
        {
            this.InitializeComponent();

            //	Register for the unloaded event so we can clean up upon exit
            Unloaded += MainPage_Unloaded;

            //	Initialize the I2C bus, accelerometer, and timer
            InitI2CAccel();
        }

        private async void InitI2CAccel()
        {
            string aqs = I2cDevice.GetDeviceSelector();             //	Get a selector string that will return all I2C controllers on the system
            var dis = await DeviceInformation.FindAllAsync(aqs);    //	Find the I2C bus controller device with our selector string
            if (dis.Count == 0)
            {
                Text_Status.Text = "No I2C controllers were found on the system";
                return;
            }

            var settings = new I2cConnectionSettings(ACCEL_I2C_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            I2CAccel = await I2cDevice.FromIdAsync(dis[0].Id, settings);    // Create an I2C Device with our selected bus controller and I2C settings
            if (I2CAccel == null)
            {
                Text_Status.Text = string.Format(
                    "Slave address {0} on I2C Controller {1} is currently in use by " +
                    "another application. Please ensure that no other applications are using I2C.",
                    settings.SlaveAddress,
                    dis[0].Id);
                return;
            }

            /*
		Initialize the accelerometer:
		For this device, we create 2-byte write buffers:
		The first byte is the register address we want to write to.
		The second byte is the contents that we want to write to the register.
	    */
            byte[] WriteBuf_BandWRate = new byte[] { ACCEL_REG_BANDW_RATE, 0x0A };          //	0x0A sets Normal Mode and Output Data Rate = 100 Hz
            byte[] WriteBuf_DataFormat = new byte[] { ACCEL_REG_DATA_FORMAT, 0x08 };        //	0x08 sets range to +- 2Gs
            byte[] WriteBuf_PowerControl = new byte[] { ACCEL_REG_POWER_CONTROL, 0x08 };    //	0x08 puts the accelerometer into measurement mode

            //	Write the register settings
            try
            {
                I2CAccel.Write(WriteBuf_BandWRate);
                I2CAccel.Write(WriteBuf_DataFormat);
                I2CAccel.Write(WriteBuf_PowerControl);
            }
            // If the write fails display the error and stop running
            catch (Exception ex)
            {
                Text_Status.Text = "Failed to communicate with device: " + ex.Message;
                return;
            }

            //	Create a timer to read data every 300ms
            periodicTimer = new Timer(this.TimerCallback, null, 0, 300);
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            //	Cleanup
            I2CAccel.Dispose();
        }

        private void TimerCallback(object state)
        {
            string xText, yText, zText;
            string addressText, statusText;

            //	Read and format accelerometer data
            try
            {
                Acceleration accel = ReadI2CAccel();
                addressText = "I2C Address of the Accelerometer ADXL345: 0x53";
                xText = String.Format("X Axis: {0:F0}", accel.X);
                yText = String.Format("Y Axis: {0:F0}", accel.Y);
                zText = String.Format("Z Axis: {0:F0}", accel.Z);
                statusText = "Status: Running";
            }
            catch (Exception ex)
            {
                xText = "X Axis: Error";
                yText = "Y Axis: Error";
                zText = "Z Axis: Error";
                statusText = "Failed to read from Accelerometer: " + ex.Message;
            }

            //	UI updates must be invoked on the UI thread
            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Text_X_Axis.Text = xText;
                Text_Y_Axis.Text = yText;
                Text_Z_Axis.Text = zText;
                Text_Status.Text = statusText;
            });
        }

        private Acceleration ReadI2CAccel()
        {
            byte[] RegAddrBuf = new byte[] { ACCEL_REG_X };     //	Read data from the register address
            byte[] ReadBuf = new byte[6];                       //	We read 6 bytes sequentially to get all 3 two-byte axes registers in one read

            /*
		Read from the accelerometer 
		We call WriteRead() so we first write the address of the X-Axis I2C register, then read all 3 axes
	    */
            I2CAccel.WriteRead(RegAddrBuf, ReadBuf);

            /*
		In order to get the raw 16-bit data values, we need to concatenate two 8-bit bytes from the I2C read for each axis.
		We accomplish this by using the BitConverter class.
	    */
            short AccelRawX = BitConverter.ToInt16(ReadBuf, 0);
            short AccelRawY = BitConverter.ToInt16(ReadBuf, 2);
            short AccelRawZ = BitConverter.ToInt16(ReadBuf, 4);

            Acceleration accel;
            accel.X = AccelRawX;
            accel.Y = AccelRawY;
            accel.Z = AccelRawZ;

            return accel;
        }
    }
}
