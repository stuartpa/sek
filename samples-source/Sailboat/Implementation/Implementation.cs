using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace Sailboat.Implementation
{

    public enum Heading
    {
        N = 90,
        S = 270,
        E = 0,
        W = 180,
        NE = 45,
        NW = 135,
        SE = 315,
        SW = 225
    }

    public delegate void AgroundDelegate(int xc, int yc);

    public static class SailboatController
    {
        static bool roundingBug = false; // set this to true to inject a bug in the implementation
        const double epsilon = 0.0001; // epsilon is used to control accuracy loss when invoking Math functions

        static int xpos = 0;
        static int ypos = 0;
        static Sailboat form;

        static Sailboat Form
        {
            get
            {
                if (form == null)
                {
                    form = new Sailboat();
                    form.MoveTo(true, 0, 0);
                    form.Show();
                    Application.DoEvents();
                }
                return form;
            }
        }
              

        public static void Sail(Heading heading, int hours, int knots)
        {
            bool directionWest = (int)heading <= 225;
            bool runAground = false;

            double xd = xpos;
            double yd = ypos;
            double radians = ((int)heading * Math.PI / 180);
            double xds = knots * Math.Cos(radians);
            double yds = knots * Math.Sin(radians);
            for (int i = 1; i <= hours; i++)
            {
                xd += xds;
                yd += yds;
                if (roundingBug)
                {
                    xds = Math.Round(xds);
                    yds = Math.Round(yds);
                }
                if (!runAground)
                {
                    Form.MoveTo(xds >= 0, (int)xd, (int)yd);
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(20);
                    runAground = xd + epsilon < 0 || xd - epsilon > 1000 || yd + epsilon < 0 || yd -epsilon > 1000;
                }
            }
            xpos = (int)Math.Round(xd);
            ypos = (int)Math.Round(yd);
            if (runAground)
            {
                if (xpos < 0)
                    xpos = 0;
                else if (xpos > 1000)
                    xpos = 1000;
                if (ypos < 0)
                    ypos = 0;
                else if (ypos > 1000)
                    ypos = 1000;
                if (RunAground != null)
                    RunAground(xpos, ypos);
                Thread.Sleep(500);
            }
        }

        public static event AgroundDelegate RunAground;
        
        public static void Rescue()
        {
            Form.MoveTo(true, 0, 0);
            xpos = ypos = 0;
            Application.DoEvents();
            Thread.Sleep(500);
            Form.Close();
            Form.Dispose();
            form = null;
        }

        static void Main()
        {
            for (int i = 0; i < 14; i++)
            {
                Sail(Heading.NE, 5, 25);
            }
            for (int i = 0; i < 14; i++)
            {
                Sail(Heading.SW, 5, 30);
            }
        }
    }
}
