/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

namespace FairScience.Device.Serial;

public enum Parity
{
    None = 0,
    Odd = 1,
    Even = 2,
    Mark = 3,
    Space = 4,
    NotSet = -1
}
