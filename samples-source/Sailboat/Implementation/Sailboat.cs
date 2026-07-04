using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Sailboat.Implementation
{
    public partial class Sailboat : Form
    {
        public Sailboat()
        {
            InitializeComponent();
        }

        public void MoveTo(bool directionWest, int xc, int yc)
        {
            int index = directionWest ? 0 : 2;
            bool aground = false;
            if (xc < 0)
            {
                xc = 0;
                aground = true;
            }
            else if (xc > 1000)
            {
                xc = 1000;
                aground = true;
            }
            if (yc < 0)
            {
                yc = 0;
                aground = true;
            }
            else if (yc > 1000)
            {
                yc = 1000;
                aground = true;
            }
            if (aground)
                index++;
            state.Text = String.Format("{0},{1} {2}",
                                xc, yc, aground ? "aground" : "sailing");
            int width = sea.Size.Width - boat.Size.Width;
            int height = sea.Size.Height - boat.Size.Height;
            xc = (int)Math.Round((double)xc / 1000 * width);
            yc = (int)Math.Round((double)(1000 - yc) / 1000 * height);
            boat.ImageIndex = index;
            this.boat.Left = xc;
            this.boat.Top = yc;
        }
    }
}
