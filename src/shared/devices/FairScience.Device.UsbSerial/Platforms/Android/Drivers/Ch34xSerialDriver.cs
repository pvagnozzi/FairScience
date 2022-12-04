/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public class Ch34xSerialDriver : UsbSerialDriver
{
    private const string TAG = nameof(ProlificSerialDriver);

    public Ch34xSerialDriver(UsbDevice device) : base(device)
    {
        Ports.Add(new Ch340SerialPort(Device, 0, this));
    }

    public class Ch340SerialPort : CommonUsbSerialPort
    {
        private const int USB_TIMEOUT_MILLIS = 5000;
        private const int DEFAULT_BAUD_RATE = 9600;

        private const int SCL_DTR = 0x20;
        private const int SCL_RTS = 0x40;
        private const int LCR_ENABLE_RX = 0x80;
        private const int LCR_ENABLE_TX = 0x40;
        private const int LCR_STOP_BITS_2 = 0x04;
        private const int LCR_CS8 = 0x03;
        private const int LCR_CS7 = 0x02;
        private const int LCR_CS6 = 0x01;
        private const int LCR_CS5 = 0x00;

        private const int LCR_MARK_SPACE = 0x20;
        private const int LCR_PAR_EVEN = 0x10;
        private const int LCR_ENABLE_PAR = 0x08;

        private bool _dtr;
        private bool _rts;

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;

        //private static string TAG => Ch34xSerialDriver.TAG;

        public Ch340SerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber, driver)
        {
        }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
            {
                throw new IOException("Already opened.");
            }

            SetConnection(connection);
            var opened = false;
            try
            {
                for (var i = 0; i < mDevice.InterfaceCount; i++)
                {
                    var usbIface = mDevice.GetInterface(i);
                    if (Connection!.ClaimInterface(usbIface, true))
                    {
                        Log.Debug(TAG, "claimInterface " + i + " SUCCESS");
                    }
                    else
                    {
                        Log.Debug(TAG, "claimInterface " + i + " FAIL");
                    }
                }

                var dataIface = mDevice.GetInterface(mDevice.InterfaceCount - 1);
                for (var i = 0; i < dataIface.EndpointCount; i++)
                {
                    var ep = dataIface.GetEndpoint(i);
                    if (ep?.Type != (UsbAddressing)UsbSupport.UsbEndpointXferBulk)
                    {
                        continue;
                    }

                    if (ep.Direction == (UsbAddressing)UsbSupport.UsbDirIn)
                    {
                        lock (ReadBufferLock)
                        {
                            _readEndpoint = ep;
                        }
                    }
                    else
                    {
                        lock (WriteBufferLock)
                        {
                            _writeEndpoint = ep;
                        }
                    }
                }


                Initialize();
                SetBaudRate(DEFAULT_BAUD_RATE);

                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    try
                    {
                        Close();
                    }
                    catch (IOException)
                    {
                        // Ignore IOExceptions during close()
                    }
                }
            }
        }

        public override void Close()
        {
            if (Connection == null)
            {
                throw new IOException("Already closed");
            }

            // TODO: nothing sended on close, maybe needed?

            try
            {
                Connection.Close();
            }
            finally
            {
                SetConnection();
            }
        }

        public override int Read(byte[] dest, int timeoutMillis)
        {
            int numBytesRead;
            lock (ReadBufferLock)
            {
                var readAmt = Math.Min(dest.Length, ReadBuffer.Length);
                numBytesRead = Connection.BulkTransfer(_readEndpoint, ReadBuffer, readAmt,
                    timeoutMillis);
                if (numBytesRead < 0)
                {
                    // This sucks: we get -1 on timeout, not 0 as preferred.
                    // We *should* use UsbRequest, except it has a bug/api oversight
                    // where there is no way to determine the number of bytes read
                    // in response :\ -- http://b.android.com/28023
                    return 0;
                }
                Buffer.BlockCopy(ReadBuffer, 0, dest, 0, numBytesRead);
            }
            return numBytesRead;
        }

        public override int Write(byte[] src, int timeoutMillis)
        {
            var offset = 0;

            while (offset < src.Length)
            {
                int writeLength;
                int amtWritten;

                lock (WriteBufferLock)
                {
                    byte[] writeBuffer;

                    writeLength = Math.Min(src.Length - offset, WriteBuffer.Length);
                    if (offset == 0)
                    {
                        writeBuffer = src;
                    }
                    else
                    {
                        // bulkTransfer does not support offsets, make a copy.
                        Buffer.BlockCopy(src, offset, WriteBuffer, 0, writeLength);
                        writeBuffer = WriteBuffer;
                    }

                    amtWritten = Connection.BulkTransfer(_writeEndpoint, writeBuffer, writeLength,
                        timeoutMillis);
                }
                if (amtWritten <= 0)
                {
                    throw new IOException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                Log.Debug(TAG, $"Wrote amt={amtWritten} attempted={writeLength}");
                offset += amtWritten;
            }
            return offset;
        }

        private int ControlOut(int request, int value, int index)
        {
            const int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirOut;
            return Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request,
                value, index, null, 0, USB_TIMEOUT_MILLIS);
        }


        private int ControlIn(int request, int value, int index, byte[] buffer)
        {
            const int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirIn;
            return Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request,
                value, index, buffer, buffer.Length, USB_TIMEOUT_MILLIS);
        }

        private void CheckState(string msg, int request, int value, IReadOnlyList<int> expected)
        {
            var buffer = new byte[expected.Count];
            var ret = ControlIn(request, value, 0, buffer);

            if (ret < 0)
            {
                throw new IOException($"Failed send cmd [{msg}]");
            }

            if (ret != expected.Count)
            {
                throw new IOException($"Expected {expected.Count} bytes, but get {ret} [{msg}]");
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (expected[i] == -1)
                {
                    continue;
                }

                var current = buffer[i] & 0xff;
                if (expected[i] != current)
                {
                    throw new IOException($"Expected 0x{expected[i]:X} bytes, but get 0x{current:X} [ {msg} ]");
                }
            }
        }

        private void SetControlLines()
        {
            if (ControlOut(0xa4, ~((_dtr ? SCL_DTR : 0) | (_rts ? SCL_RTS : 0)), 0) < 0)
            {
                throw new IOException("Failed to set control lines");
            }
        }

        private void Initialize()
        {
            CheckState("init #1", 0x5f, 0, new int[] { -1 /* 0x27, 0x30 */, 0x00 });

            if (ControlOut(0xa1, 0, 0) < 0)
            {
                throw new IOException("init failed! #2");
            }

            SetBaudRate(DEFAULT_BAUD_RATE);

            CheckState("init #4", 0x95, 0x2518, new int[] { -1 /* 0x56, c3*/, 0x00 });

            if (ControlOut(0x9a, 0x2518, 0x0050) < 0)
            {
                throw new IOException("init failed! #5");
            }

            CheckState("init #6", 0x95, 0x0706, new int[] { -1 /*0xf?*/, -1 /*0xec,0xee*/});

            if (ControlOut(0xa1, 0x501f, 0xd90a) < 0)
            {
                throw new IOException("init failed! #7");
            }

            SetBaudRate(DEFAULT_BAUD_RATE);

            SetControlLines();

            CheckState("init #10", 0x95, 0x0706, new int[] { -1 /* 0x9f, 0xff*/, 0xee });
        }

        private void SetBaudRate(int baudRate)
        {
            var baud = new int[]
            {
                2400, 0xd901, 0x0038, 4800, 0x6402,
                0x001f, 9600, 0xb202, 0x0013, 19200, 0xd902, 0x000d, 38400,
                0x6403, 0x000a, 115200, 0xcc03, 0x0008
            };

            for (var i = 0; i < baud.Length / 3; i++)
            {
                if (baud[i * 3] != baudRate)
                {
                    continue;
                }

                var ret = ControlOut(0x9a, 0x1312, baud[i * 3 + 1]);
                if (ret < 0)
                {
                    throw new IOException("Error setting baud rate. #1");
                }
                ret = ControlOut(0x9a, 0x0f2c, baud[i * 3 + 2]);
                if (ret < 0)
                {
                    throw new IOException("Error setting baud rate. #1");
                }

                return;
            }


            throw new IOException("Baud rate " + baudRate + " currently not supported");
        }

        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);

            var lcr = LCR_ENABLE_RX | LCR_ENABLE_TX;

            lcr |= dataBits switch
            {
                DATABITS_5 => LCR_CS5,
                DATABITS_6 => LCR_CS6,
                DATABITS_7 => LCR_CS7,
                DATABITS_8 => LCR_CS8,
                _ => throw new Java.Lang.IllegalArgumentException("Invalid data bits: " + dataBits),
            };


            lcr |= (int)parity switch
            {
                PARITY_NONE => lcr,
                PARITY_ODD => LCR_ENABLE_PAR,
                PARITY_EVEN => LCR_ENABLE_PAR | LCR_PAR_EVEN,
                PARITY_MARK => LCR_ENABLE_PAR | LCR_MARK_SPACE,
                PARITY_SPACE => LCR_ENABLE_PAR | LCR_MARK_SPACE | LCR_PAR_EVEN,
                _ => throw new Java.Lang.IllegalArgumentException("Invalid parity: " + parity),
            };

            lcr |= (int)stopBits switch
            {
                STOPBITS_1 => lcr,
                STOPBITS_1_5 => throw new Java.Lang.UnsupportedOperationException("Unsupported stop bits: 1.5"),
                STOPBITS_2 => LCR_STOP_BITS_2,
                _ => throw new Java.Lang.IllegalArgumentException("Invalid stop bits: " + stopBits)
            };

            var ret = ControlOut(0x9a, 0x2518, lcr);
            if (ret < 0)
            {
                throw new IOException("Error setting control byte");
            }
        }

        public override bool GetCD() => false;

        public override bool GetCTS() => false;

        public override bool GetDSR() => false;

        public override bool GetDTR() => _dtr;

        public override void SetDTR(bool value)
        {
            _dtr = value;
            SetControlLines();
        }

        public override bool GetRI() => false;

        public override bool GetRTS() => _rts;

        public override void SetRTS(bool value)
        {
            _rts = value;
            SetControlLines();
        }

        public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers) => true;
    }

    public static Dictionary<int, int[]> GetSupportedDevices() =>
        new()
        {
            {
                UsbId.VENDOR_QINHENG, new int[]
                {
                    UsbId.QINHENG_HL340
                }
            }
        };
}
