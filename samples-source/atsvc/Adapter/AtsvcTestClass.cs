using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Microsoft.SpecExplorer.Runtime.Testing;
using ATSvc.Adapter;

namespace GeneratedTests
{
    public class AtsvcTestClass : VsTestClassBase
    {
        public override void InitializeTestManager()
        {
            base.InitializeTestManager();
            ATService.Initialize();
        }

        public override void CleanupTestManager()
        {
            ATService.CleanUp();
            base.CleanupTestManager();
        }
    }
}

