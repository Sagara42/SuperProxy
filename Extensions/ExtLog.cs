using System.Text;

namespace SuperProxy.Extensions
{
    public static class ExtLog
    {
        public static string FormatHex(this byte[] data, int bytesPerBlock = 4, int blocksPerRow = 4)
        {
            StringBuilder builder = new StringBuilder(data.Length * 4);

            int len = data.Length;
            int bytesPerRow = bytesPerBlock * blocksPerRow;

            int lastRowLen = len % bytesPerRow;
            int rows = len / bytesPerRow;

            if (lastRowLen > 0)
                rows++;

            for (int i = 0; i < rows; i++)
            {
                int currentCount = bytesPerRow * i;
                builder.Append("[");
                builder.Append(currentCount);
                builder.Append("]\t");

                int bytesInThisRow = lastRowLen > 0 && rows - 1 == i ? lastRowLen : bytesPerRow;
                for (int k = 0; k < bytesInThisRow; k++)
                {
                    byte res = data[currentCount + k];
                    builder.Append(res.ToString("X2"));

                    if ((k + 1) % bytesPerBlock == 0)
                        builder.Append("  ");
                }

                var diff = bytesPerRow - bytesInThisRow;
                if (diff > 0)
                {

                    var cnt = diff + diff / bytesPerBlock;
                    if (diff % bytesPerBlock > 0)
                        cnt++;

                    for (int k = 0; k < cnt; k++)
                        builder.Append("  ");
                }

                for (int k = 0; k < bytesInThisRow; k++)
                {

                    char res = (char)data[currentCount + k];
                    if (res > 0x1f && res < 0x80)
                        builder.Append(res);
                    else
                        builder.Append(".");
                }

                builder.Append("\n");
            }

            return builder.ToString();
        }
    }
}
