using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ironmunge
{
    public static class Ck3Bin
    {
        private const string RAKALY_LIB = @"Resources\rakaly.dll";

        [DllImport(RAKALY_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe IntPtr rakaly_ck3_melt(byte* data_ptr, UIntPtr data_len);

        [DllImport(RAKALY_LIB, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool rakaly_melt_error_code(IntPtr meltedBuffer);

        [DllImport(RAKALY_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void rakaly_free_melt(IntPtr meltedBuffer);

        [DllImport(RAKALY_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern UIntPtr rakaly_melt_data_length(IntPtr meltedBuffer);

        [DllImport(RAKALY_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe UIntPtr rakaly_melt_write_data(IntPtr meltedBuffer, byte* destinationBuffer, UIntPtr length);


        public static unsafe string Melt(ReadOnlySpan<byte> data)
        {
            fixed (byte* data_ptr = &MemoryMarshal.GetReference(data))
            {
                var data_len = (UIntPtr)data.Length;
                IntPtr meltedBuffer = IntPtr.Zero;
                try
                {
                    meltedBuffer = rakaly_ck3_melt(data_ptr, data_len);

                    if (rakaly_melt_error_code(meltedBuffer))
                        throw new InvalidOperationException("unable to melt save");

                    UIntPtr meltedBufferLen = rakaly_melt_data_length(meltedBuffer);
                    Trace.Assert(meltedBufferLen != UIntPtr.Zero);

                    fixed (byte* destinationBuffer = new byte[(int)meltedBufferLen])
                    {
                        var wroteLen = rakaly_melt_write_data(meltedBuffer, destinationBuffer, meltedBufferLen);
                        Trace.Assert(wroteLen == meltedBufferLen);

                        return Encoding.UTF8.GetString(destinationBuffer, (int)meltedBufferLen);
                    }
                }
                finally
                {
                    if (meltedBuffer != IntPtr.Zero)
                        rakaly_free_melt(meltedBuffer);
                }
            }
        }
    }
}
