using System.Collections.Generic;
using System.IO;
using Xunit;

namespace clojureCLR_nrepl.Tests
{
    public class BencodeCodecTests
    {
        [Fact]
        public void EncodeDecode_RoundTrip_Dictionary()
        {
            var data = new Dictionary<string, object>
            {
                ["op"] = "eval",
                ["id"] = 1,
                ["code"] = "(+ 1 2 3)",
                ["list"] = new List<object> { "a", 2L, "c" }
            };

            var bytes = BencodeCodec.Encode(data);
            using var ms = new MemoryStream(bytes);
            var decoded = BencodeCodec.DecodeDict(ms);

            Assert.Equal("eval", decoded["op"]);
            Assert.Equal("1", decoded["id"].ToString());
            Assert.Equal("(+ 1 2 3)", decoded["code"]);

            var list = Assert.IsType<List<object>>(decoded["list"]);
            Assert.Equal("a", list[0]);
            Assert.Equal(2L, list[1]);
            Assert.Equal("c", list[2]);
        }
    }
}
