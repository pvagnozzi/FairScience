/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;
using FairScience.Device.Serial;
using Java.Lang;
using Boolean = System.Boolean;
using Exception = Java.Lang.Exception;
using Thread = System.Threading.Thread;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial.Platforms.Android.Drivers;

public class ProlificSerialDriver : UsbSerialDriver
{
    private const string TAG = nameof(ProlificSerialDriver);

    public ProlificSerialDriver(UsbDevice device) :
        base(device) 
    {
        Ports.Add(new ProlificSerialPort(Device, 0, this));
    }

    public static Dictionary<int, int[]> GetSupportedDevices() =>
        new()
        {
            {
                UsbId.VENDOR_PROLIFIC, new[]
                {
                    UsbId.PROLIFIC_PL2303,
                    UsbId.PROLIFIC_PL2303GC,
                    UsbId.PROLIFIC_PL2303GB,
                    UsbId.PROLIFIC_PL2303GT,
                    UsbId.PROLIFIC_PL2303GL,
                    UsbId.PROLIFIC_PL2303GE,
                    UsbId.PROLIFIC_PL2303GS

                }
            }
        };


    private class ProlificSerialPort : CommonUsbSerialPort
    {
        private enum DeviceType { DEVICE_TYPE_01, DEVICE_TYPE_T, DEVICE_TYPE_HX, DEVICE_TYPE_HXN }

        private const int USB_READ_TIMEOUT_MILLIS = 1000;
        private const int USB_WRITE_TIMEOUT_MILLIS = 5000;

        private const int USB_RECIP_INTERFACE = 0x01;

        private const int VENDOR_READ_REQUEST = 0x01;
        private const int VENDOR_WRITE_REQUEST = 0x01;
        private const int VENDOR_READ_HXN_REQUEST = 0x81;
        private const int VENDOR_WRITE_HXN_REQUEST = 0x80;

        private const int VENDOR_OUT_REQTYPE = UsbSupport.UsbDirOut | UsbConstants.UsbTypeVendor;
        private const int VENDOR_IN_REQTYPE = UsbSupport.UsbDirIn | UsbConstants.UsbTypeVendor;
        private const int CTRL_OUT_REQTYPE = UsbSupport.UsbDirOut | UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

        private const int WRITE_ENDPOINT = 0x02;
        private const int READ_ENDPOINT = 0x83;
        private const int INTERRUPT_ENDPOINT = 0x81;

        private const int RESET_HXN_REQUEST = 0x07;
        private const int FLUSH_RX_REQUEST = 0x08;
        private const int FLUSH_TX_REQUEST = 0x09;

        private const int SET_LINE_REQUEST = 0x20; // same as CDC SET_LINE_CODING
        private const int SET_CONTROL_REQUEST = 0x22; // same as CDC SET_CONTROL_LINE_STATE
        private const int SEND_BREAK_REQUEST = 0x23; // same as CDC SEND_BREAK
        private const int GET_CONTROL_HXN_REQUEST = 0x80;
        private const int GET_CONTROL_REQUEST = 0x87;
        private const int STATUS_NOTIFICATION = 0xa1; // similar to CDC SERIAL_STATE but different length

        /* RESET_HXN_REQUEST */
        private const int RESET_HXN_RX_PIPE = 1;
        private const int RESET_HXN_TX_PIPE = 2;

        /* SET_CONTROL_REQUEST */
        private const int CONTROL_DTR = 0x01;
        private const int CONTROL_RTS = 0x02;

        /* GET_CONTROL_REQUEST */
        private const int GET_CONTROL_FLAG_CD = 0x02;
        private const int GET_CONTROL_FLAG_DSR = 0x04;
        private const int GET_CONTROL_FLAG_RI = 0x01;
        private const int GET_CONTROL_FLAG_CTS = 0x08;

        /* GET_CONTROL_HXN_REQUEST */
        private const int GET_CONTROL_HXN_FLAG_CD = 0x40;
        private const int GET_CONTROL_HXN_FLAG_DSR = 0x20;
        private const int GET_CONTROL_HXN_FLAG_RI = 0x80;
        private const int GET_CONTROL_HXN_FLAG_CTS = 0x08;

        /* interrupt endpoint read */
        private const int STATUS_FLAG_CD = 0x01;
        private const int STATUS_FLAG_DSR = 0x02;
        private const int STATUS_FLAG_RI = 0x08;
        private const int STATUS_FLAG_CTS = 0x80;

        private const int STATUS_BUFFER_SIZE = 10;
        private const int STATUS_BYTE_IDX = 8;
            
