using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Modeling;
using Microsoft.Xrt.Runtime;

using Sailboat.Implementation;

[assembly: NativeType(typeof(System.Math))]
namespace Sailboat.Model
{
    public static class SailboatModel
    {
        static int x, y;
        static bool runAground = false;
        static bool agroundFired = false;

        /// <summary>
        /// State description could be represented as a probe value, refer details in SailBoatView file
        /// </summary>
        /// <returns></returns>
        [Probe]
        public static string Position()
        {
            return string.Format("{0},{1}", x, y);
        }

        [AcceptingStateCondition]
        static bool InitialPosition()
        {
            return x == 0 && y == 0;
        }

        [Rule]
        static void Sail(Heading heading, int hours, int knots)
        {
            // Because the Math functions will influence parameter generation functions, so we expand the parameter explicitly here as a work-around
            Combination.Expand(heading, hours, knots);
            
            Condition.IsTrue(!runAground);
            Condition.IsTrue(hours > 0);
            Condition.IsTrue(knots > 0); 

            int miles = knots * hours;
            double radians = ((int) heading * Math.PI / 180);
            x += (int) Math.Round(miles * Math.Cos(radians));
            y += (int) Math.Round(miles * Math.Sin(radians));
            if (x < 0)
            {
                runAground = true;
                x = 0;
            }
            else if (x > 1000)
            {
                runAground = true;
                x = 1000;
            }
            if (y < 0)
            {
                runAground = true;
                y = 0;
            }
            else if (y > 1000)
            {
                runAground = true;
                y = 1000;
            }
        }
                       

       
        [Rule]
        static void RunAground(int atx, int aty)
        {
            Condition.IsTrue(runAground);
            Condition.IsTrue(!agroundFired);
            Condition.IsTrue(atx == x);
            Condition.IsTrue(aty == y);
            agroundFired = true;
        }

        [Rule]
        static void Rescue()
        {
            Condition.IsTrue(runAground);
            Condition.IsTrue(agroundFired);
            x = y = 0;
            runAground = false;
            agroundFired = false;
        }

        public static bool AwayFromTheShore
        {
            get
            {
                return 350 < x && x < 650 && 350 < y && y < 650;
            }
        }
                

    }
}
