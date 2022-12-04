/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;
using Java.Lang;
using Math = System.Math;

/*
 * driver is implemented from various information scattered over FTDI documentation
 *
 * baud rate calculation https://www.ftdichip.com/Support/Documents/AppNotes/AN232B-05_BaudRates.pdf
 * control bits https://www.ftdichip.com/Firmware/Precompiled/UM_VinculumFirmware_V205.pdf
 * device type https://www.ftdichip.com/Support/Documents/AppNotes/AN_233_Java_D2XX_for_Android_API_User_Manual.pdf -> bvdDevice
 *
 */

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public class FtdiSerialDriver : UsbSerialDriver
{

    public FtdiSerialDriver(UsbDevice device)
        : base(device)
    {

        for (var port = 0; port < device.InterfaceCount; port++)
        {
            Ports.Add(new FtdiSerialPort(device, port, this));
        }
    }


    internal class FtdiSerialPort : CommonUsbSerialPort
    {
        private const int USB_WRITE_TIMEOUT_MILLIS = 5000;
        private const int READ_HEADER_LENGTH = 2; // contains MODEM_STATUS

        // https://developer.android.com/reference/android/hardware/usb/UsbConstants#USB_DIR_IN
        private const int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | 128; // UsbConstants.USB_DIR_OUT;
        private const int REQTYPE_DEVICE_TO_HOST = UsbConstants.UsbTypeVendor | 0;   // UsbConstants.USB_DIR_IN;

        private const int RESET_REQUEST = 0;
        private const int MODEM_CONTROL_REQUEST = 1;
        private const int SET_BAUD_RATE_REQUEST = 3;
        private const int SET_DATA_REQUEST = 4;
        private const int GET_MODEM_STATUS_REQUEST = 5;
        private const int SET_LATENCY_TIMER_REQUEST = 9;
        private const int GET_LATENCY_TIMER_REQUEST = 10;

        private const int MODEM_CONTROL_DTR_ENABLE = 0x0101;
        private const int MODEM_CONTROL_DTR_DISABLE = 0x0100;
        private const int MODEM_CONTROL_RTS_ENABLE = 0x0202;
        private const int MODEM_CONTROL_RTS_DISABLE = 0x0200;
        private const int MODEM_STATUS_CTS = 0x10;
        private const int MODEM_STATUS_DSR = 0x20;
        private const int MODEM_STATUS_RI = 0x40;
        private const int MODEM_STATUS_CD = 0x80;
        private const int RESET_ALL = 0;
        private const int RESET_PURGE_RX = 1;
        private const int RESET_PURGE_TX = 2;

        private bool _dtr;
        private bool _rts;

        private const string TAG = nameof(FtdiSerialDriver);


        public FtdiSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber, driver)
        {
        }


        private void Reset()
        {
            var result = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                RESET_ALL, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 0)
            {
                throw new IOException($"Reset failed: result={result}");
            }
        }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null) 
            {
                throw new IOException("Already open");
            }

            SetConnection(connection);

            var opened = false;
            try 
            {
                for (var i = 0; i < mDevice.InterfaceCount; i++)
                {
                    if (connection.ClaimInterface(mDevice.GetInterface(i), true))
                    {
                        Log.Debug(TAG, "claimInterface " + i + " SUCCESS");
                    }
                    else
                    {
                        throw new IOException("Error claiming interface " + i);
                    }
                }
                Reset();
                opened = true;
            } 
            finally 
            {
                if (!opened)
                {
                    Close();
                    SetConnection();
                }
            }
        }

        public override void Close()
        {
            if (Connection == null)
            {
                throw new IOException("Already closed");
            }
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
            var endpoint = mDevice.GetInterface(0).GetEndpoint(0);

            lock(ReadBufferLock) 
            {
                var readAmt = Math.Min(dest.Length, ReadBuffer.Length);

                // todo: replace with async call
                var totalBytesRead = Connection.BulkTransfer(endpoint, ReadBuffer,
                    readAmt, timeoutMillis);

                if (totalBytesRead < READ_HEADER_LENGTH)
                {
                    throw new IOException("Expected at least " + READ_HEADER_LENGTH + " bytes");
                }

                return ReadFilter(dest, totalBytesRead, endpoint?.MaxPacketSize ?? 0);
            }
        }

        private int ReadFilter(byte[] buffer, int totalBytesRead, int maxPacketSize)
        {
            var destPos = 0;

            for (var srcPos = 0; srcPos < totalBytesRead; srcPos += maxPacketSize)
            {
                var length = Math.Min(srcPos + maxPacketSize, totalBytesRead) - (srcPos + READ_HEADER_LENGTH);
                if (length < 0)
                    throw new IOException("Expected at least " + READ_HEADER_LENGTH + " bytes");

                Buffer.BlockCopy(ReadBuffer, srcPos + READ_HEADER_LENGTH, buffer, destPos, length);
                destPos += length;
            }
            return destPos;
        }

        public override int Write(byte[] src, int timeoutMillis)
        {
            var endpoint = mDevice.GetInterface(0).GetEndpoint(1);
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

                    amtWritten = Connection.BulkTransfer(endpoint, writeBuffer, writeLength,
                            timeoutMillis);
                }

                if (amtWritten <= 0)
                {
                    throw new IOException("Error writing " + writeLength
                            + " bytes at offset " + offset + " length=" + src.Length);
                }

                Log.Debug(TAG, "Wrote amtWritten=" + amtWritten + " attempted=" + writeLength);
                offset += amtWritten;
            }
            return offset;
        }


        private void SetBaudRate(int baudRate)
        {
            int divisor, subdivisor, effectiveBaudRate;

            switch (baudRate)
            {
                case > 3500000:
                    throw new UnsupportedOperationException("Baud rate to high");
                case >= 2500000:
                    divisor = 0;
                    subdivisor = 0;
                    effectiveBaudRate = 3000000;
                    break;
                case >= 1750000:
                    divisor = 1;
                    subdivisor = 0;
                    effectiveBaudRate = 2000000;
                    break;
                default:
                {
                    divisor = (24000000 << 1) / baudRate;
                    divisor = (divisor + 1) >> 1; // round
                    subdivisor = divisor & 0x07;
                    divisor >>= 3;
                    if (divisor > 0x3fff) // exceeds bit 13 at 183 baud
                        throw new UnsupportedOperationException("Baud rate to low");
                    effectiveBaudRate = (24000000 << 1) / ((divisor << 3) + subdivisor);
                    effectiveBaudRate = (effectiveBaudRate + 1) >> 1;
                    break;
                }
            }

            var baudRateError = Math.Abs(1.0 - (effectiveBaudRate / (double)baudRate));
            if (baudRateError >= 0.031) 
            {
                // can happen only > 1.5Mbaud
                throw new UnsupportedOperationException(
                    "Baud rate deviation %.1f%% is higher than allowed 3%%");
            }

            var value = divisor;
            var index = 0;
            switch (subdivisor)
            {
                case 0: break; // 16,15,14 = 000 - sub-integer divisor = 0
                case 4:
                    value |= 0x4000;
                    break; // 16,15,14 = 001 - sub-integer divisor = 0.5
                case 2:
                    value |= 0x8000;
                    break; // 16,15,14 = 010 - sub-integer divisor = 0.25
                case 1:
                    value |= 0xc000;
                    break; // 16,15,14 = 011 - sub-integer divisor = 0.125
                case 3:
                    value |= 0x0000;
                    index |= 1;
                    break; // 16,15,14 = 100 - sub-integer divisor = 0.375
                case 5:
                    value |= 0x4000;
                    index |= 1;
                    break; // 16,15,14 = 101 - sub-integer divisor = 0.625
                case 6:
                    value |= 0x8000;
                    index |= 1;
                    break; // 16,15,14 = 110 - sub-integer divisor = 0.75
                case 7:
                    value |= 0xc000;
                    index |= 1;
                    break; // 16,15,14 = 111 - sub-integer divisor = 0.875
            }

            var result = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SET_BAUD_RATE_REQUEST,
                value, index, null, 0, USB_WRITE_TIMEOUT_MILLIS);

            if (result != 0)
            {
                throw new IOException("Setting baudrate failed: result=" + result);
            }

            //return effectiveBaudRate;
        }


        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            if (baudRate <= 0)
            {
                throw new IllegalArgumentException("Invalid baud rate: " + baudRate);
            }

            SetBaudRate(baudRate);

            var config = dataBits;

            config |= dataBits switch
            {
                DATABITS_5 => throw new UnsupportedOperationException("Unsupported data bits: " + dataBits),
                DATABITS_6 => throw new UnsupportedOperationException("Unsupported data bits: " + dataBits),
                DATABITS_7 => dataBits,
                DATABITS_8 => dataBits,
                _ => throw new IllegalArgumentException("Invalid data bits: " + dataBits)
            };

            switch (parity)
            {
                case Parity.None:
                    break;
                case Parity.Odd:
                    config |= 0x100;
                    break;
                case Parity.Even:
                    config |= 0x200;
                    break;
                case Parity.Mark:
                    config |= 0x300;
                    break;
                case Parity.Space:
                    config |= 0x400;
                    break;
                case Parity.NotSet:
                    break;
                default:
                    throw new IllegalArgumentException("Unknown parity value: " + parity);
            }

            switch (stopBits)
            {
                case StopBits.One:
                    break;
                case StopBits.OnePointFive:
                    throw new UnsupportedOperationException("Unsupported stop bits: 1.5");
                case StopBits.Two:
                    config |= 0x1000;
                    break;
                case StopBits.NotSet:
                    break;
                default:
                    throw new IllegalArgumentException("Unknown stopBits value: " + stopBits);
            }

            var result = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SET_DATA_REQUEST,
                config, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);

            if (result != 0)
            {
                throw new IOException($"Setting parameters failed: result={result}");
            }

            //breakConfig = config;
        }

        private int GetStatus()
        {
            var data = new byte[2];
            var result = Connection.ControlTransfer((UsbAddressing)REQTYPE_DEVICE_TO_HOST, GET_MODEM_STATUS_REQUEST,
                    0, mPortNumber + 1, data, data.Length, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 2) 
            {
                throw new IOException($"Get modem status failed: result={result}");
            }
            return data[0];
        }

        public override bool GetCD() => (GetStatus() & MODEM_STATUS_CD) != 0;

        public override bool GetCTS() =>
            (GetStatus() & MODEM_STATUS_CTS) != 0;

        public override bool GetDSR() =>
            (GetStatus() & MODEM_STATUS_DSR) != 0;
        
        public override bool GetDTR() => _dtr;
        
        public override void SetDTR(bool value)
        {
            var result = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, MODEM_CONTROL_REQUEST,
                                value ? MODEM_CONTROL_DTR_ENABLE : MODEM_CONTROL_DTR_DISABLE, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 0)
            {
                throw new IOException($"Set DTR failed: result={result}");
            }
            _dtr = value;
        }

        public override bool GetRI() =>
            (GetStatus() & MODEM_STATUS_RI) != 0;

        public override bool GetRTS() =>
            _rts;
        

        public override void SetRTS(bool value)
        {
            var result = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, MODEM_CONTROL_REQUEST,
                    value ? MODEM_CONTROL_RTS_ENABLE : MODEM_CONTROL_RTS_DISABLE, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 0)
            {
                throw new IOException($"Set RTS failed: result={result}");
            }
            _rts = value;
        }

        public override bool PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            if (purgeWriteBuffers)
            {
                var resultTx = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                        RESET_PURGE_RX, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (resultTx != 0)
                {
                    throw new IOException($"Flushing RX failed: result={resultTx}");
                }
            }

            if (!purgeReadBuffers)
            {
                return true;
            }

            
            var resultRx = Connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                RESET_PURGE_RX, mPortNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (resultRx != 0)
            {
                throw new IOException($"Flushing RX failed: result={resultRx}");
            }

            return true;
        }
    }

    public static Dictionary<int, int[]> GetSupportedDevices() =>
        new()
        {
            {
                UsbId.VENDOR_FTDI, new int[]
                {
                    UsbId.FTDI_FT232R,
                    UsbId.FTDI_FT232H,
                    UsbId.FTDI_FT2232H,
                    UsbId.FTDI_FT4232H,
                    UsbId.FTDI_FT231X,  // same ID for FT230X, FT231X, FT234XD
                }
            }
        };
}

