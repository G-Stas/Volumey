using System;
using System.Threading.Tasks;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Helper;

namespace Volumey.Tests
{
	[TestClass]
	public class TaskExtensionsTests
	{
		[TestMethod]
		public void AFailedTaskIsLoggedInsteadOfEndingTheApp()
		{
			var log = new Mock<ILog>();

			Task.FromException(new InvalidOperationException("boom")).Forget(log.Object, "work failed");

			log.Verify(l => l.Error("work failed", It.IsAny<InvalidOperationException>()), Times.Once);
		}

		[TestMethod]
		public async Task ATaskThatFailsLaterIsLoggedToo()
		{
			var log = new Mock<ILog>();
			var logged = new TaskCompletionSource();
			log.Setup(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>())).Callback(() => logged.SetResult());

			async Task FailLater()
			{
				await Task.Delay(20);
				throw new InvalidOperationException("later");
			}
			FailLater().Forget(log.Object, "work failed");

			Assert.AreSame(logged.Task, await Task.WhenAny(logged.Task, Task.Delay(3000)), "the failure wasn't logged");
		}

		[TestMethod]
		public void ASuccessfulTaskLogsNothing()
		{
			var log = new Mock<ILog>();

			Task.CompletedTask.Forget(log.Object, "work failed");

			log.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Never);
		}
	}
}
