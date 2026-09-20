using System;
using System.Windows.Forms;

namespace VatpacPlugin
{
    /// <summary>
    /// A TabControl with no tabs: the pages of the VATPAC window, which are chosen from the dropdown above
    /// it rather than from a tab strip. Still a real TabControl so that pages are added and laid out in
    /// the designer as usual - the strip is only hidden when running, and is there to click on at design
    /// time.
    /// </summary>
    public class VatpacPages : TabControl
    {
        private const int TCM_ADJUSTRECT = 0x1328;

        protected override void WndProc(ref Message m)
        {
            // This is the control asking how much of itself is left for the page once the strip and
            // border are taken out. Answering without touching the rectangle says "all of it", so the
            // page covers both.
            if (m.Msg == TCM_ADJUSTRECT && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
