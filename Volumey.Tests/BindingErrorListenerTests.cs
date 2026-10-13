using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

[TestClass]
public class BindingErrorListenerTests
{
	private static (TraceSource source, List<string> written) CreateSource()
	{
		var written = new List<string>();
		var source = new TraceSource("BindingErrorListenerTests", SourceLevels.All);
		source.Listeners.Clear();
		source.Listeners.Add(new BindingErrorListener(written.Add));
		return (source, written);
	}

	[TestMethod]
	public void ABindingErrorIsWrittenWithThePrefix()
	{
		var (source, written) = CreateSource();

		source.TraceEvent(TraceEventType.Error, 40, "BindingExpression path error: 'Volume' property not found on 'object'");

		Assert.ContainsSingle(written);
		Assert.AreEqual("Binding error: 40: BindingExpression path error: 'Volume' property not found on 'object'", written[0]);
	}

	[TestMethod]
	public void AFormattedErrorIsWrittenWithItsArguments()
	{
		var (source, written) = CreateSource();

		source.TraceEvent(TraceEventType.Error, 4, "Cannot find source for binding with reference '{0}'", "ElementName=Box");

		Assert.AreEqual("Binding error: 4: Cannot find source for binding with reference 'ElementName=Box'", written[0]);
	}

	[TestMethod]
	public void WarningsAndInformationAreNotWritten()
	{
		var (source, written) = CreateSource();

		source.TraceEvent(TraceEventType.Warning, 5, "a warning");
		source.TraceEvent(TraceEventType.Information, 6, "information");

		Assert.IsEmpty(written);
	}
}
