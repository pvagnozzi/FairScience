/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using FairScience.Device.UsbSerial.Platforms.Android.Extensions;
using Java.Lang;
using Java.Nio;
using IOException = Java.IO.IOException;
using Math = System.Math;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public class CdcAcmSerialDriver : UsbSerialDriver
{
    private const string TAG = nameof(CdcAcmSerialDriver);

    public CdcAcmSerialDriver(UsbDevice device, bool? enableAsyncReads = null)
        : base(device)
    {
        Ports.Add(new CdcAcmSerialPort(device, 0, this, enableAsyncReads));
    }


    private class CdcAcmSerialPort : CommonUsbSerialPort
    {
        private readonly bool _enableAsyncReads;
        private UsbInterface _controlInterface;
        private UsbInterface _dataInterface;

        private UsbEndpoint _controlEndpoint;
        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;

        private bool _rts;
        private bool _dtr;

        private const int USB_RECIP_INTERFACE = 0x01;
        private const int USB_RT_ACM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

        private const int SET_LINE_CODING = 0x20; // USB CDC 1.1 section 6.2
        private const int GET_LINE_CODING = 0x21;
        private const int SET_CONTROL_LINE_STATE = 0x22;
        private const int SEND_BREAK = 0x23;


        // ReSharper disable once TooManyDependencies
        public CdcAcmSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver,
            bool? enableAsyncReads = null) :
            base(device, portNumber, driver)
        {
            if (enableAsyncReads is not null)
            {
                _enableAsyncReads = (bool)enableAsyncReads;
            }
            else
            {
                _enableAsyncReads = Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1;
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
                if (1 == mDevice.InterfaceCount)
                {
                    Log.Debug(TAG, "device might be castrated ACM device, trying single interface logic");
                    OpenSingleInterface();
                }
                else
                {
                    Log.Debug(TAG, "trying default interface logic");
                    OpenInterface();
                }

                Log.Debug(TAG, _enableAsyncReads ? "Async reads enabled" : "Async reads disabled.");

                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    SetConnection();
                    // just to be on the save side
                    _controlEndpoint = null;
                    _readEndpoint = null;
                    _writeEndpoint = null;
                }
            }
        }

        private void OpenSingleInterface()
        {
            // the following code is inspired by the cdc-acm driver
            // in the linux kernel

            _controlInterface = mDevice.GetInterface(0);
            Log.Debug(TAG, "Control iface=" + _controlInterface);

            _dataInterface = mDevice.GetInterface(0);
            Log.Debug(TAG, "data iface=" + _dataInterface);

            if (!Connection.ClaimInterface(_controlInterface, true))
            {
                throw new IOException("Could not claim shared control/data interface.");
            }

            var endCount = _controlInterface.EndpointCount;

            if (endCount < 3)
            {
                Log.Debug(TAG, "not enough endpoints - need 3. count=" + endCount);
                throw new IOException("Insufficient number of endpoints(" + endCount + ")");
            }

            // Analyse endpoints for their properties
            _controlEndpoint = null;
            _readEndpoint = null;
            _writeEndpoint = null;
            for (var i = 0; i < endCount; ++i)
            {
                var ep = _controlInterface.GetEndpoint(i);
                switch (ep?.Direction)
                {
                    case UsbAddressing.In when
                        (ep.Type == UsbAddressing.XferInterrupt):
                        Log.Debug(TAG, "Found controlling endpoint");
                        _controlEndpoint = ep;
                        break;
                    case UsbAddressing.In when
                        (ep.Type == UsbAddressing.XferBulk):
                        Log.Debug(TAG, "Found reading endpoint");
                        _readEndpoint = ep;
                        break;
                    case UsbAddressing.Out when
                        (ep.Type == UsbAddressing.XferBulk):
                        Log.Debug(TAG, "Found writing endpoint");
                        _writeEndpoint = ep;
                        break;
                    case UsbAddressing.NumberMask:
                        break;
                    case UsbAddressing.XferBulk:
                        break;
                    case UsbAddressing.XferInterrupt:
                        break;
                    case UsbAddressing.XferIsochronous:
                        break;
                    case null:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                if (_controlEndpoint == null ||
                    _readEndpoint == null ||
                    _writeEndpoint == null)
                {
                    continue;
                }

                Log.Debug(TAG, "Found all required endpoints");
                break;
            }

            if (_controlEndpoint != null &&
                _readEndpoint != null &&
                _writeEndpoint != null)
            {
                return;
            }

            Log.Debug(TAG, "Could not establish all endpoints");
            throw new IOException("Could not establish all endpoints");
        }

        private void OpenInterface()
        {
            Log.Debug(TAG, "claiming interfaces, count=" + mDevice.InterfaceCount);

            _controlInterface = mDevice.GetInterface(0);
            Log.Debug(TAG, "Control iface=" + _controlInterface);
            // class should be USB_CLASS_COMM

            if (!Connection.ClaimInterface(_controlInterface, true))
            {
                throw new IOException("Could not claim control interface.");
            }

            _controlEndpoint = _controlInterface.GetEndpoint(0);
            Log.Debug(TAG, "Control endpoint direction: " + _controlEndpoint.Direction);

            Log.Debug(TAG, "Claiming data interface.");
            _dataInterface = mDevice.GetInterface(1);
            Log.Debug(TAG, "data iface=" + _dataInterface);
            // class should be USB_CLASS_CDC_DATA

            if (!Connection.ClaimInterface(_dataInterface, true))
            {
                throw new IOException("Could not claim data interface.");
            }

            _readEndpoint = _dataInterface.GetEndpoint(1);
            Log.Debug(TAG, "Read endpoint direction: " + _readEndpoint?.Direction);
            _writeEndpoint = _dataInterface.GetEndpoint(0);
            Log.Debug(TAG, "Write endpoint direction: " + _writeEndpoint?.Direction);
        }

        private void SendAcmControlMessage(int request, int value, byte[] buf) =>
            Connection.ControlTransfer((UsbAddressing)0x21,
                request, value, 0, buf, buf?.Length ?? 0, 5000);

        public override void Close()
        {
            if (Connection == null)
            {
                throw new IOException("Already closed");
            }

            Connection.Close();
            SetConnection();
        }

        public override int Read(byte[] dest, int timeoutMillis)
        {
            var request = new UsbRequest();
            if (_enableAsyncReads)
            {
                ByteBuffer buf = ByteBuffer.Wrap(dest);
                try
                {
                    request.Initialize(Connection, _readEndpoint);

                    // CJM: Xamarin bug:  ByteBuffer.Wrap is supposed to be a two way update
                    // Changes made to one buffer should reflect in the other.  It's not working
                    // As a work around, I added a new method as an extension that uses JNI to turn
                    // a new byte[] array.  I then used BlockCopy to copy the bytes back the original array
                    // see https://forums.xamarin.com/discussion/comment/238396/#Comment_238396
                    //
                    // Old work around:
                    // as a work around, we populate dest with a call to buf.Get()
                    // see https://bugzilla.xamarin.com/show_bug.cgi?id=20772
                    // and https://bugzilla.xamarin.com/show_bug.cgi?id=31260

                    if (!request.Queue(buf, dest.Length))
                    {
                        throw new IOException("Error queueing request.");
                    }

                    var response = Connection.RequestWait();
                    if (response == null)
                    {
                        throw new IOException("Null response");
                    }

                    var nread = buf.Position();
                    if (nread > 0)
                    {
                        // CJM: This differs from the Java implementation.  The dest buffer was
                        // not getting the data back.

                        // 1st work around, no longer used
                        //buf.Rewind();
                        //buf.Get(dest, 0, dest.Length);

                        System.Buffer.BlockCopy(buf.ToByteArray(), 0, dest, 0, dest.Length);

                        //Log.Debug(TAG, HexDump.DumpHexString(dest, 0, Math.Min(32, dest.Length)));
                        return nread;
                    }
                    else
                    {
                        return 0;
                    }
                }
                finally
                {
                    request.Close();
                }
            }

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
                    if (timeoutMillis == Integer.MaxValue)
                    {
                        // Hack: Special case "~infinite timeout" as an error.
                        return -1;
                    }

                    return 0;
                }

                System.Buffer.BlockCopy(ReadBuffer, 0, dest, 0, numBytesRead);
            }

            return numBytesRead;
        }

        public override int Write(byte[] src, int timeoutMillis)
        {
            // TODO(mikey): Nearly identical to FtdiSerial write. Refactor.
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
                        System.Buffer.BlockCopy(src, offset, WriteBuffer, 0, writeLength);
                        writeBuffer = WriteBuffer;
                    }

                    amtWritten = Connection.BulkTransfer(_writeEndpoint, writeBuffer, writeLength,
                        timeoutMillis);
                }

                if (amtWritten <= 0)
                {
                    throw new IOException("Error writing " + writeLength
                                                           + " bytes at offset " + offset + " length=" + src.Length);
                }

                Log.Debug(TAG, "Wrote amt=" + amtWritten + " attempted=" + writeLength);
                offset += amtWritten;
            }

            return offset;
        }


        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            byte stopBitsByte = stopBits switch
            {
                StopBits.One => 0,
                StopBits.OnePointFive => 1,
                StopBits.Two => 2,
                _ => throw new IllegalArgumentException("Bad value for stopBits: " + stopBits)
            };

            byte parityBitesByte = parity switch
            {
                Parity.None => 0,
                Parity.Odd => 1,
                Parity.Even => 2,
                Parity.Mark => 3,
                Parity.Space => 4,
                _ => throw new IllegalArgumentException("Bad value for parity: " + parity)
            };

            byte[] msg =
            {
                (byte)(baudRate & 0xff),
                (byte)((baudRate >> 8) & 0xff),
                (byte)((baudRate >> 16) & 0xff),
                (byte)((baudRate >> 24) & 0xff),
                stopBitsByte,
                parityBitesByte,
                (byte)dataBits
            };
            SendAcmControlMessage(SET_LINE_CODING, 0, msg);
        }

        public override bool GetCD() => false; // TODO

        public override bool GetCTS() => false; // TODO

        public override bool GetDSR() => false; // TODO

        public override bool GetDTR() => _dtr;

        public override void SetDTR(bool value)
        {
            _dtr = value;
            SetDtrRts();
        }

        public override bool GetRI() => false; // TODO

        public override bool GetRTS() => _rts;

        public override void SetRTS(bool value)
        {
            _rts = value;
            SetDtrRts();
        }

        private void SetDtrRts()
        {
            var value = (_rts ? 0x2 : 0) | (_dtr ? 0x1 : 0);
            SendAcmControlMessage(SET_CONTROL_LINE_STATE, value, null);
        }
    }

    public static Dictionary<int, int[]> GetSupportedDevices() =>
        new()
        {
            {
                UsbId.VENDOR_ARDUINO, new[]
                {
                    UsbId.ARDUINO_UNO,
                    UsbId.ARDUINO_UNO_R3,
                    UsbId.ARDUINO_MEGA_2560,
                    UsbId.ARDUINO_MEGA_2560_R3,
                    UsbId.ARDUINO_SERIAL_ADAPTER,
                    UsbId.ARDUINO_SERIAL_ADAPTER_R3,
                    UsbId.ARDUINO_MEGA_ADK,
                    UsbId.ARDUINO_MEGA_ADK_R3,
                    UsbId.ARDUINO_LEONARDO,
                    UsbId.ARDUINO_MICRO,
                }
            },
            {
                UsbId.VENDOR_VAN_OOIJEN_TECH, new[]
                {
                    UsbId.VAN_OOIJEN_TECH_TEENSYDUINO_SERIAL
                }
            },
            {
                UsbId.VENDOR_ATMEL, new[]
                {
                    UsbId.ATMEL_LUFA_CDC_DEMO_APP
                }
            },
            {
                UsbId.VENDOR_ELATEC, new[]
                {
                    UsbId.ELATEC_TWN3_CDC,
                    UsbId.ELATEC_TWN4_MIFARE_NFC,
                    UsbId.ELATEC_TWN4_CDC,
                }
            },
            {
                UsbId.VENDOR_LEAFLABS, new[]
                {
                    UsbId.LEAFLABS_MAPLE
                }
            }
        };
}

