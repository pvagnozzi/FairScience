/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;

using Java.Nio;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public class STM32SerialDriver : UsbSerialDriver
{
    //private readonly string TAG = nameof(STM32SerialDriver);

    private int _ctrlInterf;

	public STM32SerialDriver(UsbDevice device)
	    : base(device)
	{
        Ports.Add(new STM32SerialPort(device, 0, this));
	}

	public class STM32SerialPort : CommonUsbSerialPort
	{
        private const string TAG = nameof(STM32SerialDriver);

        private readonly bool _enableAsyncReads;
		private UsbInterface _controlInterface;
        private UsbInterface _dataInterface;

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;

        private bool _rts = false;
        private bool _dtr = false;

		private const int USB_WRITE_TIMEOUT_MILLIS = 5000;

        private const int USB_RECIP_INTERFACE = 0x01;
        private const int USB_RT_AM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

        private const int SET_LINE_CODING = 0x20; // USB CDC 1.1 section 6.2
        private const int SET_CONTROL_LINE_STATE = 0x22;

		public STM32SerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber, driver)
		{
            _enableAsyncReads = true;
		}

		private void SendAcmControlMessage(int request, int value, byte[] buf) =>
			Connection.ControlTransfer((UsbAddressing)USB_RT_AM, request, value, ((STM32SerialDriver)Driver)._ctrlInterf, buf, buf?.Length ?? 0, USB_WRITE_TIMEOUT_MILLIS);

		public override void Open(UsbDeviceConnection connection)
		{
            if (Connection != null)
            {
                throw new IOException("Already opened.");
            }

            SetConnection(connection);
			var opened = false;
			var controlInterfaceFound = false;
			try
			{
				for (var i = 0; i < mDevice.InterfaceCount; i++)
				{
					_controlInterface = mDevice.GetInterface(i);
                    if (_controlInterface.InterfaceClass != UsbClass.Comm)
                    {
                        continue;
                    }

                    if (!Connection!.ClaimInterface(_controlInterface, true))
                    {
                        throw new IOException("Could not claim control interface");
                    }

                    ((STM32SerialDriver)Driver)._ctrlInterf = i;
                    controlInterfaceFound = true;
                    break;
                }

                if (!controlInterfaceFound)
                {
                    throw new IOException("Could not claim control interface");
                }


                for (var i = 0; i < mDevice.InterfaceCount; i++)
				{
					_dataInterface = mDevice.GetInterface(i);
                    if (_dataInterface.InterfaceClass != UsbClass.CdcData)
                    {
                        continue;
                    }

                    if (!Connection.ClaimInterface(_dataInterface, true))
                    {
                        throw new IOException("Could not claim data interface");
                    }

                    
                    _readEndpoint = _dataInterface.GetEndpoint(1);
                    lock (WriteBufferLock)
                    {
                        _writeEndpoint = _dataInterface.GetEndpoint(0);
                    }

                    opened = true;
                    break;
                }

                if (!opened)
                {
                    throw new IOException("Could not claim data interface.");
                }
            }
			finally
			{
                if (!opened)
                {
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

            Connection.Close();
			SetConnection();
		}

		public override int Read(byte[] dest, int timeoutMillis)
		{
			if(_enableAsyncReads)
			{
				var request = new UsbRequest();
				try
				{
					request.Initialize(Connection, _readEndpoint);
					var buf = ByteBuffer.Wrap(dest);
                    if (!request.Queue(buf, dest.Length))
                    {
                        throw new IOException("Error queuing request");
                    }

                    var response = Connection.RequestWait();
                    if (response == null)
                    {
                        throw new IOException("Null response");
                    }

                    var nread = buf.Position();
					return nread > 0 ? nread : 0;
                }
				finally
				{
					request.Close();
				}
			}

			int numBytesRead;
			lock(ReadBufferLock)
			{
				int readAmt = Math.Min(dest.Length, ReadBuffer.Length);
				numBytesRead = Connection.BulkTransfer(_readEndpoint, ReadBuffer, readAmt, timeoutMillis);
				if(numBytesRead < 0)
				{
					// This sucks: we get -1 on timeout, not 0 as preferred.
					// We *should* use UsbRequest, except it has a bug/api oversight
					// where there is no way to determine the number of bytes read
					// in response :\ -- http://b.android.com/28023
					if (timeoutMillis == int.MaxValue)
					{
						// Hack: Special case "~infinite timeout" as an error.
						return -1;
					}

					return 0;
				}
				Array.Copy(ReadBuffer, 0, dest, 0, numBytesRead);
			}
			return numBytesRead;
		}

		public override int Write(byte[] src, int timeoutMillis)
		{
			var offset = 0;

			while(offset < src.Length)
			{
				int writeLength;
				int amtWritten;

				lock(WriteBufferLock)
				{
					byte[] writeBuffer;

					writeLength = Math.Min(src.Length - offset, WriteBuffer.Length);
					if (offset == 0)
						writeBuffer = src;
					else
					{
						Array.Copy(src, offset, WriteBuffer, 0, writeLength);
						writeBuffer = WriteBuffer;
					}

					amtWritten = Connection.BulkTransfer(_writeEndpoint, writeBuffer, writeLength, timeoutMillis);
				}
				if(amtWritten <= 0)
					throw new IOException($"Error writing {writeLength} bytes at offset {offset} length={src.Length}");

				Log.Debug(TAG, $"Wrote amt={amtWritten} attempted={writeLength}");
				offset += amtWritten;
			}

			return offset;
		}

		public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
		{
            byte stopBitsBytes = stopBits switch
            {
                StopBits.One => 0,
                StopBits.OnePointFive => 1,
                StopBits.Two => 2,
                _ => throw new ArgumentException($"Bad value for stopBits: {stopBits}")
            };

            byte parityBitesBytes = parity switch
            {
                Parity.None => 0,
                Parity.Odd => 1,
                Parity.Even => 2,
                Parity.Mark => 3,
                Parity.Space => 4,
                _ => throw new ArgumentException($"Bad value for parity: {parity}")
            };

            byte[] msg = {
				(byte)(baudRate & 0xff),
				(byte) ((baudRate >> 8 ) & 0xff),
				(byte) ((baudRate >> 16) & 0xff),
				(byte) ((baudRate >> 24) & 0xff),
				stopBitsBytes,
				parityBitesBytes,
				(byte) dataBits
			};
			SendAcmControlMessage(SET_LINE_CODING, 0, msg);
		}

		public override bool GetCD() =>
			false; //TODO

		public override bool GetCTS() =>
			false; //TODO

		public override bool GetDSR() =>
			false; // TODO

		public override bool GetDTR() =>
			_dtr;

		public override void SetDTR(bool value)
		{
			_dtr = value;
			SetDtrRts();
		}

		public override bool GetRI() =>
			false; //TODO

		public override bool GetRTS() =>
			_rts; //TODO

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

		public static Dictionary<int, int[]> GetSupportedDevices() =>
            new()
            {
                {
                    UsbId.VENDOR_STM, new int[]
                    {
                        UsbId.STM32_STLINK,
                        UsbId.STM32_VCOM
                    }
                }
            };
    }
}

