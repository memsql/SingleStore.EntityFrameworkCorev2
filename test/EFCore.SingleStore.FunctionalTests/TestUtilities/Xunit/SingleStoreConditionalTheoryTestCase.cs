﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace EntityFrameworkCore.SingleStore.FunctionalTests.TestUtilities.Xunit
{
    /// <remarks>
    /// We cannot inherit from ConditionalTheoryTestCase, because it's sealed.
    /// </remarks>
    public sealed class SingleStoreConditionalTheoryTestCase : XunitTheoryTestCase
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SingleStoreConditionalTheoryTestCase()
        {
        }

        public SingleStoreConditionalTheoryTestCase(
            IMessageSink diagnosticMessageSink,
            TestMethodDisplay defaultMethodDisplay,
            TestMethodDisplayOptions defaultMethodDisplayOptions,
            ITestMethod testMethod)
            : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod)
        {
        }

        public override async Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            => await XunitTestCaseExtensions.TrySkipAsync(this, messageBus)
                ? new RunSummary { Total = 1, Skipped = 1 }
                : await new SingleStoreXunitTheoryTestCaseRunner(
                        this,
                        DisplayName,
                        SkipReason,
                        constructorArguments,
                        diagnosticMessageSink,
                        messageBus,
                        aggregator,
                        cancellationTokenSource)
                    .RunAsync();
    }
}
