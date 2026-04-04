using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlCli.Tests
{
	/// <summary>
	/// Basic smoke test to verify the test runner and project build.
	/// </summary>
	[TestClass]
	public class SmokeTest
	{
		/// <summary>
		/// Confirms that the project builds and the test runner executes successfully.
		/// </summary>
		[TestMethod]
		public void ProjectBuildsAndTestRunnerWorks()
		{
			Assert.IsTrue( true );
		}
	}
}
