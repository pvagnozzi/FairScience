/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.OS;
using FairScience.Device.UsbSerial.Platforms.Android.Drivers;
using Java.Interop;
using Object = Java.Lang.Object;

namespace FairScience.Device.UsbSerial.Platforms.Android.Extensions;

public sealed class UsbSerialPortInfo : Java.Lang.Object, IParcelable
{
    private static readonly IParcelableCreator creator = new ParcelableCreator();

    [ExportField("CREATOR")]
    public static IParcelableCreator GetCreator()
    {
        return creator;
    }

    public UsbSerialPortInfo()
    {
    }

    public UsbSerialPortInfo(UsbSerialPort port)
    {
        var device = port.Driver.Device;
        VendorId = device.VendorId;
        DeviceId = device.DeviceId;
        PortNumber = port.PortNumber;
    }

    private UsbSerialPortInfo(Parcel parcel)
    {
        VendorId = parcel.ReadInt();
        DeviceId = parcel.ReadInt();
        PortNumber = parcel.ReadInt();
    }

    public int VendorId { get; set; }

    public int DeviceId { get; set; }

    public int PortNumber { get; set; }

    #region IParcelable implementation

    public int DescribeContents() => 0;

    public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
    {
        dest.WriteInt(VendorId);
        dest.WriteInt(DeviceId);
        dest.WriteInt(PortNumber);
    }

    #endregion


    public sealed class ParcelableCreator : Java.Lang.Object, IParcelableCreator
    {
        #region IParcelableCreator implementation

        public Object CreateFromParcel(Parcel parcel) => new UsbSerialPortInfo(parcel);

        public Object[] NewArray(int size) => new Object[size];

        #endregion
    }

}
