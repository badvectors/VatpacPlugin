using System;

namespace VatpacPlugin
{
    public class Position
    {
        public Position() { }

        public Position(string name, DateTime eto, DateTime ato, bool mprArmed)
        {
            Name = name;
            ETO = eto;
            ATO = ato;
            MPRArmed = mprArmed;
        }

        public string Name { set; get; }
        public DateTime ETO { set; get; }
        public DateTime ATO { set; get; }
        public bool MPRArmed { get; set; }
    }
}
