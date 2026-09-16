using UnityEditor.TestTools.TestRunner.Api;

namespace SsalMuk.Editor.Validation
{
    internal sealed class ValidationCallbacks : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result) => EditorValidationBridge.Complete(result);
        public void OnError(string message) => EditorValidationBridge.Fail("TestExecutionFailed", message);
    }
}