        private readonly object _readStatusThreadLock = new();
        private DeviceType _deviceType = DeviceType.DEVICE_TYPE_HX;

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;
        private UsbEndpoint _interruptEndpoint;

        private int _controlLinesValue;
        private int _baudRate = -1;
        private int _dataBits = -1;
        private StopBits _stopBits = StopBits.NotSet;
        private Parity _parity = Parity.NotSet;

        private int _status;
        private volatile Thread _readStatusThread = null;
        private bool _stopReadStatusThread;
        private IOException _readStatusException;


        public ProlificSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver)
            : base(device, portNumber, driver)
        {
        }


        private byte[] InControlTransfer(int requestType, int request,
            int value, int index, int length)
        {
            var buffer = new byte[length];
            var result = Connection.ControlTransfer((UsbAddressing)requestType, request, value,
                index, buffer, length, USB_READ_TIMEOUT_MILLIS);
            if (result != length)
            {
                throw new IOException($"ControlTransfer with value {value} failed: {result}");
            }
            return buffer;
        }

        private void OutControlTransfer(int requestType, int request,
            int value, int index, byte[] data)
        {
            var length = data?.Length ?? 0;
            var result = Connection.ControlTransfer((UsbAddressing)requestType, request, value,
                index, data, length, USB_WRITE_TIMEOUT_MILLIS);
            if (result != length)
            {
                throw new IOException($"ControlTransfer with value {value} failed: {result}");
            }
        }

        private byte[] VendorIn(int value, int index, int length)
        {
            var request = (_deviceType == DeviceType.DEVICE_TYPE_HXN) ? VENDOR_READ_HXN_REQUEST : VENDOR_READ_REQUEST;
            return InControlTransfer(VENDOR_IN_REQTYPE, request, value, index, length);
        }

        private void VendorOut(int value, int index, byte[] data)
        {
            var request = (_deviceType == DeviceType.DEVICE_TYPE_HXN) ? VENDOR_WRITE_HXN_REQUEST : VENDOR_WRITE_REQUEST;
            OutControlTransfer(VENDOR_OUT_REQTYPE, request, value, index, data);
        }

        private void ResetDevice() => 
            PurgeBuffers(true, true);
            

        private void CtrlOut(int request, int value, int index, byte[] data) =>
            OutControlTransfer(CTRL_OUT_REQTYPE, request, value, index, data);
            

