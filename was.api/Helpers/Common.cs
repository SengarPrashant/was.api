using System.IO.Compression;

namespace was.api.Helpers
{
    public class Common
    {
        //public static byte[] Compress(byte[] data)
        //{
        //    using var output = new MemoryStream();
        //    using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        //    {
        //        gzip.Write(data, 0, data.Length);
        //    }
        //    return output.ToArray();
        //}

        //public static byte[] Decompress(byte[] compressed)
        //{
        //    using var input = new MemoryStream(compressed);
        //    using var gzip = new GZipStream(input, CompressionMode.Decompress);
        //    using var output = new MemoryStream();
        //    gzip.CopyTo(output);
        //    return output.ToArray();
        //}

        public static byte[] Compress(byte[] data)
        {
            // skip compression for empty/null
            if (data == null || data.Length == 0)
                return new byte[] { 0 }; // header only

            using var output = new MemoryStream();

            // Write compression flag (1 = compressed)
            output.WriteByte(1);

            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        public static byte[] Decompress(byte[] input)
        {
            if (input == null || input.Length == 0)
                return Array.Empty<byte>();

            // First byte tells us if compressed
            byte flag = input[0];
            if (flag == 0)
            {
                // not compressed → return the rest as-is
                return input.Skip(1).ToArray();
            }

            using var inputStream = new MemoryStream(input, 1, input.Length - 1); // skip header
            using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);

            return output.ToArray();
        }

        public static string GenerateTemporaryPassword(int length = 10)
        {
            //IindiqubeEHS@employeeId
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string GenerateRequestId(string formType, long id)
        {
            switch (formType)
            {
                case OptionTypes.work_permit:
                    return $"WP-{id}";
                case OptionTypes.incident:
                    return $"INC-{id}";
                default:
                    return $"{id}";
            }
        }
    }
}
