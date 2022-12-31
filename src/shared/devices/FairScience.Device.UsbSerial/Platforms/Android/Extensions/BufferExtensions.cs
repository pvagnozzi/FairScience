using Android.Runtime;
using Java.Nio;
using Byte = Java.Lang.Byte;

namespace FairScience.Device.Serial.Platforms.Android.Extensions;

internal static class BufferExtensions
{
    private static nint _byteBufferClassRef;

    private static nint _byteBufferGetBii;

    public static ByteBuffer GetBuffer(this ByteBuffer buffer, JavaArray<Byte> dst, int dstOffset, int byteCount)
    {
        if (_byteBufferClassRef == nint.Zero)
        {
            _byteBufferClassRef = JNIEnv.FindClass("java/nio/ByteBuffer");
        }

        if (_byteBufferGetBii == nint.Zero)
        {
            _byteBufferGetBii = JNIEnv.GetMethodID(_byteBufferClassRef, "get", "([BII)Ljava/nio/ByteBuffer;");
        }

        return Java.Lang.Object.GetObject<ByteBuffer>(
            JNIEnv.CallObjectMethod(
                buffer.Handle,
                _byteBufferGetBii,
                new(dst),
                new(dstOffset),
                new(byteCount)
            ),
            JniHandleOwnership.TransferLocalRef);
    }

    public static byte[] ToByteArray(this ByteBuffer buffer)
    {
        var classHandle = JNIEnv.FindClass("java/nio/ByteBuffer");
        var methodId = JNIEnv.GetMethodID(classHandle, "array", "()[B");
        var resultHandle = JNIEnv.CallObjectMethod(buffer.Handle, methodId);
        var result = JNIEnv.GetArray<byte>(resultHandle);
        JNIEnv.DeleteLocalRef(resultHandle);
        return result;
    }

}