        private bool TestHxStatus()
        {
            try
            {
                InControlTransfer(VENDOR_IN_REQTYPE, VENDOR_READ_REQUEST, 0x8080, 0, 1);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void DoBlackMagic()
        {
            if (_deviceType == DeviceType.DEVICE_TYPE_HXN)
            {
                return;
            }

            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 0, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 1, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorOut(0, 1, null);
            VendorOut(1, 0, null);
            VendorOut(2, (_deviceType == DeviceType.DEVICE_TYPE_HX) ? 0x44 : 0x24, null);
        }

        private void SetControlLines(int newControlLinesValue)
        {
            CtrlOut(SET_CONTROL_REQUEST, newControlLinesValue, 0, null);
            _controlLinesValue = newControlLinesValue;
        }

        private void ReadStatusThreadFunction()
        {
            try
            {
                while (!_stopReadStatusThread)
                {
                    var buffer = new byte[STATUS_BUFFER_SIZE];
                    var readBytesCount = Connection.BulkTransfer(_interruptEndpoint,
                        buffer,
                        STATUS_BUFFER_SIZE,
                        500);

                    switch (readBytesCount)
                    {
                        case <= 0:
                            continue;
                        case STATUS_BUFFER_SIZE:
                            _status = buffer[STATUS_BYTE_IDX] & 0xff;
                            break;
                        default:
                            throw new IOException(
                                $"Invalid CTS / DSR / CD / RI status buffer received, expected {STATUS_BUFFER_SIZE} bytes, but received {readBytesCount}");
                    }
                }
            }
            catch (IOException e)
            {
                _readStatusException = e;
            }
        }

        private int GetStatus()
        {
            if (_readStatusThread == null && (_readStatusException == null))
            {
                lock (_readStatusThreadLock)
                {
                    if (_readStatusThread == null)
                    {
                        _status = 0;
                        if (_deviceType == DeviceType.DEVICE_TYPE_HXN)
                        {
                            var data = VendorIn(GET_CONTROL_HXN_REQUEST, 0, 1);
                            if ((data[0] & GET_CONTROL_HXN_FLAG_CTS) == 0) _status |= STATUS_FLAG_CTS;
                            if ((data[0] & GET_CONTROL_HXN_FLAG_DSR) == 0) _status |= STATUS_FLAG_DSR;
                            if ((data[0] & GET_CONTROL_HXN_FLAG_CD) == 0) _status |= STATUS_FLAG_CD;
                            if ((data[0] & GET_CONTROL_HXN_FLAG_RI) == 0) _status |= STATUS_FLAG_RI;
                        }
                        else
                        {
                            var data = VendorIn(GET_CONTROL_REQUEST, 0, 1);
                            if ((data[0] & GET_CONTROL_FLAG_CTS) == 0) _status |= STATUS_FLAG_CTS;
                            if ((data[0] & GET_CONTROL_FLAG_DSR) == 0) _status |= STATUS_FLAG_DSR;
                            if ((data[0] & GET_CONTROL_FLAG_CD) == 0) _status |= STATUS_FLAG_CD;
                            if ((data[0] & GET_CONTROL_FLAG_RI) == 0) _status |= STATUS_FLAG_RI;
                        }
                        var mReadStatusThreadDelegate = new ThreadStart(ReadStatusThreadFunction);

                        _readStatusThread = new Thread(mReadStatusThreadDelegate);
                        _readStatusThread.Start();
                    }
                }
            }


            /* throw and clear an exception which occured in the status read thread */
            var readStatusException = _readStatusException;
            if (_readStatusException == null)
            {
                return _status;
            }

            _readStatusException = null;
            throw readStatusException ?? new IOException();
        }

        private bool TestStatusFlag(int flag) =>
            (GetStatus() & flag) == flag;
            

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
            {
                throw new IOException("Already open");
            }

            var usbInterface = Device.GetInterface(0);

            if (!connection.ClaimInterface(usbInterface, true))
            {
                throw new IOException("Error claiming Prolific interface 0");
            }
            
            SetConnection(connection);
            var opened = false;
            try
            {
                for (var i = 0; i < usbInterface.EndpointCount; ++i)
                {
                    var currentEndpoint = usbInterface.GetEndpoint(i);

                    switch (currentEndpoint?.Address)
                    {
                        case (UsbAddressing)READ_ENDPOINT:
                            lock (ReadBufferLock)
                            {
                                _readEndpoint = currentEndpoint;
                            }
                            break;

                        case (UsbAddressing)WRITE_ENDPOINT:
                            lock (WriteBufferLock)
                            {
                                _writeEndpoint = currentEndpoint;
                            }
                            break;

                        case (UsbAddressing)INTERRUPT_ENDPOINT:
                            _interruptEndpoint = currentEndpoint;
                            break;
                        case UsbAddressing.In:
                            break;
                        case UsbAddressing.Out:
                            break;
                        case UsbAddressing.NumberMask:
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
                }

                var rawDescriptors = connection.GetRawDescriptors();
                if (rawDescriptors == null || rawDescriptors.Length < 14)
                {
                    throw new IOException("Could not get device descriptors");
                }
                var usbVersion = (rawDescriptors[3] << 8) + rawDescriptors[2];
                var deviceVersion = (rawDescriptors[13] << 8) + rawDescriptors[12];
                var maxPacketSize0 = rawDescriptors[7];

                if (Device.DeviceClass == UsbClass.Comm || maxPacketSize0 != 64)
                {
                    _deviceType = DeviceType.DEVICE_TYPE_01;
                }
                else switch (deviceVersion)
                {
                    case 0x300 when usbVersion == 0x200:
                    // TB
                    case 0x500:
                        _deviceType = DeviceType.DEVICE_TYPE_T; // TA
                        break;
                    default:
                    {
                        if (usbVersion == 0x200 && !TestHxStatus())
                        {
                            _deviceType = DeviceType.DEVICE_TYPE_HXN;
                        }
                        else
                        {
                            _deviceType = DeviceType.DEVICE_TYPE_HX;
                        }

                        break;
                    }
                }
                    
                SetControlLines(_controlLinesValue);
                ResetDevice();

                DoBlackMagic();
                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    SetConnection();
                    connection.ReleaseInterface(usbInterface);
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
                _stopReadStatusThread = true;
                lock (_readStatusThreadLock)
                {
                    if (_readStatusThread != null)
                    {
                        try
                        {
                            _readStatusThread.Join();
                        }
                        catch (Exception e)
                        {
                            Log.Warn(TAG, "An error occured while waiting for status read thread", e);
                        }
                    }
                }
                ResetDevice();
            }
            finally
            {
                try
                {
                    Connection.ReleaseInterface(Device.GetInterface(0));
                }
                finally
                {
                    SetConnection();
                }
            }
        }

        public override int Read(byte[] dest, int timeoutMillis)
        {
            lock (ReadBufferLock)
            {
                var readAmt = System.Math.Min(dest.Length, ReadBuffer.Length);
                var numBytesRead = Connection.BulkTransfer(_readEndpoint, ReadBuffer,
                    readAmt, timeoutMillis);
                if (numBytesRead < 0)
                {
                    return 0;
                }
                Buffer.BlockCopy(ReadBuffer, 0, dest, 0, numBytesRead);
                return numBytesRead;
            }
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

                    writeLength = System.Math.Min(src.Length - offset, WriteBuffer.Length);
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

                    amtWritten = Connection.BulkTransfer(_writeEndpoint,
                        writeBuffer, writeLength, timeoutMillis);
                }

                if (amtWritten <= 0)
                {
                    throw new IOException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                offset += amtWritten;
            }
            return offset;
        }

        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            if ((_baudRate == baudRate) && (_dataBits == dataBits)
                && (_stopBits == stopBits) && (_parity == parity))
            {
                // Make sure no action is performed if there is nothing to change
                return;
            }

            var lineRequestData = new byte[7];

            lineRequestData[0] = (byte)(baudRate & 0xff);
            lineRequestData[1] = (byte)((baudRate >> 8) & 0xff);
            lineRequestData[2] = (byte)((baudRate >> 16) & 0xff);
            lineRequestData[3] = (byte)((baudRate >> 24) & 0xff);

            switch (stopBits)
            {
                case StopBits.NotSet:
                default:
                    throw new IllegalArgumentException("Unknown stopBits value: " + stopBits);

                case StopBits.One:
                    lineRequestData[4] = 0;
                    break;

                case StopBits.OnePointFive:
                    lineRequestData[4] = 1;
                    break;

                case StopBits.Two:
                    lineRequestData[4] = 2;
                    break;

            }

            lineRequestData[5] = parity switch
            {
                Parity.None => 0,
                Parity.Odd => 1,
                Parity.Even => 2,
                Parity.Mark => 3,
                Parity.Space => 4,
                _ => throw new IllegalArgumentException("Unknown parity value: " + parity)
            };

            lineRequestData[6] = (byte)dataBits;

            CtrlOut(SET_LINE_REQUEST, 0, 0, lineRequestData);

            ResetDevice();

            _baudRate = baudRate;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _parity = parity;
        }

