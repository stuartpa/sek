using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Microsoft.SpecExplorer.Runtime.Testing;
using Chat.Adapter;

namespace GeneratedTests
{
    public class ChatTestClass : VsTestClassBase
    {
        public override void InitializeTestManager()
        {
            base.InitializeTestManager();
        }

        public override void CleanupTestManager()
        {
            ChatSetupAdapter.Kill();
            ChatAdapter.StopThread();
            base.CleanupTestManager();
        }
    }
}
