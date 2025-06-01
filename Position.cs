using System;

namespace VatpacPlugin
{
    public class Position
    {
        public Position() { }

        public Position(string name, DateTime eto, DateTime ato, DateTime seto, bool mprArmed)
        {
            Name = name;
            ETO = eto;
            ATO = ato;
            SETO = seto;
            MPRArmed = mprArmed;
        }

        public string Name { set; get; }
        public DateTime ETO { set; get; }
        public DateTime ATO { set; get; }
        public DateTime SETO { set; get; }

        public bool MPRArmed { get; set; }
    }
}
