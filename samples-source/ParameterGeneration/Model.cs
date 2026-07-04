using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Modeling;

namespace PG
{

    #region Helper Types

    public enum Frequency
    {
        Once,
        Daily,
        Weekly
    }

    public struct JobInfo
    {
        public string Name;
        public int Time;
        public Frequency Frequency;
    }

    [Flags]
    public enum DaysOfWeek
    {
        None = 0x0,
        Mon = 0x1,
        Tue = 0x2,
        Wed = 0x4,
        Thu = 0x8,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        All = Mon|Tue|Wed|Thu|Fri|Sat|Sun
    }

    #endregion

    #region ModelWithFrequency

    namespace ModelWithFrequency
    {

        static class Model
        {
            [Rule]
            static void AddJob(string name, int time, Frequency frequency)
            {
            }
        }
    }

    #endregion

    #region ModelWithStruct

    namespace ModelWithStruct
    {
        static class Model
        {
            [Rule]
            static void AddJob(JobInfo job, int priority)
            {
            }


        }
    }

    #endregion

    #region ModelWithExpand

    namespace ModelWithExpand
    {
        static class Model
        {
            [Rule]
            static void AddJob(string name, int time, Frequency frequency)
            {
                Condition.In<string>(name, "@$^", "t.cmd", "t.exe");
                Condition.In<int>(time, -1, 60, 3600);
                Combination.Interaction(name);
                Combination.Interaction(time, frequency);
                Combination.Isolated(name == "@$^");
                Combination.Isolated(time == -1);
                Combination.Seeded(name == "t.exe", time == 3600);
                Combination.Expand(name, time, frequency);
            }


        }
    }
    #endregion

    #region ModelWithFlags

    namespace ModelWithFlags
    {
        static class Model
        {
            [Rule]
            static void AddJob(string name, int time, DaysOfWeek days)
            {
            }

        }
    }
    #endregion

    #region ModelWithBitmask

    namespace ModelWithBitmask
    {
        static class Model
        {
            [Rule]
            static void AddJob(string name, int time, uint days)
            {
            }
        }
    }

    #endregion

    #region Probability

    namespace Probability
    {
        static class Model
        {
            [Rule]
            static void CreateFile(string name, bool errorIfExists, bool appendAtEnd)
            {
            }
        }
    }

    #endregion

    #region Equivalence Class

    namespace ModelWithEquivalenceClass
    {
        public static class Model
        {
            [Rule]
            static void AddJob(string name, int time, DaysOfWeek days)
            {
                Combination.Pairwise(name, time, PG.ModelWithEquivalenceClass.Model.ContainsWeekend(days));
            }

            public static bool ContainsWeekend(DaysOfWeek days)
            {
                if ((days & DaysOfWeek.Sat) != DaysOfWeek.None)
                    return true;
                if ((days & DaysOfWeek.Sun) != DaysOfWeek.None)
                    return true;
                return false;
            }
        }
    }

    #endregion


}
