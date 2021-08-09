using System;
using System.IO;
using System.Text;

namespace Ironmunge.Common
{
    public static class CKSettings
    {
        public static Encoding SaveGameEncoding { get; }
            = CodePagesEncodingProvider.Instance.GetEncoding(1252) ?? throw new InvalidOperationException("Couldn't find western-1252 code page");
    }
}
