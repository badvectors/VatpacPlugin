using System;

namespace VatpacPlugin
{
    public class Atc
    {
        public int CID { get; set; }
        public Guid Token { get; set; }
        public DateTime ExpiryUtc { get; set; }
    }
}
