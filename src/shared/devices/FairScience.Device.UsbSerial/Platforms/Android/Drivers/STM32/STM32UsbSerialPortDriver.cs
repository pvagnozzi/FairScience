/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial.Platforms.Android.Drivers.STM32;

public class STM32UsbSerialPortDriver : CommonUsbSerialPortDriver
{
    #region Consts
    private const int USB_WRITE_TIMEOUT_MILLIS = 5000;
    private const int USB_RECIP_INTERFACE = 0x01;
    private const int USB_RT_AM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;
    private const int SET_LINE_CODING = 0x20;
    private const int SET_CONTROL_LINE_STATE = 0x22;
    #endregion

    #region Fields
    private int _controlInterfaceIndex;
    private bool _rts;
    private bool _dtr;
    #endregion

    public STM32UsbSerialPortDriver(UsbDevice device, int portNumber, ILogger logger) :
        base(device, portNumber, logger)
    {
    }

    #region Overrides
    public override bool GetCD() => false;
    public override bool GetCTS() => false;
    public override bool GetDSR() => false;
    public override bool GetDTR() => _dtr;
    public override void SetDTR(bool value)
    {
        _dtr = value;
        SetDtrRts();
    }
    public override bool GetRI() => false;
    public override bool GetRTS() => _rts;
    public override void SetRTS(bool value)
    {
        _rts = value;
        SetDtrRts();
    }

    protected override void SetInterfaces(UsbDevice device)
    {
        var (_, ctrlIndex) = FindInterface(device, x => x.InterfaceClass == UsbClass.Comm);
        var (dataInterface, _) = FindInterface(device, x => x.InterfaceClass == UsbClass.CdcData);
        SetReadEndPoint(dataInterface.GetEndpoint(1));
        SetWriteEndPoint(dataInterface.GetEndpoint(0));

        _controlInterfaceIndex = ctrlIndex;
    }

    protected override void SetParameters(UsbDeviceConnection connection, SerialPortParameters parameters)
    {
        byte stopBitsBytes = parameters.StopBits switch
        {
            StopBits.One => 0,
            StopBits.OnePointFive => 1,
            StopBits.Two => 2,
            _ => throw new ArgumentException($"Bad value for stopBits: {parameters.StopBits}")
        };

        byte parityBitesBytes = parameters.Partity switch
        {
            Parity.None => 0,
            Parity.Odd => 1,
            Parity.Even => 2,
            Parity.Mark => 3,
            Parity.Space => 4,
            _ => throw new ArgumentException($"Bad value for parity: {parameters.Partity}")
        };

        var baudRate = parameters.BaudRate;

        byte[] msg =
        {
            (byte)(baudRate & 0xff),
            (byte)(baudRate >> 8 & 0xff),
            (byte)(baudRate >> 16 & 0xff),
            (byte)(baudRate >> 24 & 0xff),
            stopBitsBytes,
            parityBitesBytes,
            (byte)parameters.DataBits
        };
        SendAcmControlMessage(SET_LINE_CODING, 0, msg);
    }
    #endregion

    #region Private Methods
    private void SetDtrRts()
    {
        var value = (_rts ? 0x2 : 0) | (_dtr ? 0x1 : 0);
        SendAcmControlMessage(SET_CONTROL_LINE_STATE, value, null);
    }

    private void SendAcmControlMessage(int request, int value, byte[] buf) =>
        Connection.ControlTransfer((UsbAddressing)USB_RT_AM, request, value, _controlInterfaceIndex, buf,
            buf?.Length ?? 0,
            USB_WRITE_TIMEOUT_MILLIS);
    #endregion

}

