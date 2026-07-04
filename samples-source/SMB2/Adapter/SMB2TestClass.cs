using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Microsoft.SpecExplorer.Runtime.Testing;
using SMB2.Adapter;

namespace GeneratedTests
{
    public class SMB2TestClass : VsTestClassBase
    {
        public override void InitializeTestManager()
        {
            base.InitializeTestManager();
        }

        public override void CleanupTestManager()
        {
            Smb2Adapter.Reset();
            base.CleanupTestManager();
        }
    }
}