        public override bool GetCD() => TestStatusFlag(STATUS_FLAG_CD);
        public override bool GetCTS() => TestStatusFlag(STATUS_FLAG_CTS);
        public override bool GetDSR() => TestStatusFlag(STATUS_FLAG_DSR);

        public override bool GetDTR() => (_controlLinesValue & CONTROL_DTR) == CONTROL_DTR;
    

        public override void SetDTR(bool value)
        {
            int newControlLinesValue;
            if (value)
            {
                newControlLinesValue = _controlLinesValue | CONTROL_DTR;
            }
            else
            {
                newControlLinesValue = _controlLinesValue & ~CONTROL_DTR;
            }
            SetControlLines(newControlLinesValue);
        }


        public override bool GetRI() => TestStatusFlag(STATUS_FLAG_RI);

        public override bool GetRTS() => (_controlLinesValue & CONTROL_RTS) == CONTROL_RTS;

        public override void SetRTS(bool value)
        {
            int newControlLinesValue;
            if (value)
            {
                newControlLinesValue = _controlLinesValue | CONTROL_RTS;
            }
            else
            {
                newControlLinesValue = _controlLinesValue & ~CONTROL_RTS;
            }
            SetControlLines(newControlLinesValue);
        }

        public override bool PurgeBuffers(Boolean purgeReadBuffers, Boolean purgeWriteBuffers)
        {
            if (_deviceType == DeviceType.DEVICE_TYPE_HXN)
            {
                var index = 0;
                if (purgeWriteBuffers) index |= RESET_HXN_RX_PIPE;
                if (purgeReadBuffers) index |= RESET_HXN_TX_PIPE;
                if (index != 0)
                    VendorOut(RESET_HXN_REQUEST, index, null);
            }
            else
            {
                if (purgeWriteBuffers)
                    VendorOut(FLUSH_RX_REQUEST, 0, null);
                if (purgeReadBuffers)
                    VendorOut(FLUSH_TX_REQUEST, 0, null);
            }
            return purgeReadBuffers || purgeWriteBuffers;
        }
    }
}
