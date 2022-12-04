/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */


using Android.Hardware.Usb;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public interface IUsbSerialDriver
{
    UsbDevice Device { get; }

    IList<UsbSerialPort> Ports { get; }

}