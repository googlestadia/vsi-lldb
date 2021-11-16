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

namespace YetiVSI.Shared.Metrics
{
    public class DeveloperEventType
    {
        public class Types
        {
            public enum Type
            {
                VsiDebugEventBatch,
                VsiException,
                VsiGameletsClearLogs,
                VsiGameletsEnableSsh,
                VsiGameletsPrepare,
                VsiGameletsSelect,
                UnknownEventType,
                VsiApplicationsGet,
                VsiCoreAttach,
                VsiCoreDownload,
                VsiCoreList,
                VsiDebugAttach,
                VsiDebugContinueAfterAttach,
                VsiDebugCreateExecutableTarget,
                VsiDebugEnd,
                VsiDebugEngineLoadSymbols,
                VsiDebugLoadCore,
                VsiDebugModuleLoadSymbols,
                VsiDebugPreflightBinaryCheck,
                VsiDebugSetupQueries,
                VsiDebugStart,
                VsiDebugWaitDebugger,
                VsiDebugWaitProcess,
                VsiDeployBinary,
                VsiDeployLldbServer,
                VsiGameletsList,
                VsiGameletsStop,
                VsiGameletsGet,
                VsiProcessList,
                VsiInitialized,
                VsiGameletsReadMounts,
                VsiGameLaunchStopPrompt,
                VsiGameLaunchDeleteExisting,
                VsiGameLaunchWaitForStart,
                VsiGameLaunchCreate,
                VsiGameLaunchWaitForEnd,
                VsiGameLaunchGetExisting,
                VsiDebugExpressionEvaluationBatch,
                VsiReportFeedback,
            }
        }
    }
}
