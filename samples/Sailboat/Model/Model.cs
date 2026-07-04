using System;
using Sek.Modeling;

namespace Sailboat.Model
{
    /// <summary>Compass heading, in degrees (ported from Sailboat.Implementation).</summary>
    public enum Heading
    {
        E = 0,
        NE = 45,
        N = 90,
        NW = 135,
        W = 180,
        SW = 225,
        S = 270,
        SE = 315,
    }

    /// <summary>
    /// The Sailboat model. State is the boat position plus grounding flags. Ported from
    /// the classic Spec Explorer Sailboat sample; guards use <see cref="ModelProgram.Require"/>
    /// (Spec Explorer's Condition.IsTrue) and the heading/hours/knots domains come from Cord.
    /// </summary>
    public sealed class SailboatModel : ModelProgram
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool Aground { get; set; }
        public bool AgroundFired { get; set; }

        // State-dependent domains so RunAground's "atx == x" guard is always satisfiable.
        private int[] XDomain() => new[] { X };
        private int[] YDomain() => new[] { Y };

        [Rule("SailboatController.Sail")]
        public void Sail(Heading heading, int hours, int knots)
        {
            Require(!Aground, "already aground");
            Require(hours > 0, "hours must be positive");
            Require(knots > 0, "knots must be positive");

            int miles = knots * hours;
            double radians = (int)heading * Math.PI / 180;
            X += (int)Math.Round(miles * Math.Cos(radians));
            Y += (int)Math.Round(miles * Math.Sin(radians));

            if (X < 0) { Aground = true; X = 0; }
            else if (X > 1000) { Aground = true; X = 1000; }

            if (Y < 0) { Aground = true; Y = 0; }
            else if (Y > 1000) { Aground = true; Y = 1000; }
        }

        [Rule("SailboatController.RunAground")]
        public void RunAground([Domain("XDomain")] int atx, [Domain("YDomain")] int aty)
        {
            Require(Aground, "not aground");
            Require(!AgroundFired, "aground already fired");
            Require(atx == X, "atx must equal x");
            Require(aty == Y, "aty must equal y");
            AgroundFired = true;
        }

        [Rule("SailboatController.Rescue")]
        public void Rescue()
        {
            Require(Aground, "not aground");
            Require(AgroundFired, "aground not fired");
            X = 0;
            Y = 0;
            Aground = false;
            AgroundFired = false;
        }

        [AcceptingCondition]
        public bool InitialPosition() => X == 0 && Y == 0;
    }
}
