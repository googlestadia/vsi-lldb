// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NLog;
using System;

namespace TestsCommon.TestSupport
{
    // Captures log output in unit tests.
    //
    // Example usage:
    //   var logSpy = new NLogSpy();
    //   logSpy.Attach();
    //
    //   // Exercise system under test
    //
    //   Assert.That(logSpy.GetOutput().Contains("Some Expected String"));
    //
    //   logSpy.Detach();
    //
    // TODO: Rename and refactor NLogSpy as a Test environment log sandbox object.
    public class NLogSpy
    {
        private LogFactory logFactory;
        private string name;
        private NLog.Targets.MemoryTarget target;
        private NLog.Config.LoggingRule rule;

        public static NLogSpy CreateUnique(string name)
        {
            var logFactory = new LogFactory();
            logFactory.Configuration = new NLog.Config.LoggingConfiguration();
            return new NLogSpy(logFactory, name);
        }

        private NLogSpy(LogFactory logFactory, string name)
        {
            this.logFactory = logFactory;
            this.name = $"{name}.logger";

            target = new NLog.Targets.MemoryTarget($"{name}.target");
            target.Layout = "${message}";
        }

        public void Attach()
        {
            if (rule != null)
            {
                throw new InvalidOperationException("NLogSpy already attached");
            }

            var config = logFactory.Configuration;

            rule = new NLog.Config.LoggingRule(name, NLog.LogLevel.Trace, target);
            config.AddTarget(target);
            config.LoggingRules.Add(rule);

            // IMPORTANT: The configuration is not updated until this property is reassigned, at
            // which point all of the settings are updated all at once. We only "reuse" the existing
            // config object to retain all of the previous settings.
            logFactory.Configuration = config;
        }

        public void Detach()
        {
            if (rule == null)
            {
                throw new InvalidOperationException("NLogSpy not currently attached");
            }

            var config = logFactory.Configuration;
            config.LoggingRules.Remove(rule);
            config.RemoveTarget(target.Name);
            rule = null;

            // IMPORTANT: The configuration is not updated until this property is reassigned, at
            // which point all of the settings are updated all at once. We only "reuse" the existing
            // config object to retain all of the previous settings.
            logFactory.Configuration = config;
        }

        public NLog.ILogger GetLogger()
        {
            return logFactory.GetLogger(name);
        }

        // Returns all the log output captured.
        public string GetOutput()
        {
            return string.Join(Environment.NewLine, target.Logs);
        }

        public void Clear()
        {
            target.Logs.Clear();
        }
    }
}
